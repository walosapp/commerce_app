using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
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

    public async Task<IEnumerable<Credit>> GetCreditsAsync(long companyId, long branchId, string? status, string? search)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            var where = "WHERE c.company_id = @CompanyId AND c.branch_id = @BranchId";
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
                BranchId = branchId,
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

    public async Task<Credit?> GetCreditByIdAsync(long creditId, long companyId, long branchId)
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
                WHERE id = @CreditId
                  AND company_id = @CompanyId
                  AND branch_id = @BranchId";

            var credit = await connection.QueryFirstOrDefaultAsync<Credit>(creditSql, new
            {
                CreditId = creditId,
                CompanyId = companyId,
                BranchId = branchId
            });
            if (credit == null) return null;

            const string paymentsSql = @"
                SELECT cp.id AS Id, cp.company_id AS CompanyId, cp.credit_id AS CreditId,
                       cp.amount AS Amount, cp.payment_method AS PaymentMethod,
                       cp.cash_register_id AS CashRegisterId, cp.notes AS Notes,
                       cp.created_at AS CreatedAt, cp.created_by AS CreatedBy
                FROM sales.credit_payments cp
                JOIN sales.credits c
                  ON c.id = cp.credit_id
                 AND c.company_id = cp.company_id
                 AND c.branch_id = @BranchId
                WHERE cp.credit_id = @CreditId AND cp.company_id = @CompanyId
                ORDER BY cp.created_at ASC";

            credit.Payments = (await connection.QueryAsync<CreditPayment>(paymentsSql, new
            {
                CreditId = creditId,
                CompanyId = companyId,
                BranchId = branchId
            })).ToList();
            return credit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo credito {CreditId}", creditId);
            throw;
        }
    }

    public async Task<Credit?> GetCreditByOrderAsync(long orderId, long companyId, long branchId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();

            const string sql = @"
                SELECT id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                       order_id AS OrderId, customer_name AS CustomerName,
                       order_number AS OrderNumber, original_total AS OriginalTotal,
                       amount_paid AS AmountPaid, credit_amount AS CreditAmount,
                       status AS Status, notes AS Notes,
                       paid_at AS PaidAt, created_at AS CreatedAt, created_by AS CreatedBy
                FROM sales.credits
                WHERE order_id = @OrderId
                  AND company_id = @CompanyId
                  AND branch_id = @BranchId
                ORDER BY id DESC
                LIMIT 1";

            return await connection.QueryFirstOrDefaultAsync<Credit>(sql, new
            {
                OrderId = orderId,
                CompanyId = companyId,
                BranchId = branchId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo credito de la orden {OrderId}", orderId);
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

    public async Task<Credit> ProcessPaymentAsync(CreditPaymentCommand command)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            const string creditSql = @"
                SELECT id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                       order_id AS OrderId, customer_name AS CustomerName,
                       order_number AS OrderNumber, original_total AS OriginalTotal,
                       amount_paid AS AmountPaid, credit_amount AS CreditAmount,
                       status AS Status, notes AS Notes, paid_at AS PaidAt,
                       created_at AS CreatedAt, created_by AS CreatedBy
                FROM sales.credits
                WHERE id = @CreditId
                  AND company_id = @CompanyId
                  AND branch_id = @BranchId
                FOR UPDATE";

            var credit = await connection.QuerySingleOrDefaultAsync<Credit>(creditSql, command, transaction)
                ?? throw new NotFoundException("Credito no encontrado");

            if (credit.Status is "paid" or "cancelled")
                throw new BusinessException("Este credito ya fue saldado o cancelado");

            if (command.Amount <= 0)
                throw new ValidationException("El monto del abono debe ser mayor a cero");

            if (command.Amount > credit.CreditAmount)
                throw new ValidationException($"El abono no puede superar el saldo pendiente de {credit.CreditAmount:N2}");

            long? cashRegisterId = null;
            if (command.PaymentMethod == "cash")
            {
                const string registerSql = @"
                    SELECT id
                    FROM sales.cash_registers
                    WHERE company_id = @CompanyId
                      AND branch_id = @BranchId
                      AND opened_by = @UserId
                      AND status = 'open'
                      AND deleted_at IS NULL
                    ORDER BY opened_at DESC
                    LIMIT 1
                    FOR UPDATE";

                cashRegisterId = await connection.QuerySingleOrDefaultAsync<long?>(registerSql, command, transaction);
                if (!cashRegisterId.HasValue)
                    throw new BusinessException("Debes abrir una caja antes de registrar un abono en efectivo");
            }

            const string paymentSql = @"
                INSERT INTO sales.credit_payments (
                    company_id, credit_id, amount, payment_method, cash_register_id,
                    notes, created_by, created_at
                ) VALUES (
                    @CompanyId, @CreditId, @Amount, @PaymentMethod, @CashRegisterId,
                    @Notes, @UserId, NOW()
                )";

            await connection.ExecuteAsync(paymentSql, new
            {
                command.CompanyId,
                command.CreditId,
                command.Amount,
                command.PaymentMethod,
                CashRegisterId = cashRegisterId,
                command.Notes,
                command.UserId
            }, transaction);

            var newCreditAmount = Math.Round(credit.CreditAmount - command.Amount, 2);
            var newStatus = newCreditAmount == 0 ? "paid" : "partial";

            const string updateCreditSql = @"
                UPDATE sales.credits
                SET amount_paid = amount_paid + @Amount,
                    credit_amount = @CreditAmount,
                    status = @Status,
                    paid_at = CASE WHEN @Status = 'paid' THEN NOW() ELSE NULL END,
                    updated_at = NOW()
                WHERE id = @CreditId
                  AND company_id = @CompanyId
                  AND branch_id = @BranchId";

            var updated = await connection.ExecuteAsync(updateCreditSql, new
            {
                command.Amount,
                CreditAmount = newCreditAmount,
                Status = newStatus,
                command.CreditId,
                command.CompanyId,
                command.BranchId
            }, transaction);

            if (updated != 1)
                throw new BusinessException("El credito dejo de estar disponible durante el abono");

            if (cashRegisterId.HasValue)
            {
                const string updateCashSql = @"
                    UPDATE sales.cash_registers
                    SET cash_in = cash_in + @Amount,
                        updated_at = NOW()
                    WHERE id = @CashRegisterId
                      AND company_id = @CompanyId
                      AND branch_id = @BranchId
                      AND status = 'open';

                    INSERT INTO sales.cash_movements (
                        company_id, cash_register_id, type, amount, reason, notes, created_by, created_at
                    ) VALUES (
                        @CompanyId, @CashRegisterId, 'in', @Amount,
                        'Abono de credito', @MovementNotes, @UserId, NOW()
                    );";

                await connection.ExecuteAsync(updateCashSql, new
                {
                    command.CompanyId,
                    command.BranchId,
                    command.UserId,
                    command.Amount,
                    CashRegisterId = cashRegisterId.Value,
                    MovementNotes = $"Credito #{command.CreditId}"
                }, transaction);
            }

            credit.AmountPaid += command.Amount;
            credit.CreditAmount = newCreditAmount;
            credit.Status = newStatus;
            credit.PaidAt = newStatus == "paid" ? DateTime.UtcNow : null;

            transaction.Commit();
            return credit;
        }
        catch
        {
            transaction.Rollback();
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

    public async Task<bool> CancelCreditAsync(long creditId, long companyId, long branchId)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            const string sql = @"
                UPDATE sales.credits SET status = 'cancelled', updated_at = NOW()
                WHERE id = @CreditId
                  AND company_id = @CompanyId
                  AND branch_id = @BranchId
                  AND status <> 'paid'";
            return await connection.ExecuteAsync(sql, new
            {
                CreditId = creditId,
                CompanyId = companyId,
                BranchId = branchId
            }) == 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelando credito {CreditId}", creditId);
            throw;
        }
    }
}
