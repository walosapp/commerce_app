using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;
using Walos.Infrastructure.Data;

namespace Walos.Infrastructure.Repositories;

public class RefundRepository : IRefundRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<RefundRepository> _logger;

    public RefundRepository(IDbConnectionFactory connectionFactory, ILogger<RefundRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Refund> CreateAsync(Refund refund)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            INSERT INTO sales.refunds (
                company_id, branch_id, order_id, refund_type, refund_amount,
                reason, status, approved_by, created_by, created_at
            ) VALUES (
                @CompanyId, @BranchId, @OrderId, @RefundType, @RefundAmount,
                @Reason, @Status, @ApprovedBy, @CreatedBy, NOW()
            ) RETURNING id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                        order_id AS OrderId, refund_type AS RefundType, refund_amount AS RefundAmount,
                        reason AS Reason, status AS Status, approved_by AS ApprovedBy,
                        created_by AS CreatedBy, created_at AS CreatedAt";

        return await connection.QuerySingleAsync<Refund>(sql, refund);
    }

    public async Task CreateItemsAsync(long refundId, List<RefundItem> items)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            INSERT INTO sales.refund_items (
                refund_id, order_item_id, quantity, unit_price, subtotal, created_at
            ) VALUES (
                @RefundId, @OrderItemId, @Quantity, @UnitPrice, @Subtotal, NOW()
            )";

        foreach (var item in items)
        {
            item.RefundId = refundId;
            await connection.ExecuteAsync(sql, item);
        }
    }

    public async Task<Refund?> GetByIdAsync(long id, long companyId, long branchId)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            SELECT r.id AS Id, r.company_id AS CompanyId, r.branch_id AS BranchId,
                   r.order_id AS OrderId, r.refund_type AS RefundType, r.refund_amount AS RefundAmount,
                   r.reason AS Reason, r.status AS Status, r.approved_by AS ApprovedBy,
                   r.idempotency_key AS IdempotencyKey, r.request_fingerprint AS RequestFingerprint,
                   r.cash_register_id AS CashRegisterId,
                   r.created_by AS CreatedBy, r.created_at AS CreatedAt
            FROM sales.refunds r
            JOIN sales.orders o
              ON o.id = r.order_id AND o.company_id = r.company_id
            WHERE r.id = @Id
              AND r.company_id = @CompanyId
              AND r.branch_id = @BranchId
              AND o.branch_id = @BranchId";

        var refund = await connection.QuerySingleOrDefaultAsync<Refund>(sql, new
        {
            Id = id,
            CompanyId = companyId,
            BranchId = branchId
        });
        if (refund != null)
        {
            const string itemsSql = @"
                SELECT ri.id AS Id, ri.refund_id AS RefundId, ri.order_item_id AS OrderItemId,
                       ri.quantity AS Quantity, ri.unit_price AS UnitPrice, ri.subtotal AS Subtotal,
                       ri.created_at AS CreatedAt
                FROM sales.refund_items ri
                WHERE ri.refund_id = @RefundId";

            refund.Items = (await connection.QueryAsync<RefundItem>(itemsSql, new { RefundId = refund.Id })).ToList();
        }
        return refund;
    }

    public async Task<IEnumerable<Refund>> GetByOrderIdAsync(long orderId, long companyId, long branchId)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            SELECT r.id AS Id, r.company_id AS CompanyId, r.branch_id AS BranchId,
                   r.order_id AS OrderId, r.refund_type AS RefundType, r.refund_amount AS RefundAmount,
                   r.reason AS Reason, r.status AS Status, r.approved_by AS ApprovedBy,
                   r.created_by AS CreatedBy, r.created_at AS CreatedAt
            FROM sales.refunds r
            JOIN sales.orders o
              ON o.id = r.order_id AND o.company_id = r.company_id
            WHERE r.order_id = @OrderId
              AND r.company_id = @CompanyId
              AND r.branch_id = @BranchId
              AND o.branch_id = @BranchId
            ORDER BY r.created_at DESC";

        return await connection.QueryAsync<Refund>(sql, new
        {
            OrderId = orderId,
            CompanyId = companyId,
            BranchId = branchId
        });
    }

    public async Task<IEnumerable<Refund>> GetAllAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo, int page, int limit)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        var sql = @"
            SELECT r.id AS Id, r.company_id AS CompanyId, r.branch_id AS BranchId,
                   r.order_id AS OrderId, r.refund_type AS RefundType, r.refund_amount AS RefundAmount,
                   r.reason AS Reason, r.status AS Status,
                   r.created_by AS CreatedBy, r.created_at AS CreatedAt,
                   o.order_number AS OrderNumber,
                   u.first_name || ' ' || u.last_name AS CreatedByName
            FROM sales.refunds r
            JOIN sales.orders o ON o.id = r.order_id
            LEFT JOIN core.users u ON u.id = r.created_by
            WHERE r.company_id = @CompanyId AND r.branch_id = @BranchId";

        if (dateFrom.HasValue) sql += " AND r.created_at >= @DateFrom";
        if (dateTo.HasValue) sql += " AND r.created_at <= @DateTo";
        sql += " ORDER BY r.created_at DESC LIMIT @Limit OFFSET @Offset";

        return await connection.QueryAsync<Refund>(sql, new
        {
            CompanyId = companyId,
            BranchId = branchId,
            DateFrom = dateFrom,
            DateTo = dateTo?.AddDays(1),
            Limit = limit,
            Offset = (page - 1) * limit
        });
    }

    public async Task<int> GetCountAsync(long companyId, long branchId, DateTime? dateFrom, DateTime? dateTo)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        var sql = @"
            SELECT COUNT(*) FROM sales.refunds
            WHERE company_id = @CompanyId AND branch_id = @BranchId";

        if (dateFrom.HasValue) sql += " AND created_at >= @DateFrom";
        if (dateTo.HasValue) sql += " AND created_at <= @DateTo";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            CompanyId = companyId,
            BranchId = branchId,
            DateFrom = dateFrom,
            DateTo = dateTo?.AddDays(1)
        });
    }

    public async Task UpdateOrderRefundStatusAsync(long orderId, long companyId, string refundStatus)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
            UPDATE sales.orders
            SET refund_status = @RefundStatus, updated_at = NOW()
            WHERE id = @OrderId AND company_id = @CompanyId";

        await connection.ExecuteAsync(sql, new { OrderId = orderId, CompanyId = companyId, RefundStatus = refundStatus });
    }

    public async Task<RefundProcessResult> ProcessAsync(RefundProcessCommand command)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(@"
                SELECT pg_advisory_xact_lock(
                    hashtextextended(CAST(@CompanyId AS text) || ':' || @IdempotencyKey, 0)
                )", Parameters(command), transaction);

            const string orderSql = @"
                SELECT id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                       order_number AS OrderNumber, status AS Status,
                       subtotal AS Subtotal, total AS Total,
                       discount_amount AS DiscountAmount,
                       final_total_paid AS FinalTotalPaid,
                       payment_method AS PaymentMethod,
                       refund_status AS RefundStatus
                FROM sales.orders
                WHERE id = @OrderId
                  AND company_id = @CompanyId
                  AND branch_id = @BranchId
                FOR UPDATE";

            var order = await connection.QuerySingleOrDefaultAsync<RefundOrderRow>(orderSql, Parameters(command), transaction)
                ?? throw new NotFoundException("Orden no encontrada");

            var replay = await GetReplayAsync(connection, transaction, command);
            if (replay is not null)
            {
                transaction.Commit();
                return replay;
            }

            var idempotencyKeyAlreadyUsed = await connection.ExecuteScalarAsync<bool>(@"
                SELECT EXISTS (
                    SELECT 1
                    FROM sales.refunds
                    WHERE company_id = @CompanyId
                      AND idempotency_key = @IdempotencyKey
                )", Parameters(command), transaction);
            if (idempotencyKeyAlreadyUsed)
                throw new BusinessException("Idempotency-Key ya fue utilizada en otra operacion");

            if (order.Status != "completed")
                throw new ValidationException("Solo se pueden devolver ordenes completadas");
            if (order.RefundStatus == "full_refund")
                throw new BusinessException("Esta orden ya fue devuelta completamente");

            const string itemsSql = @"
                SELECT oi.id AS Id, oi.product_id AS ProductId,
                       oi.product_name AS ProductName, oi.quantity AS Quantity,
                       oi.unit_price AS UnitPrice,
                       p.product_type AS ProductType, p.track_stock AS TrackStock,
                       COALESCE(SUM(CASE WHEN r.status = 'completed' THEN ri.quantity ELSE 0 END), 0) AS RefundedQuantity,
                       COALESCE(SUM(CASE WHEN r.status = 'completed' THEN ri.subtotal ELSE 0 END), 0) AS RefundedSubtotal
                FROM sales.order_items oi
                JOIN inventory.products p
                  ON p.id = oi.product_id AND p.company_id = oi.company_id
                LEFT JOIN sales.refund_items ri ON ri.order_item_id = oi.id
                LEFT JOIN sales.refunds r ON r.id = ri.refund_id AND r.company_id = oi.company_id
                WHERE oi.order_id = @OrderId AND oi.company_id = @CompanyId
                GROUP BY oi.id, oi.product_id, oi.product_name, oi.quantity,
                         oi.unit_price, p.product_type, p.track_stock
                ORDER BY oi.id";

            var orderItems = (await connection.QueryAsync<RefundOrderItemRow>(itemsSql, Parameters(command), transaction)).ToList();
            if (orderItems.Count == 0)
                throw new BusinessException("La orden no tiene items para devolver");

            var selected = SelectItems(command, orderItems);
            var refundItems = CalculateNetRefundItems(order, orderItems, selected);
            var refundAmount = Math.Round(refundItems.Sum(i => i.Subtotal), 2);

            var credit = await connection.QuerySingleOrDefaultAsync<Credit>(@"
                SELECT id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                       order_id AS OrderId, original_total AS OriginalTotal,
                       amount_paid AS AmountPaid, credit_amount AS CreditAmount,
                       status AS Status, paid_at AS PaidAt
                FROM sales.credits
                WHERE order_id = @OrderId
                  AND company_id = @CompanyId
                  AND branch_id = @BranchId
                ORDER BY id
                LIMIT 1
                FOR UPDATE", Parameters(command), transaction);

            var creditReduction = credit is null ? 0 : Math.Min(refundAmount, credit.CreditAmount);
            var moneyToReturn = Math.Round(refundAmount - creditReduction, 2);
            var cashToReturn = await CalculateCashPortionAsync(
                connection, transaction, order, credit, command, moneyToReturn);

            long? cashRegisterId = null;
            if (cashToReturn > 0)
            {
                cashRegisterId = await connection.QuerySingleOrDefaultAsync<long?>(@"
                    SELECT id
                    FROM sales.cash_registers
                    WHERE company_id = @CompanyId
                      AND branch_id = @BranchId
                      AND opened_by = @UserId
                      AND status = 'open'
                      AND deleted_at IS NULL
                    ORDER BY opened_at DESC
                    LIMIT 1
                    FOR UPDATE", Parameters(command), transaction);

                if (!cashRegisterId.HasValue)
                    throw new BusinessException("Debes abrir una caja antes de realizar una devolucion en efectivo");
            }

            var refund = await connection.QuerySingleAsync<Refund>(@"
                INSERT INTO sales.refunds (
                    company_id, branch_id, order_id, refund_type, refund_amount,
                    reason, status, idempotency_key, request_fingerprint,
                    cash_register_id, created_by, created_at
                ) VALUES (
                    @CompanyId, @BranchId, @OrderId, @RefundType, @RefundAmount,
                    @Reason, 'completed', @IdempotencyKey, @RequestFingerprint,
                    @CashRegisterId, @UserId, NOW()
                )
                RETURNING id AS Id, company_id AS CompanyId, branch_id AS BranchId,
                          order_id AS OrderId, refund_type AS RefundType,
                          refund_amount AS RefundAmount, reason AS Reason,
                          status AS Status, idempotency_key AS IdempotencyKey,
                          request_fingerprint AS RequestFingerprint,
                          cash_register_id AS CashRegisterId,
                          created_by AS CreatedBy, created_at AS CreatedAt", new
            {
                command.CompanyId,
                command.BranchId,
                command.OrderId,
                command.RefundType,
                RefundAmount = refundAmount,
                command.Reason,
                command.IdempotencyKey,
                command.RequestFingerprint,
                CashRegisterId = cashRegisterId,
                command.UserId
            }, transaction);

            foreach (var item in refundItems)
            {
                item.RefundId = refund.Id;
                item.Id = await connection.ExecuteScalarAsync<long>(@"
                    INSERT INTO sales.refund_items (
                        refund_id, order_item_id, quantity, unit_price, subtotal, created_at
                    ) VALUES (
                        @RefundId, @OrderItemId, @Quantity, @UnitPrice, @Subtotal, NOW()
                    ) RETURNING id", item, transaction);
            }

            await RestoreInventoryAsync(connection, transaction, command, order, orderItems, refundItems, refund.Id);

            if (credit is not null)
                await AdjustCreditAsync(connection, transaction, command, credit, refundAmount, creditReduction, moneyToReturn);

            if (cashRegisterId.HasValue)
                await RegisterCashRefundAsync(connection, transaction, command, refund.Id, cashRegisterId.Value, cashToReturn);

            var quantitiesByItem = refundItems.ToDictionary(i => i.OrderItemId, i => i.Quantity);
            var remainingQuantity = orderItems.Sum(i =>
                Math.Max(0, i.Quantity - i.RefundedQuantity - quantitiesByItem.GetValueOrDefault(i.Id)));
            var orderRefundStatus = remainingQuantity == 0 ? "full_refund" : "partial_refund";

            await connection.ExecuteAsync(@"
                UPDATE sales.orders
                SET refund_status = @RefundStatus, updated_at = NOW()
                WHERE id = @OrderId AND company_id = @CompanyId AND branch_id = @BranchId", new
            {
                RefundStatus = orderRefundStatus,
                command.OrderId,
                command.CompanyId,
                command.BranchId
            }, transaction);

            transaction.Commit();
            return new RefundProcessResult(refund, refundItems, false);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task<RefundProcessResult?> GetReplayAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        RefundProcessCommand command)
    {
        var existing = await connection.QuerySingleOrDefaultAsync<Refund>(@"
            SELECT r.id AS Id, r.company_id AS CompanyId, r.branch_id AS BranchId,
                   r.order_id AS OrderId, r.refund_type AS RefundType,
                   r.refund_amount AS RefundAmount, r.reason AS Reason, r.status AS Status,
                   r.idempotency_key AS IdempotencyKey,
                   r.request_fingerprint AS RequestFingerprint,
                   r.cash_register_id AS CashRegisterId,
                   r.created_by AS CreatedBy, r.created_at AS CreatedAt
            FROM sales.refunds r
            JOIN sales.orders o
              ON o.id = r.order_id AND o.company_id = r.company_id
            WHERE r.company_id = @CompanyId
              AND r.branch_id = @BranchId
              AND o.branch_id = @BranchId
              AND r.idempotency_key = @IdempotencyKey
            FOR UPDATE OF r", Parameters(command), transaction);

        if (existing is null)
            return null;
        if (!string.Equals(existing.RequestFingerprint, command.RequestFingerprint, StringComparison.Ordinal))
            throw new BusinessException("Idempotency-Key ya fue utilizada con una operacion diferente");

        var items = (await connection.QueryAsync<RefundItem>(@"
            SELECT id AS Id, refund_id AS RefundId, order_item_id AS OrderItemId,
                   quantity AS Quantity, unit_price AS UnitPrice,
                   subtotal AS Subtotal, created_at AS CreatedAt
            FROM sales.refund_items
            WHERE refund_id = @RefundId
            ORDER BY id", new { RefundId = existing.Id }, transaction)).ToList();

        return new RefundProcessResult(existing, items, true);
    }

    private static Dictionary<long, decimal> SelectItems(
        RefundProcessCommand command,
        List<RefundOrderItemRow> orderItems)
    {
        if (command.RefundType == "full")
        {
            var remaining = orderItems
                .Where(i => i.Quantity > i.RefundedQuantity)
                .ToDictionary(i => i.Id, i => i.Quantity - i.RefundedQuantity);
            if (remaining.Count == 0)
                throw new BusinessException("La orden ya no tiene cantidades disponibles para devolver");
            return remaining;
        }

        var selected = new Dictionary<long, decimal>();
        foreach (var requested in command.Items)
        {
            var item = orderItems.SingleOrDefault(i => i.Id == requested.OrderItemId)
                ?? throw new ValidationException($"Item de orden {requested.OrderItemId} no encontrado");
            var available = item.Quantity - item.RefundedQuantity;
            if (requested.Quantity > available)
                throw new ValidationException($"Cantidad invalida para {item.ProductName}. Disponible: {available}");
            selected.Add(item.Id, requested.Quantity);
        }
        return selected;
    }

    private static List<RefundItem> CalculateNetRefundItems(
        RefundOrderRow order,
        List<RefundOrderItemRow> orderItems,
        Dictionary<long, decimal> selected)
    {
        var grossTotal = orderItems.Sum(i => i.Quantity * i.UnitPrice);
        if (grossTotal <= 0)
            throw new BusinessException("La orden no tiene un valor reembolsable valido");

        var netOrderTotal = Math.Round(Math.Max(0, order.Total - order.DiscountAmount), 2);
        var entitlements = new Dictionary<long, decimal>();
        var remaining = netOrderTotal;
        var ordered = orderItems.OrderBy(i => i.Id).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var item = ordered[index];
            var entitlement = index == ordered.Count - 1
                ? remaining
                : Math.Round(netOrderTotal * ((item.Quantity * item.UnitPrice) / grossTotal), 2, MidpointRounding.AwayFromZero);
            entitlement = Math.Min(remaining, Math.Max(0, entitlement));
            entitlements[item.Id] = entitlement;
            remaining = Math.Round(remaining - entitlement, 2);
        }

        var result = new List<RefundItem>();
        foreach (var pair in selected.OrderBy(p => p.Key))
        {
            var item = orderItems.Single(i => i.Id == pair.Key);
            var availableQuantity = item.Quantity - item.RefundedQuantity;
            var availableValue = Math.Max(0, Math.Round(entitlements[item.Id] - item.RefundedSubtotal, 2));
            var subtotal = pair.Value == availableQuantity
                ? availableValue
                : Math.Min(availableValue, Math.Round(entitlements[item.Id] * (pair.Value / item.Quantity), 2, MidpointRounding.AwayFromZero));

            result.Add(new RefundItem
            {
                OrderItemId = item.Id,
                Quantity = pair.Value,
                UnitPrice = pair.Value == 0 ? 0 : Math.Round(subtotal / pair.Value, 2, MidpointRounding.AwayFromZero),
                Subtotal = subtotal
            });
        }
        return result;
    }

    private static async Task<decimal> CalculateCashPortionAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        RefundOrderRow order,
        Credit? credit,
        RefundProcessCommand command,
        decimal moneyToReturn)
    {
        if (moneyToReturn <= 0)
            return 0;

        var payments = (await connection.QueryAsync<RefundPaymentRow>(@"
            SELECT 'order' AS Source, op.method AS Method, op.amount AS Amount
            FROM sales.order_payments op
            JOIN sales.orders o
              ON o.id = op.order_id AND o.company_id = op.company_id
            WHERE op.order_id = @OrderId
              AND op.company_id = @CompanyId
              AND o.branch_id = @BranchId

            UNION ALL

            SELECT 'credit' AS Source, cp.payment_method AS Method, cp.amount AS Amount
            FROM sales.credit_payments cp
            JOIN sales.credits c
              ON c.id = cp.credit_id AND c.company_id = cp.company_id
            JOIN sales.orders o
              ON o.id = c.order_id AND o.company_id = c.company_id
            WHERE c.order_id = @OrderId
              AND cp.company_id = @CompanyId
              AND c.branch_id = @BranchId
              AND o.branch_id = @BranchId", Parameters(command), transaction)).ToList();

        if (!payments.Any(payment => payment.Source == "order") && order.FinalTotalPaid > 0)
        {
            payments.Add(new RefundPaymentRow
            {
                Source = "order",
                Method = order.PaymentMethod,
                Amount = order.FinalTotalPaid
            });
        }

        if (payments.Any(payment => payment.Amount <= 0))
            throw new BusinessException("Los pagos de la orden no permiten calcular la devolucion");

        foreach (var payment in payments)
        {
            payment.Method = payment.Method?.Trim().ToLowerInvariant();
            if (payment.Method is not ("cash" or "card" or "transfer" or "nequi" or "other"))
                throw new BusinessException("No se puede determinar el medio original del dinero a devolver");
        }

        var paymentsTotal = payments.Sum(payment => payment.Amount);
        if (paymentsTotal <= 0)
            throw new BusinessException("Los pagos de la orden no permiten calcular la devolucion");

        var cashCollected = payments
            .Where(payment => payment.Method == "cash")
            .Sum(payment => payment.Amount);
        if (cashCollected <= 0)
            return 0;

        var previousMoneyReturned = credit is not null
            ? Math.Max(0, Math.Round(paymentsTotal - credit.AmountPaid, 2))
            : await connection.ExecuteScalarAsync<decimal>(@"
                SELECT COALESCE(SUM(r.refund_amount), 0)
                FROM sales.refunds r
                JOIN sales.orders o
                  ON o.id = r.order_id AND o.company_id = r.company_id
                WHERE r.order_id = @OrderId
                  AND r.company_id = @CompanyId
                  AND r.branch_id = @BranchId
                  AND o.branch_id = @BranchId
                  AND r.status = 'completed'", Parameters(command), transaction);

        var previousCashReturned = await connection.ExecuteScalarAsync<decimal>(@"
            SELECT COALESCE(SUM(cm.amount), 0)
            FROM sales.refunds r
            JOIN sales.orders o
              ON o.id = r.order_id AND o.company_id = r.company_id
            JOIN sales.cash_movements cm
              ON cm.company_id = r.company_id
             AND cm.cash_register_id = r.cash_register_id
             AND cm.type = 'out'
             AND cm.reason = 'Devolucion de venta'
             AND cm.notes = ('Refund #' || r.id::text)
            WHERE r.order_id = @OrderId
              AND r.company_id = @CompanyId
              AND r.branch_id = @BranchId
              AND o.branch_id = @BranchId
              AND r.status = 'completed'", Parameters(command), transaction);

        var cumulativeMoneyReturned = Math.Min(
            paymentsTotal,
            Math.Max(0, previousMoneyReturned + moneyToReturn));
        var targetCumulativeCash = Math.Min(
            cashCollected,
            Math.Round(
                cumulativeMoneyReturned * (cashCollected / paymentsTotal),
                2,
                MidpointRounding.AwayFromZero));
        var remainingCashCollected = Math.Max(0, cashCollected - previousCashReturned);

        return Math.Max(0, Math.Min(
            moneyToReturn,
            Math.Min(remainingCashCollected, targetCumulativeCash - previousCashReturned)));
    }

    private static async Task AdjustCreditAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        RefundProcessCommand command,
        Credit credit,
        decimal refundAmount,
        decimal creditReduction,
        decimal moneyReturned)
    {
        var originalTotal = Math.Max(0, Math.Round(credit.OriginalTotal - refundAmount, 2));
        var amountPaid = Math.Max(0, Math.Round(credit.AmountPaid - moneyReturned, 2));
        var creditAmount = Math.Max(0, Math.Round(credit.CreditAmount - creditReduction, 2));
        var status = originalTotal == 0
            ? "cancelled"
            : creditAmount == 0 ? "paid" : amountPaid > 0 ? "partial" : "pending";

        await connection.ExecuteAsync(@"
            UPDATE sales.credits
            SET original_total = @OriginalTotal,
                amount_paid = @AmountPaid,
                credit_amount = @CreditAmount,
                status = @Status,
                paid_at = CASE WHEN @Status = 'paid' THEN COALESCE(paid_at, NOW()) ELSE NULL END,
                updated_at = NOW()
            WHERE id = @Id AND company_id = @CompanyId", new
        {
            OriginalTotal = originalTotal,
            AmountPaid = amountPaid,
            CreditAmount = creditAmount,
            Status = status,
            credit.Id,
            command.CompanyId
        }, transaction);
    }

    private static async Task RegisterCashRefundAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        RefundProcessCommand command,
        long refundId,
        long cashRegisterId,
        decimal cashAmount)
    {
        var updated = await connection.ExecuteAsync(@"
            UPDATE sales.cash_registers
            SET cash_out = cash_out + @Amount, updated_at = NOW()
            WHERE id = @CashRegisterId
              AND company_id = @CompanyId
              AND branch_id = @BranchId
              AND status = 'open'", new
        {
            Amount = cashAmount,
            CashRegisterId = cashRegisterId,
            command.CompanyId,
            command.BranchId
        }, transaction);
        if (updated != 1)
            throw new BusinessException("La caja dejo de estar disponible durante la devolucion");

        await connection.ExecuteAsync(@"
            INSERT INTO sales.cash_movements (
                company_id, cash_register_id, type, amount, reason, notes, created_by, created_at
            ) VALUES (
                @CompanyId, @CashRegisterId, 'out', @Amount,
                'Devolucion de venta', @Notes, @UserId, NOW()
            )", new
        {
            command.CompanyId,
            CashRegisterId = cashRegisterId,
            Amount = cashAmount,
            Notes = $"Refund #{refundId}",
            command.UserId
        }, transaction);
    }

    private static async Task RestoreInventoryAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        RefundProcessCommand command,
        RefundOrderRow order,
        List<RefundOrderItemRow> orderItems,
        List<RefundItem> refundItems,
        long refundId)
    {
        foreach (var refundItem in refundItems)
        {
            var orderItem = orderItems.Single(i => i.Id == refundItem.OrderItemId);
            if (orderItem.ProductType == "prepared")
            {
                var ingredients = await connection.QueryAsync<RecipeRefundRow>(@"
                    SELECT r.ingredient_id AS ProductId,
                           r.quantity AS QuantityPerProduct,
                           p.track_stock AS TrackStock
                    FROM inventory.recipes r
                    JOIN inventory.products p
                      ON p.id = r.ingredient_id AND p.company_id = r.company_id
                    WHERE r.company_id = @CompanyId AND r.product_id = @ProductId", new
                {
                    command.CompanyId,
                    ProductId = orderItem.ProductId
                }, transaction);

                foreach (var ingredient in ingredients.Where(i => i.TrackStock))
                {
                    await RestoreTrackedProductAsync(connection, transaction, command,
                        ingredient.ProductId, ingredient.QuantityPerProduct * refundItem.Quantity,
                        0, "refund_recipe", refundId, order.OrderNumber);
                }
            }
            else if (orderItem.TrackStock)
            {
                await RestoreTrackedProductAsync(connection, transaction, command,
                    orderItem.ProductId, refundItem.Quantity, refundItem.UnitPrice,
                    "refund", refundId, order.OrderNumber);
            }
        }
    }

    private static async Task RestoreTrackedProductAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        RefundProcessCommand command,
        long productId,
        decimal quantity,
        decimal unitCost,
        string movementType,
        long refundId,
        string orderNumber)
    {
        var stockAfter = await connection.QuerySingleOrDefaultAsync<decimal?>(@"
            INSERT INTO inventory.stock (
                company_id, branch_id, product_id, quantity, reserved_quantity, created_at, updated_at
            ) VALUES (
                @CompanyId, @BranchId, @ProductId, @Quantity, 0, NOW(), NOW()
            )
            ON CONFLICT (branch_id, product_id) DO UPDATE
            SET quantity = inventory.stock.quantity + EXCLUDED.quantity,
                updated_at = NOW()
            WHERE inventory.stock.company_id = @CompanyId
            RETURNING quantity", new
        {
            command.CompanyId,
            command.BranchId,
            ProductId = productId,
            Quantity = quantity
        }, transaction);
        if (!stockAfter.HasValue)
            throw new BusinessException("No se pudo restaurar el stock del producto devuelto");

        await connection.ExecuteAsync(@"
            INSERT INTO inventory.movements (
                company_id, branch_id, product_id, movement_type, quantity,
                unit_cost, reference_type, reference_id, notes, stock_after,
                created_by, created_at
            ) VALUES (
                @CompanyId, @BranchId, @ProductId, @MovementType, @Quantity,
                @UnitCost, 'refund', @RefundId, @Notes, @StockAfter,
                @UserId, NOW()
            )", new
        {
            command.CompanyId,
            command.BranchId,
            command.UserId,
            ProductId = productId,
            MovementType = movementType,
            Quantity = quantity,
            UnitCost = unitCost,
            RefundId = refundId,
            Notes = $"Devolucion - Orden {orderNumber}",
            StockAfter = stockAfter.Value
        }, transaction);
    }

    private sealed class RefundOrderRow
    {
        public long Id { get; init; }
        public long CompanyId { get; init; }
        public long BranchId { get; init; }
        public string OrderNumber { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public decimal Subtotal { get; init; }
        public decimal Total { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal FinalTotalPaid { get; init; }
        public string? PaymentMethod { get; init; }
        public string? RefundStatus { get; init; }
    }

    private static object Parameters(RefundProcessCommand command) => new
    {
        command.CompanyId,
        command.BranchId,
        command.UserId,
        command.OrderId,
        command.RefundType,
        command.Reason,
        command.IdempotencyKey,
        command.RequestFingerprint
    };

    private sealed class RefundOrderItemRow
    {
        public long Id { get; init; }
        public long ProductId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public string ProductType { get; init; } = string.Empty;
        public bool TrackStock { get; init; }
        public decimal RefundedQuantity { get; init; }
        public decimal RefundedSubtotal { get; init; }
    }

    private sealed class RefundPaymentRow
    {
        public string Source { get; init; } = string.Empty;
        public string? Method { get; set; }
        public decimal Amount { get; init; }
    }

    private sealed class RecipeRefundRow
    {
        public long ProductId { get; init; }
        public decimal QuantityPerProduct { get; init; }
        public bool TrackStock { get; init; }
    }
}
