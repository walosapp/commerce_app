using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Inventory;
using Walos.Application.Security;
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
        var userId = _tenant.UserId;
        var product = await _repository.GetProductByIdAsync(id, companyId);

        if (product is null)
            return NotFound(ApiResponse.Fail("Producto no encontrado"));

        return Ok(ApiResponse<Product>.Ok(product));
    }

    /// <summary>
    /// POST /api/v1/inventory/products - Crear producto
    /// </summary>
    [HttpPost("products")]
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
    {
        try
        {
            var created = await _service.CreateProductAsync(_tenant.CompanyId, _tenant.UserId, _tenant.BranchId, request);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<Product>.Ok(created, "Producto creado exitosamente"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando producto {Name}", request.Name);
            return BadRequest(ApiResponse.Fail($"Error al crear el producto: {ex.Message}"));
        }
    }

    /// <summary>
    /// PUT /api/v1/inventory/products/{id} - Actualizar producto
    /// </summary>
    [HttpPut("products/{id:long}")]
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
    public async Task<IActionResult> UpdateProduct(long id, [FromBody] UpdateProductRequest request)
    {
        var updated = await _service.UpdateProductAsync(id, _tenant.CompanyId, _tenant.UserId, request);
        if (updated is null)
            return NotFound(ApiResponse.Fail("Producto no encontrado"));

        return Ok(ApiResponse<Product>.Ok(updated, "Producto actualizado exitosamente"));
    }

    /// <summary>
    /// POST /api/v1/inventory/products/{id}/image - Subir imagen de producto
    /// </summary>
    [HttpPost("products/{id:long}/image")]
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
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
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
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
    [Authorize(Roles = "dev,super_admin,admin,manager")]
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
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
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

                // Crear entrada de stock con cantidad inicial si se especificó
                if (branchId.HasValue)
                {
                    var initialQty = row.Quantity > 0 ? row.Quantity : 0;
                    await _repository.CreateStockEntryAsync(branchId.Value, savedProduct.Id, initialQty, companyId);

                    // Si hay cantidad inicial, crear movimiento de tipo 'purchase' para trazabilidad
                    if (initialQty > 0)
                    {
                        await _repository.CreateMovementAsync(new Movement
                        {
                            CompanyId = companyId,
                            ProductId = savedProduct.Id,
                            BranchId = branchId.Value,
                            Quantity = initialQty,
                            UnitCost = savedProduct.CostPrice,
                            MovementType = "purchase",
                            Notes = "Stock inicial por importación de Excel",
                            CreatedBy = userId
                        });
                    }
                }

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
        var branch = await _service.ResolveBranchAsync(companyId, _tenant.BranchId, branchId, required: true);

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
        var branch = await _service.ResolveBranchAsync(companyId, _tenant.BranchId, branchId, required: true);

        var lowStock = await _service.GetLowStockProductsAsync(companyId, branch.Value);
        var list = lowStock.ToList();

        return Ok(ApiResponse<List<Stock>>.Ok(list, count: list.Count));
    }

    /// <summary>
    /// POST /api/v1/inventory/stock/add - Agregar stock manualmente
    /// </summary>
    [HttpPost("stock/add")]
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
    public async Task<IActionResult> AddStock([FromBody] AddStockRequest request)
    {
        var stock = await _service.AddStockAsync(_tenant.CompanyId, _tenant.UserId, _tenant.BranchId, request);
        return Ok(ApiResponse<Stock>.Ok(stock, "Stock agregado exitosamente"));
    }

    /// <summary>
    /// POST /api/v1/inventory/ai/process - Procesar entrada con IA
    /// </summary>
    [HttpPost("ai/process")]
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
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
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
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
        var branch = await _service.ResolveBranchAsync(companyId, _tenant.BranchId, branchId);

        var alerts = await _repository.GetActiveAlertsAsync(companyId, branch);
        var list = alerts.ToList();

        return Ok(ApiResponse<List<Alert>>.Ok(list, count: list.Count));
    }

    /// <summary>
    /// GET /api/v1/inventory/reports/profits - Reporte de ganancias
    /// </summary>
    [HttpGet("reports/profits")]
    [Authorize(Policy = WalosPolicies.Finance)]
    public async Task<IActionResult> GetProfitsReport(
        [FromQuery] long? branchId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var companyId = _tenant.CompanyId;
        var branch = await _service.ResolveBranchAsync(companyId, _tenant.BranchId, branchId, required: true);

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


