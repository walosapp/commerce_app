using System.Text.Json;
using Microsoft.Extensions.Logging;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _repository;
    private readonly IAiService _aiService;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        IInventoryRepository repository,
        IAiService aiService,
        ILogger<InventoryService> logger)
    {
        _repository = repository;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<AiProcessResult> ProcessAiInventoryInputAsync(string userInput, AiInputContext context)
    {
        try
        {
            var sessionId = context.SessionId ?? Guid.NewGuid().ToString();

            var existingProducts = await _repository.GetAllProductsAsync(context.CompanyId);
            var categories = await _repository.GetCategoriesAsync(context.CompanyId);
            var units = await _repository.GetUnitsAsync(context.CompanyId);

            // Build conversation history from previous interactions in this session
            List<AiConversationMessage>? history = null;
            if (context.SessionId is not null)
            {
                var previousInteractions = await _repository.GetAiInteractionsBySessionAsync(context.SessionId, context.CompanyId);
                history = previousInteractions
                    .SelectMany(i => new[]
                    {
                        new AiConversationMessage { Role = "user", Content = i.UserInput },
                        new AiConversationMessage { Role = "assistant", Content = i.AiResponse }
                    })
                    .ToList();
            }

            var aiResponse = await _aiService.ProcessInventoryInputAsync(userInput, new AiContext
            {
                CompanyName = context.CompanyName,
                BranchName = context.BranchName,
                ExistingProductsCount = existingProducts.Count(),
                ExistingProductNames = existingProducts.Select(p => p.Name).ToList(),
                Categories = categories.Select(c => c.Name).ToList(),
                Units = units.Select(u => $"{u.Name} ({u.Abbreviation})").ToList()
            }, history);

            var interaction = new AiInteraction
            {
                CompanyId = context.CompanyId,
                BranchId = context.BranchId ?? 0,
                UserId = context.UserId,
                SessionId = sessionId,
                InteractionType = context.InputType,
                UserInput = userInput,
                AiResponse = aiResponse.Response,
                AiAction = aiResponse.Action,
                ProcessedData = JsonSerializer.Serialize(aiResponse.Data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }),
                ActionStatus = "pending",
                ConfidenceScore = aiResponse.Confidence,
                AiModel = aiResponse.Metadata?.Model ?? "gpt-4",
                TokensUsed = aiResponse.Metadata?.TokensUsed ?? 0
            };

            var saved = await _repository.SaveAiInteractionAsync(interaction);

            return new AiProcessResult
            {
                InteractionId = saved.Id,
                SessionId = sessionId,
                Action = aiResponse.Action,
                Response = aiResponse.Response,
                Data = aiResponse.Data,
                Confidence = aiResponse.Confidence,
                RequiresConfirmation = aiResponse.Confidence < 90
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando entrada IA");
            throw;
        }
    }

    public async Task<AiConfirmResult> ConfirmAiActionAsync(long interactionId, long userId, long companyId)
    {
        try
        {
            var interaction = await _repository.GetAiInteractionByIdAsync(interactionId, companyId);

            if (interaction is null)
                throw new NotFoundException("Interacción");

            var data = JsonSerializer.Deserialize<AiInventoryData>(
                interaction.ProcessedData ?? "{}",
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true });

            if (interaction.AiAction is "add_stock" or "create_and_stock")
            {
                var movements = new List<Movement>();
                var createdProducts = new List<string>();
                var categories = await _repository.GetCategoriesAsync(companyId);
                var units = await _repository.GetUnitsAsync(companyId);

                foreach (var product in data?.Products ?? new List<AiProductEntry>())
                {
                    var productRecords = await _repository.FindProductsByNameAsync(companyId, product.Name);
                    var productRecord = productRecords.FirstOrDefault();

                    if (productRecord is null)
                    {
                        // --- NEW PRODUCT: create with profit margin ---
                        product.IsNew = true;
                        var category = categories.FirstOrDefault(c =>
                            c.Name.Equals(product.Category, StringComparison.OrdinalIgnoreCase))
                            ?? categories.FirstOrDefault();

                        var unit = units.FirstOrDefault(u =>
                            u.Name.Equals(product.Unit?.Split(" (")[0], StringComparison.OrdinalIgnoreCase)
                            || u.Abbreviation.Equals(product.Unit, StringComparison.OrdinalIgnoreCase))
                            ?? units.FirstOrDefault();

                        if (category is null || unit is null)
                            throw new BusinessException("No hay categorías o unidades disponibles. Créalas primero.");

                        // Calculate sale price from margin; fallback to explicit sale_price or 30% default margin
                        var salePrice = product.ProfitMargin > 0
                            ? product.UnitCost * (1 + product.ProfitMargin / 100m)
                            : product.SalePrice > 0
                                ? product.SalePrice
                                : product.UnitCost * 1.3m;

                        var sku = $"AI-{DateTime.UtcNow:yyyyMMddHHmmss}-{movements.Count + 1}";

                        var newProduct = await _repository.CreateProductAsync(new Product
                        {
                            CompanyId = companyId,
                            Name = product.Name,
                            Sku = sku,
                            Description = product.Description ?? "Producto creado por IA",
                            CategoryId = category.Id,
                            UnitId = unit.Id,
                            CostPrice = product.UnitCost,
                            SalePrice = Math.Round(salePrice, 2),
                            MinStock = product.MinStock > 0 ? product.MinStock : 10,
                            MaxStock = 0,
                            ReorderPoint = 0,
                            IsPerishable = false,
                            IsActive = true,
                            CreatedBy = userId
                        });

                        await _repository.CreateStockEntryAsync(
                            interaction.BranchId, newProduct.Id, 0, companyId);

                        productRecord = newProduct;
                        createdProducts.Add($"{product.Name} (costo: ${product.UnitCost:N0}, venta: ${salePrice:N0}, margen: {(product.ProfitMargin > 0 ? product.ProfitMargin : 30)}%)");
                    }
                    else
                    {
                        // --- EXISTING PRODUCT: weighted average cost ---
                        var currentStock = await _repository.GetStockByProductAsync(
                            interaction.BranchId, productRecord.Id, companyId);
                        var currentQty = currentStock?.Quantity ?? 0;
                        var currentCost = productRecord.CostPrice;

                        // Weighted average: (currentQty * currentCost + newQty * newCost) / (currentQty + newQty)
                        var totalQty = currentQty + product.Quantity;
                        var weightedAvgCost = totalQty > 0
                            ? (currentQty * currentCost + product.Quantity * product.UnitCost) / totalQty
                            : product.UnitCost;
                        weightedAvgCost = Math.Round(weightedAvgCost, 2);

                        // Update product cost price
                        await _repository.UpdateProductCostAndPriceAsync(
                            productRecord.Id, companyId, weightedAvgCost);

                        _logger.LogInformation(
                            "Costo promedio ponderado de {Product}: ({OldQty} × ${OldCost} + {NewQty} × ${NewCost}) / {TotalQty} = ${AvgCost}",
                            product.Name, currentQty, currentCost, product.Quantity, product.UnitCost, totalQty, weightedAvgCost);
                    }

                    if (productRecord is null)
                        throw new BusinessException($"Producto \"{product.Name}\" no encontrado. Créalo primero.");

                    await _repository.UpdateStockAsync(
                        interaction.BranchId,
                        productRecord.Id,
                        product.Quantity,
                        companyId);

                    var movement = await _repository.CreateMovementAsync(new Movement
                    {
                        CompanyId = companyId,
                        BranchId = interaction.BranchId,
                        ProductId = productRecord.Id,
                        MovementType = "purchase",
                        Quantity = product.Quantity,
                        UnitCost = product.UnitCost,
                        Notes = $"Entrada registrada por IA: {interaction.UserInput}",
                        CreatedByAi = true,
                        AiConfidence = interaction.ConfidenceScore,
                        AiMetadata = JsonSerializer.Serialize(new { interactionId }),
                        CreatedBy = userId
                    });

                    movements.Add(movement);
                }

                await _repository.UpdateAiInteractionStatusAsync(interactionId, "success", true, companyId);

                var msg = createdProducts.Any()
                    ? $"{movements.Count} producto(s) procesado(s). Nuevos creados: {string.Join(", ", createdProducts)}"
                    : $"{movements.Count} producto(s) agregado(s) al inventario";

                return new AiConfirmResult
                {
                    Success = true,
                    Message = msg,
                    Movements = movements
                };
            }

            throw new BusinessException("Acción no soportada");
        }
        catch (Exception ex) when (ex is not NotFoundException and not BusinessException)
        {
            _logger.LogError(ex, "Error confirmando acción IA");
            throw;
        }
    }

    public async Task<IEnumerable<Stock>> GetLowStockProductsAsync(long companyId, long branchId)
    {
        try
        {
            var stock = await _repository.GetStockByBranchAsync(branchId, companyId);
            return stock.Where(s => s.StockStatus is "low" or "reorder");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo productos con stock bajo");
            throw;
        }
    }

    public async Task<IEnumerable<ProfitReport>> CalculateProductProfitsAsync(
        long companyId, long branchId, DateRange? dateRange = null)
    {
        try
        {
            var rows = await _repository.GetProductProfitsAsync(
                companyId, branchId, dateRange?.StartDate, dateRange?.EndDate);

            return rows.Select(r => new ProfitReport
            {
                Id = r.Id,
                Name = r.Name,
                Sku = r.Sku,
                CostPrice = r.CostPrice,
                SalePrice = r.SalePrice,
                MarginPercentage = r.MarginPercentage,
                TotalSales = r.TotalSales,
                TotalQuantitySold = r.TotalQuantitySold,
                TotalCost = r.TotalCost,
                TotalRevenue = r.TotalRevenue,
                TotalProfit = r.TotalProfit
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculando ganancias");
            throw;
        }
    }
}
