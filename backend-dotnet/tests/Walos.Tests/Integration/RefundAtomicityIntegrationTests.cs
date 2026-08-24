using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Walos.Application.DTOs.Sales;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Integration;

public class RefundAtomicityIntegrationTests : IntegrationTestBase
{
    private RefundService Service => new(RefundRepository, NullLogger<RefundService>.Instance);

    [SkippableFact]
    public async Task Full_Refund_Restores_All_Stock()
    {
        var ctx = await SeedOrderAsync("Full", paymentMethod: "card");
        var result = await RefundAsync(ctx, "full", NewKey(), null);

        Assert.Equal(100m, result.RefundAmount);
        Assert.Equal(2m, result.Items!.Single().Quantity);
        Assert.Equal(10m, await GetStockAsync(ctx));
        Assert.Equal("full_refund", await GetRefundStatusAsync(ctx));
        Assert.Equal(1, await CountMovementsAsync(ctx));
    }

    [SkippableFact]
    public async Task Partial_Refund_Uses_Requested_Quantity()
    {
        var ctx = await SeedOrderAsync("Partial", paymentMethod: "card");
        var result = await RefundAsync(ctx, "partial", NewKey(), 0.5m);

        Assert.Equal(25m, result.RefundAmount);
        Assert.Equal(0.5m, result.Items!.Single().Quantity);
        Assert.Equal(8.5m, await GetStockAsync(ctx));
        Assert.Equal("partial_refund", await GetRefundStatusAsync(ctx));
    }

    [SkippableFact]
    public async Task Second_Partial_Refund_Uses_Remaining_Quantity()
    {
        var ctx = await SeedOrderAsync("Second partial", paymentMethod: "card");
        await RefundAsync(ctx, "partial", NewKey(), 0.5m);
        var second = await RefundAsync(ctx, "partial", NewKey(), 1m);

        Assert.Equal(50m, second.RefundAmount);
        Assert.Equal(1.5m, await GetRefundedQuantityAsync(ctx.OrderItemId));
        Assert.Equal(9.5m, await GetStockAsync(ctx));
    }

    [SkippableFact]
    public async Task Accumulated_Refund_Cannot_Exceed_Sold_Quantity()
    {
        var ctx = await SeedOrderAsync("Over quantity", paymentMethod: "card");
        await RefundAsync(ctx, "partial", NewKey(), 1.5m);

        await Assert.ThrowsAsync<ValidationException>(() => RefundAsync(ctx, "partial", NewKey(), 0.6m));

        Assert.Equal(1.5m, await GetRefundedQuantityAsync(ctx.OrderItemId));
        Assert.Equal(1, await CountRefundsAsync(ctx));
    }

    [SkippableFact]
    public async Task Second_Full_Refund_Is_Rejected()
    {
        var ctx = await SeedOrderAsync("Double full", paymentMethod: "card");
        await RefundAsync(ctx, "full", NewKey(), null);

        await Assert.ThrowsAsync<BusinessException>(() => RefundAsync(ctx, "full", NewKey(), null));

        Assert.Equal(1, await CountRefundsAsync(ctx));
    }

    [SkippableFact]
    public async Task Refund_From_Another_Tenant_Is_Not_Found()
    {
        var ctx = await SeedOrderAsync("Owner", paymentMethod: "card");
        var companyB = await SeedCompanyAsync("Refund Attacker");
        var branchB = await SeedBranchAsync(companyB, "Attacker");
        var userB = await SeedUserAsync(companyB, branchB, $"refund-attacker-{Guid.NewGuid():N}@test.com");

        await Assert.ThrowsAsync<NotFoundException>(() => Service.CreateRefundAsync(
            companyB, branchB, userB, NewKey(), new CreateRefundRequest
            {
                OrderId = ctx.OrderId,
                RefundType = "full",
                Reason = "Intento de otro comercio"
            }));
        Assert.Equal(0, await CountRefundsAsync(ctx));
    }

