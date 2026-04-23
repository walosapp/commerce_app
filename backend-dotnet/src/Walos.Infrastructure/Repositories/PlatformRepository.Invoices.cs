using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Walos.Domain.Entities.Platform;

namespace Walos.Infrastructure.Repositories;

public partial class PlatformRepository
{
    public async Task<IEnumerable<BillingInvoice>> GetInvoicesAsync(long companyId)
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                SELECT id AS Id, company_id AS CompanyId, invoice_number AS InvoiceNumber,
                       period_start AS PeriodStart, period_end AS PeriodEnd,
                       subtotal AS Subtotal, tax_rate AS TaxRate, tax_amount AS TaxAmount,
                       total AS Total, status AS Status, sent_at AS SentAt, paid_at AS PaidAt,
                       due_date AS DueDate, payment_method AS PaymentMethod,
                       payment_ref AS PaymentRef, notes AS Notes,
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM platform.billing_invoices
                WHERE company_id = @CompanyId
                ORDER BY created_at DESC";

            return await conn.QueryAsync<BillingInvoice>(sql, new { CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo facturas de empresa {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<BillingInvoice?> GetInvoiceByIdAsync(long id)
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string invoiceSql = @"
                SELECT i.id AS Id, i.company_id AS CompanyId, i.invoice_number AS InvoiceNumber,
                       i.period_start AS PeriodStart, i.period_end AS PeriodEnd,
                       i.subtotal AS Subtotal, i.tax_rate AS TaxRate, i.tax_amount AS TaxAmount,
                       i.total AS Total, i.status AS Status, i.sent_at AS SentAt, i.paid_at AS PaidAt,
                       i.due_date AS DueDate, i.payment_method AS PaymentMethod,
                       i.payment_ref AS PaymentRef, i.notes AS Notes,
                       i.created_at AS CreatedAt, i.updated_at AS UpdatedAt,
                       c.name AS CompanyName
                FROM platform.billing_invoices i
                INNER JOIN core.companies c ON c.id = i.company_id
                WHERE i.id = @Id";

            const string itemsSql = @"
                SELECT id AS Id, invoice_id AS InvoiceId, service_code AS ServiceCode,
                       description AS Description, quantity AS Quantity,
                       unit_price AS UnitPrice, subtotal AS Subtotal, created_at AS CreatedAt
                FROM platform.billing_invoice_items
                WHERE invoice_id = @InvoiceId
                ORDER BY id ASC";

            var invoice = await conn.QueryFirstOrDefaultAsync<BillingInvoice>(invoiceSql, new { Id = id });
            if (invoice is null) return null;

            var items = await conn.QueryAsync<BillingInvoiceItem>(itemsSql, new { InvoiceId = id });
            invoice.Items = items.ToList();
            return invoice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo factura {InvoiceId}", id);
            throw;
        }
    }

    public async Task<BillingInvoice> CreateInvoiceAsync(BillingInvoice invoice, IEnumerable<BillingInvoiceItem> items)
    {
        try
        {
            using var conn = (NpgsqlConnection)await _connectionFactory.CreateConnectionAsync();
            using var tx = await conn.BeginTransactionAsync();

            const string invoiceSql = @"
                INSERT INTO platform.billing_invoices
                    (company_id, invoice_number, period_start, period_end,
                     subtotal, tax_rate, tax_amount, total, status, due_date, notes)
                VALUES
                    (@CompanyId, @InvoiceNumber, @PeriodStart, @PeriodEnd,
                     @Subtotal, @TaxRate, @TaxAmount, @Total, @Status, @DueDate, @Notes)
                RETURNING id AS Id, company_id AS CompanyId, invoice_number AS InvoiceNumber,
                          period_start AS PeriodStart, period_end AS PeriodEnd,
                          subtotal AS Subtotal, tax_rate AS TaxRate, tax_amount AS TaxAmount,
                          total AS Total, status AS Status, due_date AS DueDate,
                          created_at AS CreatedAt, updated_at AS UpdatedAt";

            var created = await conn.QuerySingleAsync<BillingInvoice>(invoiceSql, invoice, tx);

            const string itemSql = @"
                INSERT INTO platform.billing_invoice_items
                    (invoice_id, service_code, description, quantity, unit_price, subtotal)
                VALUES
                    (@InvoiceId, @ServiceCode, @Description, @Quantity, @UnitPrice, @Subtotal)";

            foreach (var item in items)
            {
                item.InvoiceId = created.Id;
                await conn.ExecuteAsync(itemSql, item, tx);
            }

            await tx.CommitAsync();
            created.Items = items.ToList();
            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando factura para empresa {CompanyId}", invoice.CompanyId);
            throw;
        }
    }

    public async Task UpdateInvoiceStatusAsync(long id, string status, string? paymentRef = null)
    {
        try
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE platform.billing_invoices
                SET status       = @Status,
                    payment_ref  = COALESCE(@PaymentRef, payment_ref),
                    sent_at      = CASE WHEN @Status = 'sent'  AND sent_at IS NULL THEN NOW() ELSE sent_at END,
                    paid_at      = CASE WHEN @Status = 'paid'  AND paid_at IS NULL THEN NOW() ELSE paid_at END,
                    updated_at   = NOW()
                WHERE id = @Id";

            await conn.ExecuteAsync(sql, new { Id = id, Status = status, PaymentRef = paymentRef });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando estado factura {InvoiceId}", id);
            throw;
        }
    }
}
