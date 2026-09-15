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
    private readonly IAiCapabilityGuard _capabilityGuard;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        IInventoryRepository repository,
        IAiService aiService,
        IFileStorage fileStorage,
        IAiCapabilityGuard capabilityGuard,
        ILogger<InventoryService> logger)
    {
        _repository = repository;
        _aiService = aiService;
        _fileStorage = fileStorage;
        _capabilityGuard = capabilityGuard;
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

    public async Task<AiProcessResult> ProcessAiInventoryInputAsync(
        string userInput,
        AiInputContext context,
        bool trustedDevBypass = false)
    {
        try
        {
            var capabilities = await _capabilityGuard.GetSnapshotAsync(context.CompanyId, trustedDevBypass);
            capabilities.Ensure(Walos.Domain.Features.WalosFeatures.Ai, Walos.Domain.Features.WalosFeatures.Inventory);
            var sessionId = context.SessionId ?? Guid.NewGuid().ToString();

            capabilities.Ensure(Walos.Domain.Features.WalosFeatures.Inventory);
            var existingProducts = await _repository.GetAllProductsAsync(context.CompanyId);
            capabilities.Ensure(Walos.Domain.Features.WalosFeatures.Inventory);
            var categories = await _repository.GetCategoriesAsync(context.CompanyId);
            capabilities.Ensure(Walos.Domain.Features.WalosFeatures.Inventory);
            var units = await _repository.GetUnitsAsync(context.CompanyId);

            var aiResponse = await _aiService.ProcessInventoryInputAsync(userInput, new AiContext
            {
                CompanyName = context.CompanyName,
                BranchName = context.BranchName,
                ExistingProductsCount = existingProducts.Count(),
                ExistingProductNames = existingProducts.Select(p => p.Name).ToList(),
                Categories = categories.Select(c => c.Name).ToList(),
                Units = units.Select(u => $"{u.Name} ({u.Abbreviation})").ToList()
            }, history: null);

            if (aiResponse.Action is "add_stock" or "create_and_stock")
            {
                return new AiProcessResult
                {
                    SessionId = sessionId,
                    Action = "stock_mutation_disabled",
                    Response = "Por seguridad, el ingreso de stock desde IA no esta disponible en V1. Registra la entrada desde Inventario para garantizar una operacion atomica y auditable.",
                    Confidence = aiResponse.Confidence,
                    RequiresConfirmation = false
                };
            }

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

    public async Task<AiConfirmResult> ConfirmAiActionAsync(
        long interactionId,
        long userId,
        long companyId,
        bool trustedDevBypass = false)
    {
        var capabilities = await _capabilityGuard.GetSnapshotAsync(companyId, trustedDevBypass);
        capabilities.Ensure(
            Walos.Domain.Features.WalosFeatures.Ai,
            Walos.Domain.Features.WalosFeatures.Inventory);

        return new AiConfirmResult
        {
            Success = false,
            Message = "Por seguridad, el ingreso de stock desde IA no esta disponible en V1. Registra la entrada desde Inventario para garantizar una operacion atomica y auditable."
        };
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
