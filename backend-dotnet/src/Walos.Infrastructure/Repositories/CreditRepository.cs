using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;
using Walos.Infrastructure.Data;

namespace Walos.Infrastructure.Repositories;

public class CreditRepository : ICreditRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<CreditRepository> _logger;

    public CreditRepository(IDbConnectionFactory connectionFactory, ILogger<CreditRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Credit> CreateCreditAsync(Credit credit)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                INSERT INTO sales.credits
                    (company_id, branch_id, order_id, customer_name, order_number,
                     original_total, amount_paid, credit_amount, status, notes, created_by, created_at)
                VALUES
                    (@CompanyId, @BranchId, @OrderId, @CustomerName, @OrderNumber,
                     @OriginalTotal, @AmountPaid, @CreditAmount, @Status, @Notes, @CreatedBy, NOW())
                RETURNING id";

            credit.Id = await connection.ExecuteScalarAsync<long>(sql, new
            {
                credit.CompanyId, credit.BranchId, credit.OrderId, credit.CustomerName,
                credit.OrderNumber, credit.OriginalTotal, credit.AmountPaid,
                credit.CreditAmount, credit.Status, credit.Notes, credit.CreatedBy
            });
            return credit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando credito");
            throw;
        }
    }

    public async Task<IEnumerable<Credit>> GetCreditsAsync(long companyId, string? status, string? search)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var where = "WHERE c.company_id = @CompanyId";
            if (!string.IsNullOrWhiteSpace(status) && status != "all")
                where += " AND c.status = @Status";
            if (!string.IsNullOrWhiteSpace(search))
                where += " AND LOWER(c.customer_name) LIKE @Search";

            var sql = $@"
                SELECT c.id AS Id, c.company_id AS CompanyId, c.branch_id AS BranchId,
                       c.order_id AS OrderId, c.customer_name AS CustomerName,
                       c.order_number AS OrderNumber, c.original_total AS OriginalTotal,
                       c.amount_paid AS AmountPaid, c.credit_amount AS CreditAmount,
                       c.status AS Status, c.notes AS Notes,
                       c.paid_at AS PaidAt, c.created_at AS CreatedAt, c.created_by AS CreatedBy
                FROM sales.credits c
                {where}
                ORDER BY c.created_at DESC";

            return await connection.QueryAsync<Credit>(sql, new
            {
                CompanyId = companyId,
                Status = status,
                Search = $"%{search?.ToLower()}%"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo creditos");
            throw;
        }
    }

    public async Task<Credit?> GetCreditByIdAsync(long creditId, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string creditSql = @"
                SELECT id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                       order_id AS OrderId, customer_name AS CustomerName,
                       order_number AS OrderNumber, original_total AS OriginalTotal,
                       amount_paid AS AmountPaid, credit_amount AS CreditAmount,
                       status AS Status, notes AS Notes,
                       paid_at AS PaidAt, created_at AS CreatedAt, created_by AS CreatedBy
                FROM sales.credits
                WHERE id = @CreditId AND company_id = @CompanyId";

            var credit = await connection.QueryFirstOrDefaultAsync<Credit>(creditSql, new { CreditId = creditId, CompanyId = companyId });
            if (credit == null) return null;

            const string paymentsSql = @"
                SELECT id AS Id, company_id AS CompanyId, credit_id AS CreditId,
                       amount AS Amount, notes AS Notes,
                       created_at AS CreatedAt, created_by AS CreatedBy
                FROM sales.credit_payments
                WHERE credit_id = @CreditId AND company_id = @CompanyId
                ORDER BY created_at ASC";

            credit.Payments = (await connection.QueryAsync<CreditPayment>(paymentsSql, new { CreditId = creditId, CompanyId = companyId })).ToList();
            return credit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo credito {CreditId}", creditId);
            throw;
        }
    }

    public async Task<CreditPayment> AddPaymentAsync(CreditPayment payment)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                INSERT INTO sales.credit_payments (company_id, credit_id, amount, notes, created_by, created_at)
                VALUES (@CompanyId, @CreditId, @Amount, @Notes, @CreatedBy, NOW())
                RETURNING id";
            payment.Id = await connection.ExecuteScalarAsync<long>(sql, new
            {
                payment.CompanyId, payment.CreditId, payment.Amount, payment.Notes, payment.CreatedBy
            });
            return payment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registrando abono a credito {CreditId}", payment.CreditId);
            throw;
        }
    }

    public async Task UpdateCreditAfterPaymentAsync(long creditId, long companyId, decimal newAmountPaid, decimal newCreditAmount, string newStatus, DateTime? paidAt)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE sales.credits
                SET amount_paid = @AmountPaid, credit_amount = @CreditAmount,
                    status = @Status, paid_at = @PaidAt, updated_at = NOW()
                WHERE id = @CreditId AND company_id = @CompanyId";
            await connection.ExecuteAsync(sql, new { AmountPaid = newAmountPaid, CreditAmount = newCreditAmount, Status = newStatus, PaidAt = paidAt, CreditId = creditId, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando credito {CreditId}", creditId);
            throw;
        }
    }

    public async Task CancelCreditAsync(long creditId, long companyId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE sales.credits SET status = 'cancelled', updated_at = NOW()
                WHERE id = @CreditId AND company_id = @CompanyId";
            await connection.ExecuteAsync(sql, new { CreditId = creditId, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelando credito {CreditId}", creditId);
            throw;
        }
    }
}
