using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace Walos.Tests.Integration;

internal static class IntegrationTestDatabaseSafety
{
    public static void RequireSafeTarget(string connectionString, string? allowWrites)
    {
        if (!string.Equals(allowWrites, "true", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Integration writes require WALOS_TEST_ALLOW_WRITES=true after verifying a disposable local database.");

        NpgsqlConnectionStringBuilder target;
        try
        {
            target = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("Invalid integration test connection configuration.");
        }

        var loopback = target.Host is "localhost" or "127.0.0.1" or "::1";
        var testDatabase = target.Database is not null
            && Regex.IsMatch(target.Database, @"\Awalos_test(?:_[A-Za-z0-9_]+)?\z", RegexOptions.CultureInvariant);
        if (!loopback || !testDatabase)
            throw new InvalidOperationException(
                "Integration writes are restricted to loopback PostgreSQL and a walos_test or walos_test_<suffix> database.");
    }
}

internal sealed class IntegrationTestDataScope
{
    private readonly HashSet<long> _companyIds = [];

    public int Count => _companyIds.Count;

    public void RegisterCreatedCompany(long companyId)
    {
        if (companyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(companyId));
        _companyIds.Add(companyId);
    }

    public void EnsureOwnedCompany(long companyId)
    {
        if (!_companyIds.Contains(companyId))
            throw new InvalidOperationException("The seed target was not created by this fixture.");
    }

    public NpgsqlCommand? CreateCleanupCommand(NpgsqlConnection connection, NpgsqlTransaction? transaction = null)
    {
        if (_companyIds.Count == 0)
            return null;

        var command = new NpgsqlCommand(CleanupSql, connection, transaction);
        command.Parameters.AddWithValue("companyIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint, _companyIds.ToArray());
        return command;
    }

    public void Clear() => _companyIds.Clear();

    internal const string CleanupSql = @"
            -- Clean up in reverse dependency order
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
            DELETE FROM core.companies WHERE id = ANY(@companyIds);";
}
