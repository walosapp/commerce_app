using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;
using Walos.Infrastructure.Data;

namespace Walos.Infrastructure.Repositories;

public class OrderPaymentRepository : IOrderPaymentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<OrderPaymentRepository> _logger;

    public OrderPaymentRepository(IDbConnectionFactory connectionFactory, ILogger<OrderPaymentRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<OrderPayment> CreateAsync(OrderPayment payment)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            INSERT INTO sales.order_payments (
                company_id, order_id, method, amount, reference, created_at
            ) VALUES (
                @CompanyId, @OrderId, @Method, @Amount, @Reference, NOW()
            )
            RETURNING id AS Id, company_id AS CompanyId, order_id AS OrderId,
                      method AS Method, amount AS Amount, reference AS Reference,
                      created_at AS CreatedAt";

        var result = await connection.QuerySingleAsync<OrderPayment>(sql, new
        {
            payment.CompanyId,
            payment.OrderId,
            payment.Method,
            payment.Amount,
            payment.Reference
        });

        _logger.LogInformation("Pago registrado: Order={OrderId}, Method={Method}, Amount={Amount}",
            result.OrderId, result.Method, result.Amount);

        return result;
    }

    public async Task<IEnumerable<OrderPayment>> GetByOrderAsync(long orderId, long companyId)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            SELECT 
                id AS Id, company_id AS CompanyId, order_id AS OrderId,
                method AS Method, amount AS Amount, reference AS Reference,
                created_at AS CreatedAt
            FROM sales.order_payments
            WHERE order_id = @OrderId AND company_id = @CompanyId
            ORDER BY created_at ASC";

        return await connection.QueryAsync<OrderPayment>(sql, new { OrderId = orderId, CompanyId = companyId });
    }

    public async Task<IEnumerable<PaymentMethodSummary>> GetSummaryByCashRegisterAsync(long cashRegisterId, long companyId, long branchId)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            SELECT 
                op.method AS Method,
                SUM(op.amount) AS TotalAmount,
                COUNT(*) AS TransactionCount
            FROM sales.order_payments op
            INNER JOIN sales.orders o ON op.order_id = o.id AND o.company_id = op.company_id
            WHERE o.cash_register_id = @CashRegisterId 
              AND op.company_id = @CompanyId
              AND o.branch_id = @BranchId
            GROUP BY op.method
            ORDER BY TotalAmount DESC";

        return await connection.QueryAsync<PaymentMethodSummary>(sql, new
        {
            CashRegisterId = cashRegisterId,
            CompanyId = companyId,
            BranchId = branchId
        });
    }

    public async Task<IEnumerable<PaymentMethodSummary>> GetSummaryByDateRangeAsync(long companyId, long branchId, DateTime dateFrom, DateTime dateTo)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            SELECT 
                op.method AS Method,
                SUM(op.amount) AS TotalAmount,
                COUNT(*) AS TransactionCount
            FROM sales.order_payments op
            INNER JOIN sales.orders o ON op.order_id = o.id AND o.company_id = op.company_id
            WHERE o.company_id = @CompanyId
              AND o.branch_id = @BranchId
              AND o.created_at BETWEEN @DateFrom AND @DateTo
            GROUP BY op.method
            ORDER BY TotalAmount DESC";

        return await connection.QueryAsync<PaymentMethodSummary>(sql, new
        {
            CompanyId = companyId,
            BranchId = branchId,
            DateFrom = dateFrom,
            DateTo = dateTo
        });
    }
}
