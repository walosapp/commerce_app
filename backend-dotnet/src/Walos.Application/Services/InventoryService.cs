using System.Text.Json;
using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Inventory;
using Walos.Application.Storage;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _repository;
    private readonly IAiService _aiService;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        IInventoryRepository repository,
        IAiService aiService,
        IFileStorage fileStorage,
        ILogger<InventoryService> logger)
    {
        _repository = repository;
        _aiService = aiService;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<long?> ResolveBranchAsync(
        long companyId,
        long? tenantBranchId,
        long? requestedBranchId,
        bool required = false)
    {
        if (tenantBranchId.HasValue &&
            requestedBranchId.HasValue &&
            requestedBranchId.Value != tenantBranchId.Value)
            throw new ValidationException("La sucursal solicitada no coincide con la sucursal autenticada");

        var branchId = tenantBranchId ?? requestedBranchId;
        if (!branchId.HasValue)
        {
            if (required)
                throw new ValidationException("ID de sucursal requerido");
            return null;
        }

        if (!await _repository.IsActiveBranchInCompanyAsync(branchId.Value, companyId))
            throw new NotFoundException("Sucursal");

        return branchId;
    }

    public async Task<Product> CreateProductAsync(long companyId, long userId, long? branchId, CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("El nombre del producto es requerido");
        if (request.CategoryId <= 0)
            throw new ValidationException("Selecciona una categoria valida. Puedes crear categorias en Configuracion > Catalogo");
        if (request.UnitId <= 0)
            throw new ValidationException("Selecciona una unidad de medida valida. Puedes crearlas en Configuracion > Catalogo");

        if (!await _repository.IsActiveCategoryInCompanyAsync(request.CategoryId, companyId))
            throw new NotFoundException("Categoria");
        if (!await _repository.IsActiveUnitInCompanyAsync(request.UnitId, companyId))
            throw new NotFoundException("Unidad");

        if (branchId.HasValue && !await _repository.IsActiveBranchInCompanyAsync(branchId.Value, companyId))
            throw new NotFoundException("Sucursal");

        var sku = string.IsNullOrWhiteSpace(request.Sku)
            ? $"SKU-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}"
            : request.Sku.Trim();

        var product = new Product
        {
            CompanyId = companyId,
            Name = request.Name,
            Sku = sku,
            Barcode = request.Barcode,
            Description = request.Description,
            CategoryId = request.CategoryId,
            UnitId = request.UnitId,
            CostPrice = request.CostPrice,
            SalePrice = request.SalePrice,
            MinStock = request.MinStock,
            MaxStock = request.MaxStock,
            ReorderPoint = request.ReorderPoint,
            IsPerishable = request.IsPerishable,
            ShelfLifeDays = request.ShelfLifeDays,
            ProductType = request.ProductType,
            TrackStock = request.TrackStock,
            IsForSale = request.IsForSale,
            CreatedBy = userId
        };

        var created = await _repository.CreateProductAsync(product);

        if (branchId.HasValue)
            await _repository.CreateStockEntryAsync(branchId.Value, created.Id, 0, companyId);

        _logger.LogInformation("Producto creado: {Name}, ProductId: {Id}, UserId: {UserId}",
            created.Name, created.Id, userId);

        return created;
    }

    public async Task<Product?> UpdateProductAsync(long id, long companyId, long userId, UpdateProductRequest request)
    {
        var existing = await _repository.GetProductByIdAsync(id, companyId);
        if (existing is null)
            return null;

        if (!await _repository.IsActiveCategoryInCompanyAsync(request.CategoryId, companyId))
            throw new NotFoundException("Categoria");
        if (!await _repository.IsActiveUnitInCompanyAsync(request.UnitId, companyId))
            throw new NotFoundException("Unidad");

        existing.Name = request.Name;
        existing.Sku = request.Sku;
        existing.Barcode = request.Barcode;
        existing.Description = request.Description;
        existing.CategoryId = request.CategoryId;
        existing.UnitId = request.UnitId;
        existing.CostPrice = request.CostPrice;
        existing.SalePrice = request.SalePrice;
        existing.MarginPercentage = request.MarginPercentage;
        existing.MinStock = request.MinStock;
        existing.MaxStock = request.MaxStock;
        existing.ReorderPoint = request.ReorderPoint;
        existing.IsPerishable = request.IsPerishable;
        existing.ShelfLifeDays = request.ShelfLifeDays;
        existing.ProductType = request.ProductType;
        existing.TrackStock = request.TrackStock;
        existing.IsForSale = request.IsForSale;

        var updated = await _repository.UpdateProductAsync(existing);

        _logger.LogInformation("Producto actualizado: {Name}, ProductId: {Id}, UserId: {UserId}",
            updated.Name, updated.Id, userId);

        return updated;
    }

    public async Task<StoredFile> UploadProductImageAsync(
        long productId,
        long companyId,
        Stream content,
        string? declaredFileName,
        string? declaredContentType,
        CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetProductByIdAsync(productId, companyId);
        if (product is null)
            throw new NotFoundException("Producto");

        var previousReference = product.ImageUrl;
        var stored = await _fileStorage.UploadImageAsync(new ImageUploadRequest(
            companyId,
            ImageStorageScope.Product,
            productId,
            content,
            declaredFileName,
            declaredContentType), cancellationToken);

        try
        {
            var updated = await _repository.TryUpdateProductImageAsync(
                productId,
                companyId,
                previousReference,
                stored.ObjectKey);

            if (!updated)
                throw new BusinessException(
                    "La imagen del producto cambió durante la actualización. Intenta nuevamente.",
                    "PRODUCT_IMAGE_CONFLICT");
        }
        catch
        {
            await DeleteBestEffortAsync(stored.ObjectKey, CancellationToken.None);
            throw;
        }

        if (_fileStorage.IsManagedReference(previousReference))
            await DeleteBestEffortAsync(previousReference!, CancellationToken.None);

        _logger.LogInformation(
            "Imagen de producto actualizada. CompanyId: {CompanyId}, ProductId: {ProductId}, ObjectKey: {ObjectKey}",
            companyId,
            productId,
            stored.ObjectKey);

        return stored;
    }

    private async Task DeleteBestEffortAsync(string reference, CancellationToken cancellationToken)
    {
        try
        {
            await _fileStorage.DeleteIfManagedAsync(reference, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar el objeto administrado {Reference}", reference);
        }
    }

    public async Task<Stock> AddStockAsync(long companyId, long userId, long? tenantBranchId, AddStockRequest request)
    {
        var branchId = await ResolveBranchAsync(companyId, tenantBranchId, request.BranchId, required: true);
        if (request.ProductId <= 0)
            throw new ValidationException("Producto requerido");
        if (request.Quantity <= 0)
            throw new ValidationException("La cantidad debe ser mayor a cero");

        var product = await _repository.GetProductByIdAsync(request.ProductId, companyId);
        if (product is null)
            throw new NotFoundException("Producto");

        if (request.UnitCost.HasValue)
        {
            var currentStock = await _repository.GetStockByProductAsync(branchId.Value, request.ProductId, companyId);
            var currentQuantity = currentStock?.Quantity ?? 0;
            var currentCost = product.CostPrice;

            var weightedCost = currentQuantity + request.Quantity > 0
                ? ((currentQuantity * currentCost) + (request.Quantity * request.UnitCost.Value)) / (currentQuantity + request.Quantity)
                : request.UnitCost.Value;

            await _repository.UpdateProductCostAndPriceAsync(
                request.ProductId,
                companyId,
                Math.Round(weightedCost, 2));
        }

        var stock = await _repository.UpdateStockAsync(branchId.Value, request.ProductId, request.Quantity, companyId);

        await _repository.CreateMovementAsync(new Movement
        {
            CompanyId = companyId,
            BranchId = branchId.Value,
            ProductId = request.ProductId,
            MovementType = "entry",
            Quantity = request.Quantity,
            UnitCost = request.UnitCost,
            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? "Ingreso manual de stock"
                : request.Notes,
            CreatedByAi = false,
            CreatedBy = userId
        });

        _logger.LogInformation(
            "Stock agregado manualmente. ProductId: {ProductId}, BranchId: {BranchId}, Quantity: {Quantity}, UserId: {UserId}",
            request.ProductId,
            branchId.Value,
            request.Quantity,
            userId);

        return stock;
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
