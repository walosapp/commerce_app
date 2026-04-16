using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<InventoryRepository> _logger;

    public InventoryRepository(IDbConnectionFactory connectionFactory, ILogger<InventoryRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<Product>> GetAllProductsAsync(long companyId, ProductFilter? filters = null)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var sql = @"
                SELECT 
                    p.id AS Id,
                    p.company_id AS CompanyId,
                    p.name AS Name,
                    p.sku AS Sku,
                    p.barcode AS Barcode,
                    p.description AS Description,
                    p.cost_price AS CostPrice,
                    p.sale_price AS SalePrice,
                    p.margin_percentage AS MarginPercentage,
                    p.min_stock AS MinStock,
                    p.max_stock AS MaxStock,
                    p.reorder_point AS ReorderPoint,
                    p.is_perishable AS IsPerishable,
                    p.shelf_life_days AS ShelfLifeDays,
                    p.product_type AS ProductType,
                    p.track_stock AS TrackStock,
                    p.is_for_sale AS IsForSale,
                    p.is_active AS IsActive,
                    c.name AS CategoryName,
                    u.abbreviation AS UnitAbbreviation,
                    p.created_at AS CreatedAt,
                    p.updated_at AS UpdatedAt
                FROM inventory.products p
                INNER JOIN inventory.categories c ON p.category_id = c.id AND c.company_id = p.company_id
                INNER JOIN inventory.units u ON p.unit_id = u.id AND u.company_id = p.company_id
                WHERE p.company_id = @CompanyId
                  AND p.deleted_at IS NULL";

            var parameters = new DynamicParameters();
            parameters.Add("CompanyId", companyId);

            if (filters?.CategoryId.HasValue == true)
            {
                sql += " AND p.category_id = @CategoryId";
                parameters.Add("CategoryId", filters.CategoryId.Value);
            }

            if (filters?.IsActive.HasValue == true)
            {
                sql += " AND p.is_active = @IsActive";
                parameters.Add("IsActive", filters.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(filters?.Search))
            {
                sql += " AND (p.name ILIKE @Search OR p.sku ILIKE @Search)";
                parameters.Add("Search", $"%{filters.Search}%");
            }

            sql += " ORDER BY p.name ASC";

            return await connection.QueryAsync<Product>(sql, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo productos");
            throw;
        }
    }

    public async Task<Product?> GetProductByIdAsync(long productId, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT 
                    p.id AS Id,
                    p.company_id AS CompanyId,
                    p.name AS Name,
                    p.sku AS Sku,
                    p.barcode AS Barcode,
                    p.description AS Description,
                    p.category_id AS CategoryId,
                    p.unit_id AS UnitId,
                    p.cost_price AS CostPrice,
                    p.sale_price AS SalePrice,
                    p.margin_percentage AS MarginPercentage,
                    p.min_stock AS MinStock,
                    p.max_stock AS MaxStock,
                    p.reorder_point AS ReorderPoint,
                    p.is_perishable AS IsPerishable,
                    p.shelf_life_days AS ShelfLifeDays,
                    p.product_type AS ProductType,
                    p.track_stock AS TrackStock,
                    p.is_for_sale AS IsForSale,
                    p.is_active AS IsActive,
                    p.created_by AS CreatedBy,
                    p.created_at AS CreatedAt,
                    p.updated_at AS UpdatedAt,
                    c.name AS CategoryName,
                    u.name AS UnitName,
                    u.abbreviation AS UnitAbbreviation
                FROM inventory.products p
                INNER JOIN inventory.categories c ON p.category_id = c.id AND c.company_id = p.company_id
                INNER JOIN inventory.units u ON p.unit_id = u.id AND u.company_id = p.company_id
                WHERE p.id = @ProductId 
                  AND p.company_id = @CompanyId
                  AND p.deleted_at IS NULL";

            return await connection.QueryFirstOrDefaultAsync<Product>(sql, new { ProductId = productId, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo producto");
            throw;
        }
    }

    public async Task<Product> CreateProductAsync(Product product)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                INSERT INTO inventory.products (
                    company_id, name, sku, barcode, description,
                    category_id, unit_id, cost_price, sale_price,
                    min_stock, max_stock, reorder_point, is_perishable,
                    shelf_life_days, product_type, track_stock, is_for_sale,
                    created_by
                ) VALUES (
                    @CompanyId, @Name, @Sku, @Barcode, @Description,
                    @CategoryId, @UnitId, @CostPrice, @SalePrice,
                    @MinStock, @MaxStock, @ReorderPoint, @IsPerishable,
                    @ShelfLifeDays, @ProductType, @TrackStock, @IsForSale,
                    @CreatedBy
                )
                RETURNING id AS Id, company_id AS CompanyId,
                       name AS Name, sku AS Sku,
                       barcode AS Barcode, description AS Description,
                       category_id AS CategoryId, unit_id AS UnitId,
                       cost_price AS CostPrice, sale_price AS SalePrice,
                       margin_percentage AS MarginPercentage,
                       min_stock AS MinStock, max_stock AS MaxStock,
                       reorder_point AS ReorderPoint,
                       is_perishable AS IsPerishable,
                       shelf_life_days AS ShelfLifeDays,
                       product_type AS ProductType,
                       track_stock AS TrackStock,
                       is_for_sale AS IsForSale,
                       is_active AS IsActive,
                       created_by AS CreatedBy,
                       created_at AS CreatedAt";

            return await connection.QuerySingleAsync<Product>(sql, product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando producto");
            throw;
        }
    }

    public async Task<IEnumerable<Stock>> GetStockByBranchAsync(long branchId, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                WITH committed AS (
                    SELECT
                        o.branch_id,
                        oi.product_id,
                        SUM(oi.quantity) AS committed_quantity
                    FROM sales.orders o
                    INNER JOIN sales.tables t ON o.table_id = t.id AND t.company_id = o.company_id
                    INNER JOIN sales.order_items oi ON oi.order_id = o.id AND oi.company_id = o.company_id
                    WHERE o.company_id = @CompanyId
                      AND o.branch_id = @BranchId
                      AND o.status = 'pending'
                      AND t.status = 'open'
                      AND t.deleted_at IS NULL
                    GROUP BY o.branch_id, oi.product_id
                )
                SELECT 
                    s.id AS Id,
                    s.company_id AS CompanyId,
                    s.branch_id AS BranchId,
                    s.product_id AS ProductId,
                    p.name AS ProductName,
                    p.sku AS Sku,
                    c.name AS Category,
                    p.min_stock AS MinStock,
                    s.quantity AS Quantity,
                    COALESCE(committed.committed_quantity, 0) AS ReservedQuantity,
                    CASE
                        WHEN s.quantity - COALESCE(committed.committed_quantity, 0) < 0 THEN 0
                        ELSE s.quantity - COALESCE(committed.committed_quantity, 0)
                    END AS AvailableQuantity,
                    s.location AS Location,
                    u.abbreviation AS Unit,
                    p.cost_price AS CostPrice,
                    p.sale_price AS SalePrice,
                    p.image_url AS ImageUrl,
                    p.product_type AS ProductType,
                    p.track_stock AS TrackStock,
                    p.is_perishable AS IsPerishable,
                    CASE 
                        WHEN p.track_stock = FALSE THEN 'ok'
                        WHEN s.quantity - COALESCE(committed.committed_quantity, 0) <= 0 THEN 'out'
                        WHEN p.min_stock > 0 AND s.quantity - COALESCE(committed.committed_quantity, 0) <= p.min_stock THEN 'low'
                        WHEN p.reorder_point > 0 AND s.quantity - COALESCE(committed.committed_quantity, 0) <= p.reorder_point THEN 'reorder'
                        ELSE 'ok'
                    END AS StockStatus
                FROM inventory.stock s
                INNER JOIN inventory.products p ON s.product_id = p.id AND p.company_id = s.company_id
                LEFT JOIN inventory.categories c ON p.category_id = c.id AND c.company_id = p.company_id
                LEFT JOIN inventory.units u ON p.unit_id = u.id AND u.company_id = p.company_id
                LEFT JOIN committed ON committed.branch_id = s.branch_id AND committed.product_id = s.product_id
                WHERE s.branch_id = @BranchId
                  AND s.company_id = @CompanyId
                  AND p.deleted_at IS NULL
                ORDER BY p.name";

            return await connection.QueryAsync<Stock>(sql, new { BranchId = branchId, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo stock");
            throw;
        }
    }

    public async Task<Stock> UpdateStockAsync(long branchId, long productId, decimal quantity, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var checkSql = @"
                SELECT id FROM inventory.stock
                WHERE branch_id = @BranchId AND product_id = @ProductId AND company_id = @CompanyId";

            var existing = await connection.QueryFirstOrDefaultAsync<long?>(checkSql, new { BranchId = branchId, ProductId = productId, CompanyId = companyId });

            if (existing is null)
            {
                const string insertSql = @"
                    INSERT INTO inventory.stock (company_id, branch_id, product_id, quantity)
                    VALUES (@CompanyId, @BranchId, @ProductId, @Quantity)
                    RETURNING id AS Id, company_id AS CompanyId,
                           branch_id AS BranchId, product_id AS ProductId,
                           quantity AS Quantity";

                return await connection.QuerySingleAsync<Stock>(insertSql,
                    new { CompanyId = companyId, BranchId = branchId, ProductId = productId, Quantity = quantity });
            }
            else
            {
                const string updateSql = @"
                    UPDATE inventory.stock
                    SET quantity = quantity + @Quantity,
                        updated_at = NOW()
                    WHERE branch_id = @BranchId AND product_id = @ProductId AND company_id = @CompanyId
                    RETURNING id AS Id, company_id AS CompanyId,
                           branch_id AS BranchId, product_id AS ProductId,
                           quantity AS Quantity";

                return await connection.QuerySingleAsync<Stock>(updateSql,
                    new { BranchId = branchId, ProductId = productId, Quantity = quantity, CompanyId = companyId });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando stock");
            throw;
        }
    }

    public async Task<Movement> CreateMovementAsync(Movement movement)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                INSERT INTO inventory.movements (
                    company_id, branch_id, product_id, movement_type,
                    quantity, unit_cost, notes, created_by_ai,
                    ai_confidence, ai_metadata, created_by
                ) VALUES (
                    @CompanyId, @BranchId, @ProductId, @MovementType,
                    @Quantity, @UnitCost, @Notes, @CreatedByAi,
                    @AiConfidence, CAST(@AiMetadata AS JSONB), @CreatedBy
                )
                RETURNING id AS Id, company_id AS CompanyId,
                       branch_id AS BranchId, product_id AS ProductId,
                       movement_type AS MovementType,
                       quantity AS Quantity, unit_cost AS UnitCost,
                       notes AS Notes,
                       created_by_ai AS CreatedByAi,
                       ai_confidence AS AiConfidence,
                       ai_metadata::TEXT AS AiMetadata,
                       created_by AS CreatedBy,
                       created_at AS CreatedAt";

            return await connection.QuerySingleAsync<Movement>(sql, movement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando movimiento");
            throw;
        }
    }

    public async Task<AiInteraction> SaveAiInteractionAsync(AiInteraction interaction)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                INSERT INTO inventory.ai_interactions (
                    company_id, branch_id, user_id, session_id,
                    interaction_type, user_input, ai_response, ai_action,
                    processed_data, action_status, confidence_score,
                    ai_model, tokens_used
                ) VALUES (
                    @CompanyId, @BranchId, @UserId, @SessionId,
                    @InteractionType, @UserInput, @AiResponse, @AiAction,
                    CAST(@ProcessedData AS JSONB), @ActionStatus, @ConfidenceScore,
                    @AiModel, @TokensUsed
                )
                RETURNING id AS Id, company_id AS CompanyId,
                       branch_id AS BranchId, user_id AS UserId,
                       session_id AS SessionId,
                       interaction_type AS InteractionType,
                       user_input AS UserInput,
                       ai_response AS AiResponse,
                       ai_action AS AiAction,
                       processed_data::TEXT AS ProcessedData,
                       action_status AS ActionStatus,
                       confidence_score AS ConfidenceScore,
                       ai_model AS AiModel,
                       tokens_used AS TokensUsed,
                       created_at AS CreatedAt";

            return await connection.QuerySingleAsync<AiInteraction>(sql, interaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error guardando interacción IA");
            throw;
        }
    }

    public async Task<AiInteraction?> GetAiInteractionByIdAsync(long id, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT 
                    id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                    user_id AS UserId, session_id AS SessionId,
                    interaction_type AS InteractionType,
                    user_input AS UserInput, ai_response AS AiResponse,
                    ai_action AS AiAction, processed_data AS ProcessedData,
                    action_status AS ActionStatus,
                    confidence_score AS ConfidenceScore,
                    ai_model AS AiModel, tokens_used AS TokensUsed,
                    confirmed_by_user AS ConfirmedByUser,
                    confirmed_at AS ConfirmedAt,
                    created_at AS CreatedAt
                FROM inventory.ai_interactions
                WHERE id = @Id AND company_id = @CompanyId";

            return await connection.QueryFirstOrDefaultAsync<AiInteraction>(sql, new { Id = id, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo interacción IA");
            throw;
        }
    }

    public async Task<IEnumerable<AiInteraction>> GetAiInteractionsBySessionAsync(string sessionId, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT
                    id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                    user_id AS UserId, session_id AS SessionId,
                    interaction_type AS InteractionType,
                    user_input AS UserInput, ai_response AS AiResponse,
                    ai_action AS AiAction, processed_data::TEXT AS ProcessedData,
                    action_status AS ActionStatus,
                    confidence_score AS ConfidenceScore,
                    created_at AS CreatedAt
                FROM inventory.ai_interactions
                WHERE session_id = @SessionId AND company_id = @CompanyId
                ORDER BY created_at ASC
                LIMIT 10";

            return await connection.QueryAsync<AiInteraction>(sql, new { SessionId = sessionId, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo historial de sesión IA");
            throw;
        }
    }

    public async Task UpdateAiInteractionStatusAsync(long id, string status, bool confirmedByUser, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                UPDATE inventory.ai_interactions
                SET action_status = @Status,
                    confirmed_by_user = @ConfirmedByUser,
                    confirmed_at = NOW()
                WHERE id = @Id AND company_id = @CompanyId";

            await connection.ExecuteAsync(sql, new { Id = id, Status = status, ConfirmedByUser = confirmedByUser, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando estado de interacción IA");
            throw;
        }
    }

    public async Task<IEnumerable<Alert>> GetActiveAlertsAsync(long companyId, long? branchId = null)
    {
        try
        {
            IEnumerable<Stock> stock;

            if (branchId.HasValue)
            {
                stock = await GetStockByBranchAsync(branchId.Value, companyId);
            }
            else
            {
                using var connection = await _connectionFactory.CreateConnectionAsync();
                const string branchSql = @"
                    SELECT DISTINCT branch_id
                    FROM inventory.stock
                    WHERE company_id = @CompanyId";

                var branchIds = (await connection.QueryAsync<long>(branchSql, new { CompanyId = companyId })).ToList();
                var allStock = new List<Stock>();
                foreach (var currentBranchId in branchIds)
                {
                    allStock.AddRange(await GetStockByBranchAsync(currentBranchId, companyId));
                }

                stock = allStock;
            }

            return stock
                .Where(item => item.StockStatus is "low" or "out" or "reorder")
                .OrderBy(item => item.StockStatus == "out" ? 0 : item.StockStatus == "low" ? 1 : 2)
                .ThenBy(item => item.AvailableQuantity)
                .Select(item => new Alert
                {
                    Id = item.Id,
                    CompanyId = companyId,
                    BranchId = item.BranchId,
                    ProductId = item.ProductId,
                    AlertType = item.StockStatus == "out" ? "out_of_stock" : item.StockStatus == "low" ? "low_stock" : "reorder",
                    Severity = item.StockStatus == "out" ? "critical" : item.StockStatus == "low" ? "high" : "medium",
                    Status = "active",
                    CreatedAt = DateTime.UtcNow,
                    ProductName = item.ProductName,
                    Sku = item.Sku,
                    Message = item.StockStatus == "out"
                        ? $"Sin stock disponible. Fisico: {item.Quantity:N2}, comprometido: {item.ReservedQuantity:N2}."
                        : $"Stock disponible en {item.AvailableQuantity:N2}. Fisico: {item.Quantity:N2}, comprometido: {item.ReservedQuantity:N2}, minimo: {item.MinStock ?? 0:N2}."
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo alertas");
            throw;
        }
    }

    public async Task<IEnumerable<Product>> FindProductsByNameAsync(long companyId, string name)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT id AS Id, name AS Name
                FROM inventory.products
                WHERE company_id = @CompanyId 
                  AND name ILIKE '%' || @Name || '%'
                  AND deleted_at IS NULL";

            return await connection.QueryAsync<Product>(sql, new { CompanyId = companyId, Name = name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error buscando productos por nombre");
            throw;
        }
    }

    public async Task<IEnumerable<ProfitReportRow>> GetProductProfitsAsync(
        long companyId, long branchId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var joinCondition = @"m.movement_type = 'sale'
                    AND m.company_id = @CompanyId
                    AND m.branch_id = @BranchId";

            var parameters = new DynamicParameters();
            parameters.Add("CompanyId", companyId);
            parameters.Add("BranchId", branchId);

            if (startDate.HasValue)
            {
                joinCondition += " AND m.created_at >= @StartDate";
                parameters.Add("StartDate", startDate.Value);
            }

            if (endDate.HasValue)
            {
                joinCondition += " AND m.created_at <= @EndDate";
                parameters.Add("EndDate", endDate.Value);
            }

            var sql = $@"
                SELECT 
                    p.id AS Id,
                    p.name AS Name,
                    p.sku AS Sku,
                    p.cost_price AS CostPrice,
                    p.sale_price AS SalePrice,
                    p.margin_percentage AS MarginPercentage,
                    COUNT(m.id) AS TotalSales,
                    COALESCE(SUM(ABS(m.quantity)), 0) AS TotalQuantitySold,
                    COALESCE(SUM(ABS(m.quantity) * p.cost_price), 0) AS TotalCost,
                    COALESCE(SUM(ABS(m.quantity) * p.sale_price), 0) AS TotalRevenue,
                    COALESCE(SUM(ABS(m.quantity) * (p.sale_price - p.cost_price)), 0) AS TotalProfit
                FROM inventory.products p
                LEFT JOIN inventory.movements m ON p.id = m.product_id
                    AND {joinCondition}
                WHERE p.company_id = @CompanyId
                  AND p.deleted_at IS NULL
                GROUP BY p.id, p.name, p.sku, p.cost_price, p.sale_price, p.margin_percentage
                ORDER BY TotalProfit DESC";

            return await connection.QueryAsync<ProfitReportRow>(sql, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculando ganancias");
            throw;
        }
    }

    public async Task<IEnumerable<CategoryInfo>> GetCategoriesAsync(long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT id AS Id, name AS Name
                FROM inventory.categories
                WHERE company_id = @CompanyId AND is_active = TRUE
                ORDER BY name";

            return await connection.QueryAsync<CategoryInfo>(sql, new { CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo categorías");
            throw;
        }
    }

    public async Task<IEnumerable<UnitInfo>> GetUnitsAsync(long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT id AS Id, name AS Name, abbreviation AS Abbreviation
                FROM inventory.units
                WHERE company_id = @CompanyId AND is_active = TRUE
                ORDER BY name";

            return await connection.QueryAsync<UnitInfo>(sql, new { CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo unidades");
            throw;
        }
    }

    public async Task<Stock> CreateStockEntryAsync(long branchId, long productId, decimal quantity, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                INSERT INTO inventory.stock (company_id, branch_id, product_id, quantity)
                VALUES (@CompanyId, @BranchId, @ProductId, @Quantity)
                RETURNING id AS Id, company_id AS CompanyId,
                       branch_id AS BranchId, product_id AS ProductId,
                       quantity AS Quantity";

            return await connection.QuerySingleAsync<Stock>(sql, new
            {
                CompanyId = companyId,
                BranchId = branchId,
                ProductId = productId,
                Quantity = quantity
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando entrada de stock");
            throw;
        }
    }

    public async Task UpdateProductCostAndPriceAsync(long productId, long companyId, decimal newCostPrice, decimal? newSalePrice = null)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var sql = newSalePrice.HasValue
                ? @"UPDATE inventory.products 
                    SET cost_price = @CostPrice, sale_price = @SalePrice, updated_at = NOW()
                    WHERE id = @ProductId AND company_id = @CompanyId"
                : @"UPDATE inventory.products 
                    SET cost_price = @CostPrice, updated_at = NOW()
                    WHERE id = @ProductId AND company_id = @CompanyId";

            await connection.ExecuteAsync(sql, new
            {
                ProductId = productId,
                CompanyId = companyId,
                CostPrice = newCostPrice,
                SalePrice = newSalePrice
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando costo de producto");
            throw;
        }
    }

    public async Task<Stock?> GetStockByProductAsync(long branchId, long productId, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                WITH committed AS (
                    SELECT
                        o.branch_id,
                        oi.product_id,
                        SUM(oi.quantity) AS committed_quantity
                    FROM sales.orders o
                    INNER JOIN sales.tables t ON o.table_id = t.id AND t.company_id = o.company_id
                    INNER JOIN sales.order_items oi ON oi.order_id = o.id AND oi.company_id = o.company_id
                    WHERE o.company_id = @CompanyId
                      AND o.branch_id = @BranchId
                      AND o.status = 'pending'
                      AND t.status = 'open'
                      AND t.deleted_at IS NULL
                    GROUP BY o.branch_id, oi.product_id
                )
                SELECT s.id AS Id, s.company_id AS CompanyId, s.branch_id AS BranchId,
                       s.product_id AS ProductId, s.quantity AS Quantity,
                       p.name AS ProductName,
                       COALESCE(committed.committed_quantity, 0) AS ReservedQuantity,
                       CASE
                           WHEN s.quantity - COALESCE(committed.committed_quantity, 0) < 0 THEN 0
                           ELSE s.quantity - COALESCE(committed.committed_quantity, 0)
                       END AS AvailableQuantity
                FROM inventory.stock s
                INNER JOIN inventory.products p ON s.product_id = p.id AND p.company_id = s.company_id
                LEFT JOIN committed ON committed.branch_id = s.branch_id AND committed.product_id = s.product_id
                WHERE s.branch_id = @BranchId AND s.product_id = @ProductId AND s.company_id = @CompanyId";

            return await connection.QueryFirstOrDefaultAsync<Stock>(sql, new
            {
                BranchId = branchId,
                ProductId = productId,
                CompanyId = companyId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo stock por producto");
            throw;
        }
    }

    public async Task<Product> UpdateProductAsync(Product product)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                UPDATE inventory.products
                SET name = @Name,
                    sku = @Sku,
                    barcode = @Barcode,
                    description = @Description,
                    category_id = @CategoryId,
                    unit_id = @UnitId,
                    cost_price = @CostPrice,
                    sale_price = @SalePrice,
                    min_stock = @MinStock,
                    max_stock = @MaxStock,
                    reorder_point = @ReorderPoint,
                    is_perishable = @IsPerishable,
                    shelf_life_days = @ShelfLifeDays,
                    product_type = @ProductType,
                    track_stock = @TrackStock,
                    is_for_sale = @IsForSale,
                    updated_at = NOW()
                WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL;

                SELECT id AS Id, company_id AS CompanyId, name AS Name, sku AS Sku,
                       barcode AS Barcode, description AS Description,
                       category_id AS CategoryId, unit_id AS UnitId,
                       cost_price AS CostPrice, sale_price AS SalePrice,
                       margin_percentage AS MarginPercentage,
                       min_stock AS MinStock, max_stock AS MaxStock,
                       reorder_point AS ReorderPoint,
                       is_perishable AS IsPerishable, shelf_life_days AS ShelfLifeDays,
                       product_type AS ProductType, track_stock AS TrackStock,
                       is_for_sale AS IsForSale, is_active AS IsActive,
                       image_url AS ImageUrl,
                       created_by AS CreatedBy, created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM inventory.products WHERE id = @Id AND company_id = @CompanyId;";

            var updated = await connection.QueryFirstOrDefaultAsync<Product>(sql, new
            {
                product.Id,
                product.CompanyId,
                product.Name,
                product.Sku,
                product.Barcode,
                product.Description,
                product.CategoryId,
                product.UnitId,
                product.CostPrice,
                product.SalePrice,
                product.MinStock,
                product.MaxStock,
                product.ReorderPoint,
                product.IsPerishable,
                product.ShelfLifeDays,
                product.ProductType,
                product.TrackStock,
                product.IsForSale
            });

            return updated ?? throw new Exception("Producto no encontrado para actualizar");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando producto {ProductId}", product.Id);
            throw;
        }
    }

    public async Task UpdateProductImageAsync(long productId, long companyId, string imageUrl)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                UPDATE inventory.products
                SET image_url = @ImageUrl, updated_at = NOW()
                WHERE id = @ProductId AND company_id = @CompanyId AND deleted_at IS NULL";

            await connection.ExecuteAsync(sql, new { ProductId = productId, CompanyId = companyId, ImageUrl = imageUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando imagen del producto {ProductId}", productId);
            throw;
        }
    }

    public async Task SoftDeleteProductAsync(long productId, long companyId, long userId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                UPDATE inventory.products
                SET deleted_at = NOW(),
                    is_active = FALSE,
                    updated_at = NOW()
                WHERE id = @ProductId AND company_id = @CompanyId AND deleted_at IS NULL";

            var rows = await connection.ExecuteAsync(sql, new { ProductId = productId, CompanyId = companyId });

            if (rows == 0)
                throw new Exception("Producto no encontrado o ya eliminado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando producto {ProductId}", productId);
            throw;
        }
    }
}
