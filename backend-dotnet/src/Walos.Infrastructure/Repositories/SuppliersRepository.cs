using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Suppliers;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Repositories;

public class SuppliersRepository : ISuppliersRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<SuppliersRepository> _logger;

    public SuppliersRepository(IDbConnectionFactory db, ILogger<SuppliersRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IEnumerable<Supplier>> GetAllAsync(long companyId, long? branchId)
    {
        using var conn = await _db.CreateConnectionAsync();
        var where = "WHERE s.company_id = @CompanyId AND s.deleted_at IS NULL AND s.is_active = TRUE";
        var p = new DynamicParameters();
        p.Add("CompanyId", companyId);

        if (branchId.HasValue)
        {
            where += " AND (s.branch_id = @BranchId OR s.branch_id IS NULL)";
            p.Add("BranchId", branchId.Value);
        }

        var sql = $@"
            SELECT
                s.id AS Id, s.company_id AS CompanyId, s.branch_id AS BranchId,
                s.name AS Name, s.contact_name AS ContactName, s.phone AS Phone,
                s.email AS Email, s.address AS Address, s.notes AS Notes,
                s.is_active AS IsActive, s.created_by AS CreatedBy,
                s.created_at AS CreatedAt, s.updated_at AS UpdatedAt,
                COUNT(sp.id) AS ProductCount
            FROM suppliers.suppliers s
            LEFT JOIN suppliers.supplier_products sp ON sp.supplier_id = s.id
            {where}
            GROUP BY s.id, s.company_id, s.branch_id, s.name, s.contact_name,
                     s.phone, s.email, s.address, s.notes, s.is_active,
                     s.created_by, s.created_at, s.updated_at
            ORDER BY s.name ASC";

        return await conn.QueryAsync<Supplier>(sql, p);
    }

    public async Task<Supplier?> GetByIdAsync(long supplierId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT
                s.id AS Id, s.company_id AS CompanyId, s.branch_id AS BranchId,
                s.name AS Name, s.contact_name AS ContactName, s.phone AS Phone,
                s.email AS Email, s.address AS Address, s.notes AS Notes,
                s.is_active AS IsActive, s.created_by AS CreatedBy,
                s.created_at AS CreatedAt, s.updated_at AS UpdatedAt
            FROM suppliers.suppliers s
            WHERE s.id = @SupplierId AND s.company_id = @CompanyId AND s.deleted_at IS NULL";

        var supplier = await conn.QueryFirstOrDefaultAsync<Supplier>(sql, new { SupplierId = supplierId, CompanyId = companyId });
        if (supplier is null) return null;

        supplier.Products = (await GetSupplierProductsAsync(supplierId)).ToList();
        return supplier;
    }

    public async Task<Supplier> CreateAsync(Supplier supplier)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            INSERT INTO suppliers.suppliers (company_id, branch_id, name, contact_name, phone, email, address, notes, created_by)
            VALUES (@CompanyId, @BranchId, @Name, @ContactName, @Phone, @Email, @Address, @Notes, @CreatedBy)
            RETURNING id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                      name AS Name, contact_name AS ContactName, phone AS Phone,
                      email AS Email, address AS Address, notes AS Notes,
                      is_active AS IsActive, created_by AS CreatedBy,
                      created_at AS CreatedAt, updated_at AS UpdatedAt";
        return await conn.QuerySingleAsync<Supplier>(sql, supplier);
    }

    public async Task<Supplier?> UpdateAsync(Supplier supplier)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE suppliers.suppliers
            SET name = @Name, contact_name = @ContactName, phone = @Phone,
                email = @Email, address = @Address, notes = @Notes, updated_at = NOW()
            WHERE id = @Id AND company_id = @CompanyId AND deleted_at IS NULL
            RETURNING id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                      name AS Name, contact_name AS ContactName, phone AS Phone,
                      email AS Email, address AS Address, notes AS Notes,
                      is_active AS IsActive, created_by AS CreatedBy,
                      created_at AS CreatedAt, updated_at AS UpdatedAt";
        return await conn.QueryFirstOrDefaultAsync<Supplier>(sql, supplier);
    }

    public async Task<bool> SoftDeleteAsync(long supplierId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            UPDATE suppliers.suppliers
            SET deleted_at = NOW(), is_active = FALSE, updated_at = NOW()
            WHERE id = @SupplierId AND company_id = @CompanyId AND deleted_at IS NULL";
        return await conn.ExecuteAsync(sql, new { SupplierId = supplierId, CompanyId = companyId }) > 0;
    }

    public async Task<IEnumerable<SupplierProduct>> GetSupplierProductsAsync(long supplierId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT sp.id AS Id, sp.supplier_id AS SupplierId, sp.product_id AS ProductId,
                   sp.supplier_sku AS SupplierSku, sp.unit_cost AS UnitCost,
                   sp.lead_time_days AS LeadTimeDays, sp.notes AS Notes,
                   sp.created_at AS CreatedAt, p.name AS ProductName
            FROM suppliers.supplier_products sp
            JOIN inventory.products p ON p.id = sp.product_id
            WHERE sp.supplier_id = @SupplierId AND p.deleted_at IS NULL
            ORDER BY p.name ASC";
        return await conn.QueryAsync<SupplierProduct>(sql, new { SupplierId = supplierId });
    }

    public async Task<SupplierProduct> AddSupplierProductAsync(SupplierProduct sp)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            INSERT INTO suppliers.supplier_products (supplier_id, product_id, supplier_sku, unit_cost, lead_time_days, notes)
            VALUES (@SupplierId, @ProductId, @SupplierSku, @UnitCost, @LeadTimeDays, @Notes)
            ON CONFLICT (supplier_id, product_id) DO UPDATE
                SET supplier_sku = EXCLUDED.supplier_sku,
                    unit_cost = EXCLUDED.unit_cost,
                    lead_time_days = EXCLUDED.lead_time_days,
                    notes = EXCLUDED.notes
            RETURNING id AS Id, supplier_id AS SupplierId, product_id AS ProductId,
                      supplier_sku AS SupplierSku, unit_cost AS UnitCost,
                      lead_time_days AS LeadTimeDays, notes AS Notes, created_at AS CreatedAt";
        return await conn.QuerySingleAsync<SupplierProduct>(sql, sp);
    }

    public async Task<bool> RemoveSupplierProductAsync(long supplierId, long productId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = "DELETE FROM suppliers.supplier_products WHERE supplier_id = @SupplierId AND product_id = @ProductId";
        return await conn.ExecuteAsync(sql, new { SupplierId = supplierId, ProductId = productId }) > 0;
    }

    public async Task<IEnumerable<Supplier>> GetSuppliersForProductAsync(long productId, long companyId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT s.id AS Id, s.company_id AS CompanyId, s.name AS Name,
                   s.contact_name AS ContactName, s.phone AS Phone, s.email AS Email,
                   s.is_active AS IsActive, s.created_at AS CreatedAt
            FROM suppliers.suppliers s
            JOIN suppliers.supplier_products sp ON sp.supplier_id = s.id
            WHERE sp.product_id = @ProductId AND s.company_id = @CompanyId
              AND s.deleted_at IS NULL AND s.is_active = TRUE";
        return await conn.QueryAsync<Supplier>(sql, new { ProductId = productId, CompanyId = companyId });
    }

    public async Task<IEnumerable<SuggestedOrderItem>> GetLowStockItemsForSupplierAsync(long supplierId, long companyId, long branchId)
    {
        using var conn = await _db.CreateConnectionAsync();
        const string sql = @"
            SELECT
                p.id AS ProductId,
                p.name AS ProductName,
                COALESCE(st.quantity, 0) AS CurrentStock,
                COALESCE(p.reorder_point, 0) AS ReorderPoint,
                sp.unit_cost AS UnitCost
            FROM suppliers.supplier_products sp
            JOIN inventory.products p ON p.id = sp.product_id
            LEFT JOIN inventory.stock st ON st.product_id = p.id AND st.branch_id = @BranchId
            WHERE sp.supplier_id = @SupplierId
              AND p.company_id = @CompanyId
              AND p.deleted_at IS NULL
              AND COALESCE(st.quantity, 0) <= COALESCE(p.reorder_point, 0)
            ORDER BY COALESCE(st.quantity, 0) ASC";

        var rows = await conn.QueryAsync<dynamic>(sql, new { SupplierId = supplierId, CompanyId = companyId, BranchId = branchId });

        return rows.Select(r => {
            var current = (decimal)(r.currentstock ?? 0);
            var reorder = (decimal)(r.reorderpoint ?? 0);
            var suggested = Math.Max(reorder * 2 - current, 1);
            var unitCost = r.unitcost is null ? (decimal?)null : (decimal)r.unitcost;
            return new SuggestedOrderItem
            {
                ProductId   = (long)r.productid,
                ProductName = (string)r.productname,
                CurrentStock = current,
                ReorderPoint = reorder,
                SuggestedQty = Math.Ceiling(suggested),
                UnitCost = unitCost,
                EstimatedCost = unitCost.HasValue ? unitCost.Value * (decimal)Math.Ceiling((double)suggested) : null,
            };
        });
    }
}
