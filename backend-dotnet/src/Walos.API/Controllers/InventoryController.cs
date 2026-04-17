using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Inventory;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;
using Walos.Infrastructure.Services;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryRepository _repository;
    private readonly IInventoryService _service;
    private readonly ITenantContext _tenant;
    private readonly ILogger<InventoryController> _logger;
    private readonly ProductExcelService _excel;

    public InventoryController(
        IInventoryRepository repository,
        IInventoryService service,
        ITenantContext tenant,
        ILogger<InventoryController> logger,
        ProductExcelService excel)
    {
        _repository = repository;
        _service = service;
        _tenant = tenant;
        _logger = logger;
        _excel = excel;
    }

    /// <summary>
    /// GET /api/v1/inventory/products - Listar productos
    /// </summary>
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] long? categoryId,
        [FromQuery] bool? isActive,
        [FromQuery] string? search)
    {
        var companyId = _tenant.CompanyId;
        var filters = new ProductFilter
        {
            CategoryId = categoryId,
            IsActive = isActive,
            Search = search
        };

        var products = await _repository.GetAllProductsAsync(companyId, filters);
        var list = products.ToList();

        return Ok(ApiResponse<List<Product>>.Ok(list, count: list.Count));
    }

    /// <summary>
    /// GET /api/v1/inventory/products/{id} - Obtener producto
    /// </summary>
    [HttpGet("products/{id:long}")]
    public async Task<IActionResult> GetProductById(long id)
    {
        var companyId = _tenant.CompanyId;
        var product = await _repository.GetProductByIdAsync(id, companyId);

        if (product is null)
            return NotFound(ApiResponse.Fail("Producto no encontrado"));

        return Ok(ApiResponse<Product>.Ok(product));
    }

    /// <summary>
    /// POST /api/v1/inventory/products - Crear producto
    /// </summary>
    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ApiResponse.Fail("El nombre del producto es requerido"));
        if (request.CategoryId <= 0)
            return BadRequest(ApiResponse.Fail("Selecciona una categoria valida. Puedes crear categorias en Configuracion > Catalogo"));
        if (request.UnitId <= 0)
            return BadRequest(ApiResponse.Fail("Selecciona una unidad de medida valida. Puedes crearlas en Configuracion > Catalogo"));

        var companyId = _tenant.CompanyId;
        var userId = _tenant.UserId;

        var product = new Product
        {
            CompanyId = companyId,
            Name = request.Name,
            Sku = request.Sku,
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

        _logger.LogInformation("Producto creado: {Name}, ProductId: {Id}, UserId: {UserId}",
            created.Name, created.Id, userId);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<Product>.Ok(created, "Producto creado exitosamente"));
    }

    /// <summary>
    /// PUT /api/v1/inventory/products/{id} - Actualizar producto
    /// </summary>
    [HttpPut("products/{id:long}")]
    public async Task<IActionResult> UpdateProduct(long id, [FromBody] UpdateProductRequest request)
    {
        var companyId = _tenant.CompanyId;
        var userId = _tenant.UserId;

        var existing = await _repository.GetProductByIdAsync(id, companyId);
        if (existing is null)
            return NotFound(ApiResponse.Fail("Producto no encontrado"));

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

        return Ok(ApiResponse<Product>.Ok(updated, "Producto actualizado exitosamente"));
    }

    /// <summary>
    /// POST /api/v1/inventory/products/{id}/image - Subir imagen de producto
    /// </summary>
    [HttpPost("products/{id:long}/image")]
    [RequestSizeLimit(2 * 1024 * 1024)] // 2MB
    public async Task<IActionResult> UploadProductImage(long id, IFormFile file)
    {
        var companyId = _tenant.CompanyId;

        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("No se proporcionó archivo"));

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest(ApiResponse.Fail("Formato no permitido. Use JPG, PNG o WebP"));

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest(ApiResponse.Fail("Extensión no permitida"));

        var product = await _repository.GetProductByIdAsync(id, companyId);
        if (product is null)
            return NotFound(ApiResponse.Fail("Producto no encontrado"));

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products");
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"{id}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var imageUrl = $"/uploads/products/{fileName}";
        await _repository.UpdateProductImageAsync(id, companyId, imageUrl);

        _logger.LogInformation("Imagen subida para producto {ProductId}: {Url}", id, imageUrl);

        return Ok(ApiResponse<object>.Ok(new { imageUrl }, "Imagen subida exitosamente"));
    }

    /// <summary>
    /// DELETE /api/v1/inventory/products/{id} - Eliminar producto (soft delete)
    /// </summary>
    [HttpDelete("products/{id:long}")]
    public async Task<IActionResult> DeleteProduct(long id)
    {
        var companyId = _tenant.CompanyId;
        var userId = _tenant.UserId;

        await _repository.SoftDeleteProductAsync(id, companyId, userId);

        _logger.LogInformation("Producto eliminado (soft): ProductId: {Id}, UserId: {UserId}", id, userId);

        return Ok(ApiResponse.Ok("Producto eliminado exitosamente"));
    }

    /// <summary>
    /// GET /api/v1/inventory/products/template - Descargar plantilla Excel
    /// </summary>
    [HttpGet("products/template")]
    [Authorize(Roles = "dev,admin,manager")]
    public async Task<IActionResult> DownloadTemplate()
    {
        var companyId = _tenant.CompanyId;
        var categories = (await _repository.GetCategoriesAsync(companyId)).Where(c => c.IsActive).Select(c => c.Name);
        var units = (await _repository.GetUnitsAsync(companyId)).Where(u => u.IsActive).Select(u => u.Name);
        var bytes = _excel.GenerateTemplate(categories, units);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "plantilla_productos.xlsx");
    }

    /// <summary>
    /// POST /api/v1/inventory/products/import - Importar productos desde Excel
    /// </summary>
    [HttpPost("products/import")]
    [Authorize(Roles = "dev,admin,manager")]
    public async Task<IActionResult> ImportProducts(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("Adjunta un archivo Excel"));

        var companyId = _tenant.CompanyId;
        var userId = _tenant.UserId;
        var branchId = _tenant.BranchId;

        var categories = (await _repository.GetCategoriesAsync(companyId)).Where(c => c.IsActive).ToList();
        var units      = (await _repository.GetUnitsAsync(companyId)).Where(u => u.IsActive).ToList();

        var catMap  = categories.ToDictionary(c => c.Name.ToLowerInvariant(), c => c.Id);
        var unitMap = units.ToDictionary(u => u.Name.ToLowerInvariant(), u => u.Id);

        using var stream = file.OpenReadStream();
        var (valid, errors) = _excel.ParseImport(stream, catMap, unitMap);

        var savedCount = 0;
        var rowErrors  = new List<object>(errors.Select(e => (object)new { e.RowNumber, e.Name, e.Error }));

        foreach (var row in valid)
        {
            try
            {
                var product = new Product
                {
                    CompanyId        = companyId,
                    Name             = row.Name,
                    Sku              = row.Sku,
                    Barcode          = row.Barcode,
                    Description      = row.Description,
                    CategoryId       = catMap[row.CategoryName],
                    UnitId           = unitMap[row.UnitName],
                    CostPrice        = row.CostPrice,
                    SalePrice        = row.SalePrice,
                    MarginPercentage = row.MarginPercentage,
                    MinStock         = row.MinStock,
                    MaxStock         = row.MaxStock,
                    ReorderPoint     = row.ReorderPoint,
                    ProductType      = row.ProductType,
                    TrackStock       = row.TrackStock,
                    IsForSale        = row.IsForSale,
                    CreatedBy        = userId,
                };
                var savedProduct = await _repository.CreateProductAsync(product);

                if (branchId.HasValue)
                    await _repository.CreateStockEntryAsync(branchId.Value, savedProduct.Id, 0, companyId);

                savedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error importando fila {Row} SKU={Sku}", row.RowNumber, row.Sku);
                rowErrors.Add(new { row.RowNumber, row.Name, Error = $"Error al guardar: {ex.Message}" });
            }
        }

        var result = new
        {
            Created = savedCount,
            Errors  = rowErrors,
        };

        var msg = rowErrors.Count == 0
            ? $"{savedCount} producto(s) guardados exitosamente"
            : $"{savedCount} guardados, {rowErrors.Count} con errores";

        return Ok(ApiResponse<object>.Ok(result, msg));
    }

    /// <summary>
    /// GET /api/v1/inventory/categories - Listar categorías
    /// </summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var companyId = _tenant.CompanyId;
        var categories = await _repository.GetCategoriesAsync(companyId);
        var list = categories.ToList();
        return Ok(ApiResponse<List<CategoryInfo>>.Ok(list, count: list.Count));
    }

    /// <summary>
    /// GET /api/v1/inventory/units - Listar unidades
    /// </summary>
    [HttpGet("units")]
    public async Task<IActionResult> GetUnits()
    {
        var companyId = _tenant.CompanyId;
        var units = await _repository.GetUnitsAsync(companyId);
        var list = units.ToList();
        return Ok(ApiResponse<List<UnitInfo>>.Ok(list, count: list.Count));
    }

    /// <summary>
    /// GET /api/v1/inventory/stock - Obtener stock
    /// </summary>
    [HttpGet("stock")]
    public async Task<IActionResult> GetStock([FromQuery] long? branchId)
    {
        var companyId = _tenant.CompanyId;
        var branch = branchId ?? _tenant.BranchId;

        if (branch is null)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var stock = await _repository.GetStockByBranchAsync(branch.Value, companyId);
        var list = stock.ToList();

        return Ok(ApiResponse<List<Stock>>.Ok(list, count: list.Count));
    }

    /// <summary>
    /// GET /api/v1/inventory/stock/low - Productos con stock bajo
    /// </summary>
    [HttpGet("stock/low")]
    public async Task<IActionResult> GetLowStock([FromQuery] long? branchId)
    {
        var companyId = _tenant.CompanyId;
        var branch = branchId ?? _tenant.BranchId;

        if (branch is null)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var lowStock = await _service.GetLowStockProductsAsync(companyId, branch.Value);
        var list = lowStock.ToList();

        return Ok(ApiResponse<List<Stock>>.Ok(list, count: list.Count));
    }

    /// <summary>
    /// POST /api/v1/inventory/stock/add - Agregar stock manualmente
    /// </summary>
    [HttpPost("stock/add")]
    public async Task<IActionResult> AddStock([FromBody] AddStockRequest request)
    {
        var companyId = _tenant.CompanyId;
        var userId = _tenant.UserId;
        var branchId = request.BranchId ?? _tenant.BranchId;

        if (branchId is null)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        if (request.ProductId <= 0)
            return BadRequest(ApiResponse.Fail("Producto requerido"));

        if (request.Quantity <= 0)
            return BadRequest(ApiResponse.Fail("La cantidad debe ser mayor a cero"));

        var product = await _repository.GetProductByIdAsync(request.ProductId, companyId);
        if (product is null)
            return NotFound(ApiResponse.Fail("Producto no encontrado"));

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

        return Ok(ApiResponse<Stock>.Ok(stock, "Stock agregado exitosamente"));
    }

    /// <summary>
    /// POST /api/v1/inventory/ai/process - Procesar entrada con IA
    /// </summary>
    [HttpPost("ai/process")]
    public async Task<IActionResult> ProcessAIInput([FromBody] AiInputRequest request)
    {
        var companyId = _tenant.CompanyId;
        var userId = _tenant.UserId;
        var branchId = _tenant.BranchId;

        var context = new AiInputContext
        {
            CompanyId = companyId,
            BranchId = branchId,
            UserId = userId,
            InputType = request.InputType,
            SessionId = request.SessionId
        };

        var result = await _service.ProcessAiInventoryInputAsync(request.UserInput, context);

        return Ok(ApiResponse<AiProcessResult>.Ok(result, "Entrada procesada por IA"));
    }

    /// <summary>
    /// POST /api/v1/inventory/ai/confirm/{interactionId} - Confirmar acción de IA
    /// </summary>
    [HttpPost("ai/confirm/{interactionId:long}")]
    public async Task<IActionResult> ConfirmAIAction(long interactionId)
    {
        var companyId = _tenant.CompanyId;
        var userId = _tenant.UserId;

        var result = await _service.ConfirmAiActionAsync(interactionId, userId, companyId);

        return Ok(ApiResponse<AiConfirmResult>.Ok(result, result.Message));
    }

    /// <summary>
    /// GET /api/v1/inventory/alerts - Obtener alertas
    /// </summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] long? branchId)
    {
        var companyId = _tenant.CompanyId;
        var branch = branchId ?? _tenant.BranchId;

        var alerts = await _repository.GetActiveAlertsAsync(companyId, branch);
        var list = alerts.ToList();

        return Ok(ApiResponse<List<Alert>>.Ok(list, count: list.Count));
    }

    /// <summary>
    /// GET /api/v1/inventory/reports/profits - Reporte de ganancias
    /// </summary>
    [HttpGet("reports/profits")]
    public async Task<IActionResult> GetProfitsReport(
        [FromQuery] long? branchId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var companyId = _tenant.CompanyId;
        var branch = branchId ?? _tenant.BranchId;

        if (branch is null)
            return BadRequest(ApiResponse.Fail("ID de sucursal requerido"));

        var dateRange = (startDate.HasValue || endDate.HasValue)
            ? new DateRange { StartDate = startDate, EndDate = endDate }
            : null;

        var profits = (await _service.CalculateProductProfitsAsync(companyId, branch.Value, dateRange)).ToList();

        var summary = new
        {
            TotalRevenue = profits.Sum(p => p.TotalRevenue),
            TotalCost = profits.Sum(p => p.TotalCost),
            TotalProfit = profits.Sum(p => p.TotalProfit),
            ProductCount = profits.Count
        };

        return Ok(ApiResponse<object>.Ok(new { summary, products = profits }));
    }
}
