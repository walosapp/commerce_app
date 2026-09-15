using Npgsql;

namespace Walos.Tests.Integration;

/// <summary>
/// Keeps V1 integration data isolated without relying on the reserved-ID
/// fixture used by the pending POS-Deli work.
/// </summary>
public abstract class V1IntegrationTestBase : IntegrationTestBase
{
    private readonly HashSet<long> _companyIds = [];

    protected new async Task<long> SeedCompanyAsync(string name = "Test Company")
    {
        using var connection = await ConnectionFactory.CreateConnectionAsync();
        using var command = new NpgsqlCommand(@"
            INSERT INTO core.companies
                (name, legal_name, tax_id, email, phone, is_active, created_by)
            VALUES
                (@name, @name, @taxId, @email, '123456', TRUE, 1)
            RETURNING id", (NpgsqlConnection)connection);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("taxId", $"V1-TEST-{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("email", $"company-{Guid.NewGuid():N}@test.local");

        var companyId = (long)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Failed to seed V1 test company"));
        _companyIds.Add(companyId);
        return companyId;
    }

    protected new async Task<long> SeedUserAsync(
        long companyId,
        long? branchId,
        string? email = null,
        string? password = null)
    {
        email ??= $"user-{Guid.NewGuid():N}@test.local";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(
            password ?? $"V1-test-{Guid.NewGuid():N}");
        using var connection = await ConnectionFactory.CreateConnectionAsync();

        using var roleCommand = new NpgsqlCommand(@"
            INSERT INTO core.roles (company_id, code, name, is_active)
            VALUES (@companyId, 'manager', 'Manager', TRUE)
            ON CONFLICT (company_id, code) DO UPDATE SET name = 'Manager'
            RETURNING id", (NpgsqlConnection)connection);
        roleCommand.Parameters.AddWithValue("companyId", companyId);
        var roleId = (long)(await roleCommand.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Failed to seed V1 test role"));

        using var command = new NpgsqlCommand(@"
            INSERT INTO core.users
                (company_id, branch_id, role_id, first_name, last_name, email,
                 password_hash, is_active, email_verified, created_by)
            VALUES
                (@companyId, @branchId, @roleId, 'Test', 'User', @email,
                 @passwordHash, TRUE, TRUE, 1)
            RETURNING id", (NpgsqlConnection)connection);
        command.Parameters.AddWithValue("companyId", companyId);
        command.Parameters.AddWithValue("branchId", branchId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("roleId", roleId);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("passwordHash", passwordHash);

        return (long)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Failed to seed V1 test user"));
    }

    public override void Dispose()
    {
        CleanupV1CompaniesAsync().GetAwaiter().GetResult();
    }

    private async Task CleanupV1CompaniesAsync()
    {
        if (_companyIds.Count == 0)
            return;

        using var connection = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(@"
            DELETE FROM core.ai_messages
            WHERE session_id IN (
                SELECT id FROM core.ai_sessions WHERE company_id = ANY(@companyIds)
            );
            DELETE FROM platform.billing_invoice_items
            WHERE invoice_id IN (
                SELECT id FROM platform.billing_invoices WHERE company_id = ANY(@companyIds)
            );
            DELETE FROM delivery.status_history
            WHERE order_id IN (
                SELECT id FROM delivery.orders WHERE company_id = ANY(@companyIds)
            );
            DELETE FROM delivery.order_items WHERE company_id = ANY(@companyIds);
            DELETE FROM suppliers.purchase_order_items
            WHERE order_id IN (
                SELECT id FROM suppliers.purchase_orders WHERE company_id = ANY(@companyIds)
            );
            DELETE FROM suppliers.supplier_products
            WHERE supplier_id IN (
                SELECT id FROM suppliers.suppliers WHERE company_id = ANY(@companyIds)
            ) OR product_id IN (
                SELECT id FROM inventory.products WHERE company_id = ANY(@companyIds)
            );
            DELETE FROM sales.refund_items
            WHERE refund_id IN (
                SELECT id FROM sales.refunds WHERE company_id = ANY(@companyIds)
            ) OR order_item_id IN (
                SELECT id FROM sales.order_items WHERE company_id = ANY(@companyIds)
            );
            DELETE FROM sales.credit_payments WHERE company_id = ANY(@companyIds);
            DELETE FROM sales.refunds WHERE company_id = ANY(@companyIds);
            DELETE FROM sales.credits WHERE company_id = ANY(@companyIds);
            DELETE FROM sales.order_payments WHERE company_id = ANY(@companyIds);
            DELETE FROM sales.cash_movements WHERE company_id = ANY(@companyIds);
            DELETE FROM sales.order_items WHERE company_id = ANY(@companyIds);
            DELETE FROM sales.orders WHERE company_id = ANY(@companyIds);
            DELETE FROM sales.cash_registers WHERE company_id = ANY(@companyIds);
            DELETE FROM sales.tables WHERE company_id = ANY(@companyIds);
            DELETE FROM inventory.recipes WHERE company_id = ANY(@companyIds);
            DELETE FROM inventory.movements WHERE company_id = ANY(@companyIds);
            DELETE FROM inventory.alerts WHERE company_id = ANY(@companyIds);
            DELETE FROM inventory.ai_interactions WHERE company_id = ANY(@companyIds);
            DELETE FROM inventory.stock WHERE company_id = ANY(@companyIds);
            DELETE FROM suppliers.purchase_orders WHERE company_id = ANY(@companyIds);
            DELETE FROM suppliers.suppliers WHERE company_id = ANY(@companyIds);
            DELETE FROM delivery.orders WHERE company_id = ANY(@companyIds);
            DELETE FROM finance.entries WHERE company_id = ANY(@companyIds);
            DELETE FROM finance.categories WHERE company_id = ANY(@companyIds);
            DELETE FROM inventory.products WHERE company_id = ANY(@companyIds);
            DELETE FROM inventory.categories WHERE company_id = ANY(@companyIds);
            DELETE FROM inventory.units WHERE company_id = ANY(@companyIds);
            DELETE FROM platform.billing_invoices WHERE company_id = ANY(@companyIds);
            DELETE FROM platform.company_subscriptions WHERE company_id = ANY(@companyIds);
            DELETE FROM platform.payment_methods WHERE company_id = ANY(@companyIds);
            DELETE FROM core.ai_sessions WHERE company_id = ANY(@companyIds);
            DELETE FROM core.users WHERE company_id = ANY(@companyIds);
            DELETE FROM core.branches WHERE company_id = ANY(@companyIds);
            DELETE FROM core.roles WHERE company_id = ANY(@companyIds);
            DELETE FROM core.companies WHERE id = ANY(@companyIds);",
            connection,
            transaction);
        command.Parameters.AddWithValue("companyIds", _companyIds.ToArray());
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }
}