    [SkippableFact]
    public async Task Stock_Failure_Rolls_Back_Refund_Items_And_Order_Status()
    {
        var ctx = await SeedOrderAsync("Stock rollback", paymentMethod: "card");
        var suffix = Guid.NewGuid().ToString("N");
        var functionName = $"fail_refund_stock_{suffix}";
        var triggerName = $"trg_fail_refund_stock_{suffix}";
        using var conn = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        await new NpgsqlCommand($@"
            CREATE FUNCTION inventory.{functionName}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.product_id = {ctx.ProductId} THEN RAISE EXCEPTION 'forced stock failure'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER {triggerName} BEFORE UPDATE ON inventory.stock
            FOR EACH ROW EXECUTE FUNCTION inventory.{functionName}();", conn).ExecuteNonQueryAsync();

        try
        {
            await Assert.ThrowsAsync<PostgresException>(() => RefundAsync(ctx, "partial", NewKey(), 1m));
            Assert.Equal(0, await CountRefundsAsync(ctx));
            Assert.Null(await GetRefundStatusAsync(ctx));
            Assert.Equal(8m, await GetStockAsync(ctx));
            Assert.Equal(0, await CountMovementsAsync(ctx));
        }
        finally
        {
            await new NpgsqlCommand($@"
                DROP TRIGGER IF EXISTS {triggerName} ON inventory.stock;
                DROP FUNCTION IF EXISTS inventory.{functionName}();", conn).ExecuteNonQueryAsync();
        }
    }

    [SkippableFact]
    public async Task Missing_Cash_Register_Rolls_Back_Entire_Cash_Refund()
    {
        var ctx = await SeedOrderAsync("Cash rollback", paymentMethod: "cash");

        await Assert.ThrowsAsync<BusinessException>(() => RefundAsync(ctx, "full", NewKey(), null));

        Assert.Equal(0, await CountRefundsAsync(ctx));
        Assert.Equal(8m, await GetStockAsync(ctx));
        Assert.Null(await GetRefundStatusAsync(ctx));
    }

    [SkippableFact]
    public async Task Credit_Refund_Reduces_Debt_First_And_Returns_Only_Excess_In_Cash()
    {
        var ctx = await SeedOrderAsync("Credit refund", paymentMethod: "cash", amountPaid: 40m, creditBalance: 60m, openRegister: true);

        var result = await RefundAsync(ctx, "partial", NewKey(), 1.4m);
        var credit = await CreditRepository.GetCreditByIdAsync(ctx.CreditId!.Value, ctx.Company, ctx.Branch);
        var register = await CashRegisterRepository.GetByIdAsync(ctx.CashRegisterId!.Value, ctx.Company);

        Assert.Equal(70m, result.RefundAmount);
        Assert.Equal(0m, credit!.CreditAmount);
        Assert.Equal(30m, credit.AmountPaid);
        Assert.Equal(30m, credit.OriginalTotal);
        Assert.Equal("paid", credit.Status);
        Assert.Equal(10m, register!.CashOut);
        Assert.Equal(0m, register.CashIn);
    }

    [SkippableFact]
    public async Task Fully_Credit_Sale_Paid_In_Cash_Refunds_The_Collected_Cash()
    {
        var ctx = await SeedOrderAsync(
            "Credit paid cash", paymentMethod: "cash", amountPaid: 0m, creditBalance: 100m, openRegister: true);
        await PayCreditAsync(ctx, 100m, "cash");

        var result = await RefundAsync(ctx, "full", NewKey(), null);
        var register = await CashRegisterRepository.GetByIdAsync(ctx.CashRegisterId!.Value, ctx.Company);

        Assert.Equal(100m, result.RefundAmount);
        Assert.Equal(100m, register!.CashIn);
        Assert.Equal(100m, register.CashOut);
        Assert.Equal(100m, await GetCashRefundMovementsAsync(ctx));
    }

    [SkippableFact]
    public async Task Fully_Credit_Sale_Paid_By_Card_Does_Not_Alter_Cash()
    {
        var ctx = await SeedOrderAsync(
            "Credit paid card", paymentMethod: "card", amountPaid: 0m, creditBalance: 100m);
        await PayCreditAsync(ctx, 100m, "card");

        var result = await RefundAsync(ctx, "full", NewKey(), null);

        Assert.Equal(100m, result.RefundAmount);
        Assert.Equal(0m, await GetCashRefundMovementsAsync(ctx));
    }

    [SkippableFact]
    public async Task Refund_Uses_Initial_And_Credit_Payment_Methods_Together()
    {
        var ctx = await SeedOrderAsync(
            "Mixed credit payments", paymentMethod: "cash", amountPaid: 40m, creditBalance: 60m, openRegister: true);
        await PayCreditAsync(ctx, 60m, "card");

        await RefundAsync(ctx, "full", NewKey(), null);
        var register = await CashRegisterRepository.GetByIdAsync(ctx.CashRegisterId!.Value, ctx.Company);

        Assert.Equal(40m, register!.CashOut);
        Assert.Equal(40m, await GetCashRefundMovementsAsync(ctx));
    }

    [SkippableFact]
    public async Task Multiple_Refunds_Never_Return_More_Cash_Than_Was_Collected()
    {
        var ctx = await SeedOrderAsync(
            "Multiple mixed refunds", paymentMethod: "cash", amountPaid: 40m, creditBalance: 60m, openRegister: true);
        await PayCreditAsync(ctx, 60m, "card");

        await RefundAsync(ctx, "partial", NewKey(), 1m);
        Assert.Equal(20m, await GetCashRefundMovementsAsync(ctx));

        await RefundAsync(ctx, "partial", NewKey(), 1m);
        var register = await CashRegisterRepository.GetByIdAsync(ctx.CashRegisterId!.Value, ctx.Company);

        Assert.Equal(40m, register!.CashOut);
        Assert.Equal(40m, await GetCashRefundMovementsAsync(ctx));
    }

    [SkippableFact]
    public async Task Historical_Credit_Payment_Without_Method_Is_Rejected_Without_Writes()
    {
        var ctx = await SeedOrderAsync(
            "Historical payment", paymentMethod: "card", amountPaid: 0m, creditBalance: 100m);
        await AddHistoricalCreditPaymentAsync(ctx, 100m);

        await Assert.ThrowsAsync<BusinessException>(() => RefundAsync(ctx, "full", NewKey(), null));

        Assert.Equal(0, await CountRefundsAsync(ctx));
        Assert.Equal(8m, await GetStockAsync(ctx));
        Assert.Null(await GetRefundStatusAsync(ctx));
        Assert.Equal(0m, await GetCashRefundMovementsAsync(ctx));
    }

    [SkippableFact]
    public async Task Refund_Reads_And_Replay_Are_Scoped_To_Authenticated_Branch()
    {
        var ctx = await SeedOrderAsync("Branch scoped refund", paymentMethod: "card");
        var key = NewKey();
        const string reason = "Devolucion limitada a la sucursal autenticada";
        var created = await RefundAsync(ctx, "partial", key, 0.5m, reason);
        var otherBranch = await SeedBranchAsync(ctx.Company, "Other refund branch");
        var otherUser = await SeedUserAsync(
            ctx.Company, otherBranch, $"refund-other-branch-{Guid.NewGuid():N}@test.com");

        Assert.Null(await Service.GetByIdAsync(created.Id, ctx.Company, otherBranch));
        Assert.Empty(await Service.GetByOrderIdAsync(ctx.OrderId, ctx.Company, otherBranch));
        Assert.Single(await Service.GetByOrderIdAsync(ctx.OrderId, ctx.Company, ctx.Branch));

        await Assert.ThrowsAsync<NotFoundException>(() => Service.CreateRefundAsync(
            ctx.Company, otherBranch, otherUser, key, new CreateRefundRequest
            {
                OrderId = ctx.OrderId,
                RefundType = "partial",
                Reason = reason,
                Items = [new RefundItemRequest { OrderItemId = ctx.OrderItemId, Quantity = 0.5m }]
            }));

        Assert.Equal(1, await CountRefundsAsync(ctx));
    }

    [SkippableFact]
    public async Task Discount_Is_Distributed_Deterministically_Using_Net_Value()
    {
        var ctx = await SeedOrderAsync("Discount", paymentMethod: "card", discountAmount: 10m);
        var first = await RefundAsync(ctx, "partial", NewKey(), 1m);
        var second = await RefundAsync(ctx, "partial", NewKey(), 1m);

        Assert.Equal(45m, first.RefundAmount);
        Assert.Equal(45m, second.RefundAmount);
        Assert.Equal(90m, await GetRefundedSubtotalAsync(ctx.OrderItemId));
    }

    [SkippableFact]
    public async Task Idempotency_Is_Replay_Safe_And_Rejects_Different_Payload()
    {
        var ctx = await SeedOrderAsync("Replay", paymentMethod: "card");
        var key = NewKey();
        var first = await RefundAsync(ctx, "partial", key, 0.5m, "Devolucion replay segura");
        var replay = await RefundAsync(ctx, "partial", key, 0.5m, "Devolucion replay segura");

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(1, await CountRefundsAsync(ctx));

        await Assert.ThrowsAsync<BusinessException>(() =>
            RefundAsync(ctx, "partial", key, 0.5m, "Payload diferente para replay"));
        Assert.Equal(1, await CountRefundsAsync(ctx));
        Assert.Equal(0.5m, await GetRefundedQuantityAsync(ctx.OrderItemId));
    }

    private async Task<RefundResponse> RefundAsync(
        RefundTestContext context,
        string type,
        string key,
        decimal? quantity,
        string reason = "Devolucion solicitada por cliente") =>
        await Service.CreateRefundAsync(context.Company, context.Branch, context.User, key,
            new CreateRefundRequest
            {
                OrderId = context.OrderId,
                RefundType = type,
                Reason = reason,
                Items = quantity.HasValue
                    ? [new RefundItemRequest { OrderItemId = context.OrderItemId, Quantity = quantity.Value }]
                    : null
            });

    private async Task<RefundTestContext> SeedOrderAsync(
        string prefix,
        string paymentMethod,
        decimal amountPaid = 100m,
        decimal creditBalance = 0m,
        decimal discountAmount = 0m,
        bool openRegister = false)
    {
        var company = await SeedCompanyAsync($"{prefix} Company");
        var branch = await SeedBranchAsync(company, $"{prefix} Branch");
        var user = await SeedUserAsync(company, branch, $"{prefix.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}@test.com");
        using var conn = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var category = await ScalarAsync(conn, @"
            INSERT INTO inventory.categories (company_id, name, code, is_active, created_by)
            VALUES (@company, @name, @code, true, @user) RETURNING id",
            ("@company", company), ("@name", $"Category {prefix}"), ("@code", $"C{suffix}"), ("@user", user));
        var unit = await ScalarAsync(conn, @"
            INSERT INTO inventory.units (company_id, name, abbreviation, unit_type, is_active, created_by)
            VALUES (@company, @name, @abbr, 'unit', true, @user) RETURNING id",
            ("@company", company), ("@name", $"Unit {prefix}"), ("@abbr", $"u{suffix}"[..8]), ("@user", user));
        var product = await ScalarAsync(conn, @"
            INSERT INTO inventory.products (
                company_id, name, sku, category_id, unit_id, cost_price, sale_price,
                min_stock, max_stock, reorder_point, is_perishable, product_type,
                track_stock, is_for_sale, is_active, created_by
            ) VALUES (
                @company, @name, @sku, @category, @unit, 20, 50,
                0, 100, 0, false, 'simple', true, true, true, @user
            ) RETURNING id",
            ("@company", company), ("@name", $"Product {prefix}"), ("@sku", $"SKU-{suffix}"),
            ("@category", category), ("@unit", unit), ("@user", user));
        await ExecuteAsync(conn, @"
            INSERT INTO inventory.stock (company_id, branch_id, product_id, quantity, reserved_quantity)
            VALUES (@company, @branch, @product, 8, 0)",
            ("@company", company), ("@branch", branch), ("@product", product));
        var table = await ScalarAsync(conn, @"
            INSERT INTO sales.tables (company_id, branch_id, table_number, name, status, created_by)
            VALUES (@company, @branch, @number, @name, 'invoiced', @user) RETURNING id",
            ("@company", company), ("@branch", branch), ("@number", Random.Shared.Next(10000, 99999)),
            ("@name", $"Table {prefix}"), ("@user", user));

        long? cashRegisterId = null;
        if (openRegister)
        {
            var register = await CashRegisterRepository.OpenAsync(new CashRegister
            {
                CompanyId = company, BranchId = branch, OpenedBy = user,
                Status = "open", OpeningAmount = 100m, OpenedAt = DateTime.UtcNow
            });
            cashRegisterId = register.Id;
        }

        var netTotal = 100m - discountAmount;
        var order = await ScalarAsync(conn, @"
            INSERT INTO sales.orders (
                company_id, branch_id, table_id, order_number, status,
                subtotal, tax, total, discount_amount, final_total_paid,
                split_reference_count, created_by, payment_method, cash_register_id
            ) VALUES (
                @company, @branch, @table, @number, 'completed',
                100, 0, 100, @discount, @paid,
                1, @user, @method, @register
            ) RETURNING id",
            ("@company", company), ("@branch", branch), ("@table", table),
            ("@number", $"ORD-{suffix}"), ("@discount", discountAmount), ("@paid", amountPaid),
            ("@user", user), ("@method", paymentMethod), ("@register", cashRegisterId ?? (object)DBNull.Value));
        var orderItem = await ScalarAsync(conn, @"
            INSERT INTO sales.order_items (
                company_id, order_id, product_id, product_name, quantity, unit_price
            ) VALUES (@company, @order, @product, @name, 2, 50) RETURNING id",
            ("@company", company), ("@order", order), ("@product", product), ("@name", $"Product {prefix}"));

        if (amountPaid > 0)
        {
            await ExecuteAsync(conn, @"
                INSERT INTO sales.order_payments (company_id, order_id, method, amount, created_at)
                VALUES (@company, @order, @method, @amount, NOW())",
                ("@company", company), ("@order", order), ("@method", paymentMethod), ("@amount", amountPaid));
        }

        long? creditId = null;
        if (creditBalance > 0)
        {
            creditId = await ScalarAsync(conn, @"
                INSERT INTO sales.credits (
                    company_id, branch_id, order_id, customer_name, order_number,
                    original_total, amount_paid, credit_amount, status, created_by
                ) VALUES (
                    @company, @branch, @order, 'Cliente credito', @number,
                    @total, @paid, @credit, 'partial', @user
                ) RETURNING id",
                ("@company", company), ("@branch", branch), ("@order", order),
                ("@number", $"ORD-{suffix}"), ("@total", netTotal), ("@paid", amountPaid),
                ("@credit", creditBalance), ("@user", user));
        }

        return new RefundTestContext(company, branch, user, order, orderItem, product, creditId, cashRegisterId);
    }

    private async Task PayCreditAsync(RefundTestContext context, decimal amount, string paymentMethod)
    {
        await CreditRepository.ProcessPaymentAsync(new CreditPaymentCommand
        {
            CreditId = context.CreditId!.Value,
            CompanyId = context.Company,
            BranchId = context.Branch,
            UserId = context.User,
            Amount = amount,
            PaymentMethod = paymentMethod,
            Notes = "Abono de prueba para devolucion"
        });
    }

    private async Task AddHistoricalCreditPaymentAsync(RefundTestContext context, decimal amount)
    {
        using var conn = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        await ExecuteAsync(conn, @"
            INSERT INTO sales.credit_payments (
                company_id, credit_id, amount, payment_method, notes, created_by, created_at
            ) VALUES (
                @company, @credit, @amount, NULL, 'Pago historico sin metodo', @user, NOW()
            );

            UPDATE sales.credits
            SET amount_paid = amount_paid + @amount,
                credit_amount = credit_amount - @amount,
                status = 'paid',
                paid_at = NOW(),
                updated_at = NOW()
            WHERE id = @credit AND company_id = @company AND branch_id = @branch",
            ("@company", context.Company), ("@branch", context.Branch),
            ("@credit", context.CreditId!.Value), ("@amount", amount), ("@user", context.User));
    }

    private async Task<decimal> GetStockAsync(RefundTestContext ctx) =>
        await DecimalScalarAsync("SELECT quantity FROM inventory.stock WHERE company_id=@company AND branch_id=@branch AND product_id=@product",
            ("@company", ctx.Company), ("@branch", ctx.Branch), ("@product", ctx.ProductId));

    private async Task<string?> GetRefundStatusAsync(RefundTestContext ctx)
    {
        using var conn = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand("SELECT refund_status FROM sales.orders WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", ctx.OrderId);
        return await cmd.ExecuteScalarAsync() as string;
    }

    private async Task<int> CountRefundsAsync(RefundTestContext ctx) =>
        Convert.ToInt32(await DecimalScalarAsync("SELECT COUNT(*) FROM sales.refunds WHERE order_id=@order", ("@order", ctx.OrderId)));
    private async Task<int> CountMovementsAsync(RefundTestContext ctx) =>
        Convert.ToInt32(await DecimalScalarAsync("SELECT COUNT(*) FROM inventory.movements WHERE reference_type='refund' AND product_id=@product", ("@product", ctx.ProductId)));
    private Task<decimal> GetRefundedQuantityAsync(long itemId) =>
        DecimalScalarAsync("SELECT COALESCE(SUM(quantity),0) FROM sales.refund_items WHERE order_item_id=@item", ("@item", itemId));
    private Task<decimal> GetRefundedSubtotalAsync(long itemId) =>
        DecimalScalarAsync("SELECT COALESCE(SUM(subtotal),0) FROM sales.refund_items WHERE order_item_id=@item", ("@item", itemId));
    private Task<decimal> GetCashRefundMovementsAsync(RefundTestContext context) =>
        DecimalScalarAsync(@"
            SELECT COALESCE(SUM(cm.amount), 0)
            FROM sales.cash_movements cm
            JOIN sales.refunds r
              ON r.company_id = cm.company_id
             AND cm.notes = ('Refund #' || r.id::text)
            WHERE r.order_id = @order
              AND cm.type = 'out'
              AND cm.reason = 'Devolucion de venta'", ("@order", context.OrderId));

    private async Task<decimal> DecimalScalarAsync(string sql, params (string Name, object Value)[] args)
    {
        using var conn = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var arg in args) cmd.Parameters.AddWithValue(arg.Name, arg.Value);
        return Convert.ToDecimal(await cmd.ExecuteScalarAsync());
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection conn, string sql, params (string Name, object Value)[] args)
    {
        using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var arg in args) cmd.Parameters.AddWithValue(arg.Name, arg.Value);
        return (long)(await cmd.ExecuteScalarAsync() ?? throw new InvalidOperationException("Seed failed"));
    }

    private static async Task ExecuteAsync(NpgsqlConnection conn, string sql, params (string Name, object Value)[] args)
    {
        using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var arg in args) cmd.Parameters.AddWithValue(arg.Name, arg.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string NewKey() => $"refund-{Guid.NewGuid():N}";

    private sealed record RefundTestContext(
        long Company,
        long Branch,
        long User,
        long OrderId,
        long OrderItemId,
        long ProductId,
        long? CreditId,
        long? CashRegisterId);
}
