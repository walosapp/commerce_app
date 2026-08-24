using Npgsql;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Integration;

public class CheckoutRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Cash_Checkout_Commits_All_Aggregates()
    {
        var ctx = await SeedCheckoutAsync("Cash");
        var result = await CheckoutRepository.ProcessAsync(Command(ctx, [new("cash", 100m, null)]));

        Assert.False(result.IsReplay);
        Assert.Equal(9m, await StockAsync(ctx.Branch, ctx.Product));
        Assert.Equal(1, await CountAsync("inventory.movements", "reference_id", ctx.Order));
        Assert.Equal(1, await CountAsync("sales.order_payments", "order_id", ctx.Order));
        Assert.Equal("completed", await ScalarAsync<string>("SELECT status FROM sales.orders WHERE id=@id", ctx.Order));
        Assert.Equal("invoiced", await ScalarAsync<string>("SELECT status FROM sales.tables WHERE id=@id", ctx.Table));
        Assert.Equal(100m, await ScalarAsync<decimal>("SELECT total_cash_sales FROM sales.cash_registers WHERE id=@id", ctx.Register!.Value));
    }

    [SkippableFact]
    public async Task Multiple_Payments_Are_Persisted_And_Classified()
    {
        var ctx = await SeedCheckoutAsync("Mixed");
        await CheckoutRepository.ProcessAsync(Command(ctx,
            [new("cash", 40m, null), new("card", 60m, "AUTH") ]));

        Assert.Equal(2, await CountAsync("sales.order_payments", "order_id", ctx.Order));
        Assert.Equal(40m, await ScalarAsync<decimal>("SELECT total_cash_sales FROM sales.cash_registers WHERE id=@id", ctx.Register!.Value));
        Assert.Equal(60m, await ScalarAsync<decimal>("SELECT total_card_sales FROM sales.cash_registers WHERE id=@id", ctx.Register.Value));
    }

    [SkippableFact]
    public async Task Partial_Credit_Is_Created_Without_Counting_Debt_As_Cash()
    {
        var ctx = await SeedCheckoutAsync("Credit");
        var command = Command(ctx, [new("cash", 40m, null)]) with
        {
            HasCredit = true,
            CreditAmountPaid = 40m,
            CreditCustomerName = "Cliente"
        };

        var result = await CheckoutRepository.ProcessAsync(command);

        Assert.Equal(60m, result.CreditAmount);
        Assert.Equal(60m, await ScalarAsync<decimal>("SELECT credit_amount FROM sales.credits WHERE order_id=@id", ctx.Order));
        Assert.Equal(40m, await ScalarAsync<decimal>("SELECT total_cash_sales FROM sales.cash_registers WHERE id=@id", ctx.Register!.Value));
        Assert.Equal(60m, await ScalarAsync<decimal>("SELECT total_credits FROM sales.cash_registers WHERE id=@id", ctx.Register.Value));
    }

    [SkippableFact]
    public async Task Prepared_Product_Deducts_Recipe_Ingredient()
    {
        var ctx = await SeedCheckoutAsync("Recipe", prepared: true, ingredientStock: 5m);
        await CheckoutRepository.ProcessAsync(Command(ctx, [new("cash", 100m, null)]));

        Assert.Equal(3m, await StockAsync(ctx.Branch, ctx.Ingredient!.Value));
        Assert.Equal("recipe_consumption", await ScalarAsync<string>(
            "SELECT movement_type FROM inventory.movements WHERE reference_id=@id", ctx.Order));
    }

    [SkippableFact]
    public async Task Inventory_Failure_Rolls_Back_Everything()
    {
        var ctx = await SeedCheckoutAsync("InventoryRollback");
        await AssertRollbackWithTriggerAsync(ctx, "inventory.movements", "INSERT", async () =>
            await CheckoutRepository.ProcessAsync(Command(ctx, [new("cash", 100m, null)])));
    }

    [SkippableFact]
    public async Task Payment_Failure_Rolls_Back_Everything()
    {
        var ctx = await SeedCheckoutAsync("PaymentRollback");
        await AssertRollbackWithTriggerAsync(ctx, "sales.order_payments", "INSERT", async () =>
            await CheckoutRepository.ProcessAsync(Command(ctx, [new("cash", 100m, null)])));
    }

    [SkippableFact]
    public async Task Cash_Register_Failure_Rolls_Back_Everything()
    {
        var ctx = await SeedCheckoutAsync("CashRollback");
        await AssertRollbackWithTriggerAsync(ctx, "sales.cash_registers", "UPDATE", async () =>
            await CheckoutRepository.ProcessAsync(Command(ctx, [new("cash", 100m, null)])));
    }

    [SkippableFact]
    public async Task Credit_Failure_Rolls_Back_Everything()
    {
        var ctx = await SeedCheckoutAsync("CreditRollback");
        var command = Command(ctx, [new("cash", 40m, null)]) with
        {
            HasCredit = true,
            CreditAmountPaid = 40m,
            CreditCustomerName = "Cliente"
        };
        await AssertRollbackWithTriggerAsync(ctx, "sales.credits", "INSERT", async () =>
            await CheckoutRepository.ProcessAsync(command));
    }

    [SkippableFact]
    public async Task Concurrent_Checkouts_Execute_Only_Once()
    {
        var ctx = await SeedCheckoutAsync("Concurrent");
        var command = Command(ctx, [new("cash", 40m, null)]) with
        {
            HasCredit = true,
            CreditAmountPaid = 40m,
            CreditCustomerName = "Concurrent Customer"
        };

        var results = await Task.WhenAll(
            CheckoutRepository.ProcessAsync(command),
            CheckoutRepository.ProcessAsync(command));

        Assert.Single(results.Where(result => !result.IsReplay));
        Assert.Single(results.Where(result => result.IsReplay));
        Assert.Equal(1, await CountAsync("inventory.movements", "reference_id", ctx.Order));
        Assert.Equal(1, await CountAsync("sales.order_payments", "order_id", ctx.Order));
        Assert.Equal(1, await CountAsync("sales.credits", "order_id", ctx.Order));
        Assert.Equal(1, await ScalarAsync<int>("SELECT order_count FROM sales.cash_registers WHERE id=@id", ctx.Register!.Value));
    }

    [SkippableFact]
    public async Task Same_Request_Replays_But_Different_Request_Is_Rejected()
    {
        var ctx = await SeedCheckoutAsync("Replay");
        var command = Command(ctx, [new("cash", 100m, null)]);
        var first = await CheckoutRepository.ProcessAsync(command);
        var replay = await CheckoutRepository.ProcessAsync(command);

        Assert.False(first.IsReplay);
        Assert.True(replay.IsReplay);
        await Assert.ThrowsAsync<BusinessException>(() => CheckoutRepository.ProcessAsync(
            Command(ctx, [new("card", 100m, null)])));
        Assert.Equal(1, await CountAsync("inventory.movements", "reference_id", ctx.Order));
        Assert.Equal(1, await CountAsync("sales.order_payments", "order_id", ctx.Order));
    }

    [SkippableFact]
    public async Task Other_Tenant_And_Other_Branch_Are_Rejected_Without_Writes()
    {
        var ctx = await SeedCheckoutAsync("Scope");
        var otherCompany = await SeedCompanyAsync("Other checkout company");
        var otherBranch = await SeedBranchAsync(otherCompany, "Other branch");
        var otherUser = await SeedUserAsync(otherCompany, otherBranch, $"other-{Guid.NewGuid():N}@test.com");
        var sameCompanyOtherBranch = await SeedBranchAsync(ctx.Company, "Wrong same-tenant branch");

        await Assert.ThrowsAsync<NotFoundException>(() => CheckoutRepository.ProcessAsync(
            Command(ctx, [new("cash", 100m, null)]) with
            { CompanyId = otherCompany, BranchId = otherBranch, UserId = otherUser }));
        await Assert.ThrowsAsync<NotFoundException>(() => CheckoutRepository.ProcessAsync(
            Command(ctx, [new("cash", 100m, null)]) with { BranchId = sameCompanyOtherBranch }));
        Assert.Equal(0, await CountAsync("inventory.movements", "reference_id", ctx.Order));
    }

    [SkippableFact]
    public async Task Conditional_Table_Update_Failure_Rolls_Back_All_Prior_Writes()
    {
        var ctx = await SeedCheckoutAsync("AffectedRows");
        var suffix = Guid.NewGuid().ToString("N");
        var function = $"skip_checkout_table_{suffix}";
        var trigger = $"trg_skip_checkout_table_{suffix}";
        await using var connection = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        await new NpgsqlCommand($@"
            CREATE FUNCTION sales.{function}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.company_id = {ctx.Company} THEN RETURN NULL; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER {trigger} BEFORE UPDATE ON sales.tables
            FOR EACH ROW EXECUTE FUNCTION sales.{function}();", connection).ExecuteNonQueryAsync();
        try
        {
            await Assert.ThrowsAsync<BusinessException>(() => CheckoutRepository.ProcessAsync(
                Command(ctx, [new("cash", 100m, null)])));
            await AssertOpenAndUntouchedAsync(ctx);
            Assert.Equal(10m, await StockAsync(ctx.Branch, ctx.Product));
        }
        finally
        {
            await new NpgsqlCommand($@"
                DROP TRIGGER IF EXISTS {trigger} ON sales.tables;
                DROP FUNCTION IF EXISTS sales.{function}();", connection).ExecuteNonQueryAsync();
        }
    }

    [SkippableFact]
    public async Task Insufficient_Direct_Stock_Rolls_Back()
    {
        var ctx = await SeedCheckoutAsync("NoStock", stock: 0.5m);
        await Assert.ThrowsAsync<BusinessException>(() => CheckoutRepository.ProcessAsync(
            Command(ctx, [new("cash", 100m, null)])));
        await AssertOpenAndUntouchedAsync(ctx);
    }

    [SkippableFact]
    public async Task Insufficient_Recipe_Ingredient_Rolls_Back()
    {
        var ctx = await SeedCheckoutAsync("NoIngredient", prepared: true, ingredientStock: 1m);
        await Assert.ThrowsAsync<BusinessException>(() => CheckoutRepository.ProcessAsync(
            Command(ctx, [new("cash", 100m, null)])));
        await AssertOpenAndUntouchedAsync(ctx);
    }

    [SkippableFact]
    public async Task Payments_Are_Persisted_When_Register_Is_Not_Required()
    {
        var ctx = await SeedCheckoutAsync("NoRegister", requireRegister: false, openRegister: false);
        await CheckoutRepository.ProcessAsync(Command(ctx, [new("transfer", 100m, "TRX") ]));
        Assert.Equal(1, await CountAsync("sales.order_payments", "order_id", ctx.Order));
    }

    [SkippableFact]
    public async Task Optional_Open_Register_Is_Used_When_Register_Is_Not_Required()
    {
        var ctx = await SeedCheckoutAsync("OptionalRegister", requireRegister: false, openRegister: true);
        await CheckoutRepository.ProcessAsync(Command(ctx, [new("cash", 100m, null)]));

        Assert.Equal(100m, await ScalarAsync<decimal>(
            "SELECT total_cash_sales FROM sales.cash_registers WHERE id=@id", ctx.Register!.Value));
        Assert.Equal(ctx.Register, await ScalarAsync<long>(
            "SELECT cash_register_id FROM sales.orders WHERE id=@id", ctx.Order));
    }

    [SkippableFact]
    public async Task Unknown_Or_Nonpositive_Payment_Is_Rejected_Without_Writes()
    {
        var ctx = await SeedCheckoutAsync("InvalidMethod");
        await Assert.ThrowsAsync<ValidationException>(() => CheckoutRepository.ProcessAsync(
            Command(ctx, [new("bitcoin", 100m, null)])));
        await AssertOpenAndUntouchedAsync(ctx);

        await Assert.ThrowsAsync<ValidationException>(() => CheckoutRepository.ProcessAsync(
            Command(ctx, [new("cash", 0m, null)])));
        await AssertOpenAndUntouchedAsync(ctx);
    }

    [SkippableFact]
    public async Task Subcent_Payment_Rounded_To_Zero_Is_Rejected()
    {
        var ctx = await SeedCheckoutAsync("Subcent", price: 0.004m);
        await Assert.ThrowsAsync<ValidationException>(() => CheckoutRepository.ProcessAsync(
            Command(ctx, [new("cash", 0.004m, null)])));
        await AssertOpenAndUntouchedAsync(ctx);
    }

    [SkippableFact]
    public async Task Included_Tip_Is_Persisted_And_Counts_In_Cash_But_Not_Net_Sales()
    {
        var ctx = await SeedCheckoutAsync("Tip");
        var result = await CheckoutRepository.ProcessAsync(Command(ctx, [new("cash", 110m, null)]) with
        {
            TipIncluded = true,
            TipAmount = 10m
        });

        Assert.Equal(10m, result.TipAmount);
        Assert.Equal(100m, await ScalarAsync<decimal>("SELECT total_sales FROM sales.cash_registers WHERE id=@id", ctx.Register!.Value));
        Assert.Equal(110m, await ScalarAsync<decimal>("SELECT total_cash_sales FROM sales.cash_registers WHERE id=@id", ctx.Register.Value));
        Assert.Equal(10m, await ScalarAsync<decimal>("SELECT total_tips FROM sales.cash_registers WHERE id=@id", ctx.Register.Value));
    }

    [SkippableFact]
    public async Task Full_Credit_Needs_No_Payment_Lines()
    {
        var ctx = await SeedCheckoutAsync("FullCredit");
        var result = await CheckoutRepository.ProcessAsync(Command(ctx, []) with
        {
            HasCredit = true,
            CreditAmountPaid = 0,
            CreditCustomerName = "Cliente fiado"
        });

        Assert.Equal(0m, result.AmountPaid);
        Assert.Equal(100m, result.CreditAmount);
        Assert.Equal(0, await CountAsync("sales.order_payments", "order_id", ctx.Order));
        Assert.Equal(100m, await ScalarAsync<decimal>("SELECT total_credits FROM sales.cash_registers WHERE id=@id", ctx.Register!.Value));
    }

    [SkippableFact]
    public async Task Discount_Override_Boundary_Is_Enforced()
    {
        var ctx = await SeedCheckoutAsync("Override", manualDiscounts: true);
        await ExecuteAsync(@"
            UPDATE core.companies
            SET discount_requires_override=true,
                discount_override_threshold_percent=10,
                max_discount_percent=10
            WHERE id=@company", new { company = ctx.Company });

        var request = Command(ctx, [new("cash", 90m, null)]) with
        {
            DiscountType = "percentage",
            DiscountValue = 10m
        };
        await Assert.ThrowsAsync<ValidationException>(() => CheckoutRepository.ProcessAsync(request));
        await AssertOpenAndUntouchedAsync(ctx);

        var result = await CheckoutRepository.ProcessAsync(request with { OverrideConfirmed = true });
        Assert.Equal(10m, result.DiscountAmount);
    }

    [SkippableFact]
    public async Task Replay_Remains_Valid_After_Credit_And_Order_Audit_Mutation()
    {
        var ctx = await SeedCheckoutAsync("CreditReplay");
        var command = Command(ctx, [new("cash", 40m, null)]) with
        {
            HasCredit = true,
            CreditAmountPaid = 40m,
            CreditCustomerName = "Cliente"
        };
        var first = await CheckoutRepository.ProcessAsync(command);
        await ExecuteAsync(@"
            UPDATE sales.credits
            SET amount_paid=70, credit_amount=30, status='partial', updated_at=NOW()
            WHERE order_id=@order;
            UPDATE sales.orders SET updated_at=NOW() + INTERVAL '1 hour' WHERE id=@order;",
            new { order = ctx.Order });

        var replay = await CheckoutRepository.ProcessAsync(command);
        Assert.True(replay.IsReplay);
        Assert.Equal(first.CreditId, replay.CreditId);
        Assert.Equal(60m, replay.CreditAmount);
        Assert.Equal(first.InvoicedAt, replay.InvoicedAt);
    }

    [SkippableFact]
    public async Task UpdateItemQuantity_Rejects_Zero_WithoutWrites()
    {
        var ctx = await SeedCheckoutAsync("ZeroQuantity");
        var itemId = await ScalarAsync<long>(
            "SELECT id FROM sales.order_items WHERE order_id=@id", ctx.Order);
        var totalBefore = await ScalarAsync<decimal>(
            "SELECT total FROM sales.orders WHERE id=@id", ctx.Order);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CheckoutRepository.UpdateItemQuantityAsync(new UpdateOrderItemQuantityCommand
            {
                CompanyId = ctx.Company,
                BranchId = ctx.Branch,
                OrderItemId = itemId,
                Quantity = 0
            }));

        Assert.Equal(1m, await ScalarAsync<decimal>(
            "SELECT quantity FROM sales.order_items WHERE id=@id", itemId));
        Assert.Equal(totalBefore, await ScalarAsync<decimal>(
            "SELECT total FROM sales.orders WHERE id=@id", ctx.Order));
    }

    [SkippableFact]
    public async Task Closed_Order_Rejects_Add_And_Update_Item_Mutations()
    {
        var ctx = await SeedCheckoutAsync("PostClose");
        var itemId = await ScalarAsync<long>("SELECT id FROM sales.order_items WHERE order_id=@id", ctx.Order);
        await CheckoutRepository.ProcessAsync(Command(ctx, [new("cash", 100m, null)]));

        await Assert.ThrowsAsync<BusinessException>(() => CheckoutRepository.AddItemsAsync(new AddOrderItemsCommand
        {
            CompanyId = ctx.Company,
            BranchId = ctx.Branch,
            TableId = ctx.Table,
            Items = [new OrderItem { ProductId = ctx.Product, ProductName = "X", Quantity = 1, UnitPrice = 100 }]
        }));
        await Assert.ThrowsAsync<BusinessException>(() => CheckoutRepository.UpdateItemQuantityAsync(
            new UpdateOrderItemQuantityCommand
            {
                CompanyId = ctx.Company,
                BranchId = ctx.Branch,
                OrderItemId = itemId,
                Quantity = 2
            }));

        Assert.Equal(1m, await ScalarAsync<decimal>("SELECT quantity FROM sales.order_items WHERE id=@id", itemId));
    }

    [SkippableFact]
    public async Task Item_Mutation_Waits_For_Table_Lock_And_Rechecks_Terminal_State()
    {
        var ctx = await SeedCheckoutAsync("MutationRace");
        await using var connection = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await new NpgsqlCommand($"SELECT id FROM sales.tables WHERE id={ctx.Table} FOR UPDATE", connection, transaction)
            .ExecuteScalarAsync();

        var mutation = CheckoutRepository.AddItemsAsync(new AddOrderItemsCommand
        {
            CompanyId = ctx.Company,
            BranchId = ctx.Branch,
            TableId = ctx.Table,
            Items = [new OrderItem { ProductId = ctx.Product, ProductName = "X", Quantity = 1, UnitPrice = 100 }]
        });
        Assert.NotSame(mutation, await Task.WhenAny(mutation, Task.Delay(100)));

        await new NpgsqlCommand($@"
            UPDATE sales.orders SET status='completed' WHERE id={ctx.Order};
            UPDATE sales.tables SET status='invoiced', updated_at=NOW() WHERE id={ctx.Table};",
            connection, transaction).ExecuteNonQueryAsync();
        await transaction.CommitAsync();

        await Assert.ThrowsAsync<BusinessException>(() => mutation);
        Assert.Equal(1m, await ScalarAsync<decimal>(
            "SELECT quantity FROM sales.order_items WHERE order_id=@id", ctx.Order));
    }

    [SkippableFact]
    public async Task Discount_Preserves_Gross_Total_For_Refund_Net_Calculation()
    {
        var ctx = await SeedCheckoutAsync("Discount", manualDiscounts: true);
        await CheckoutRepository.ProcessAsync(Command(ctx, [new("cash", 90m, null)]) with
        {
            DiscountType = "fixed",
            DiscountValue = 10m
        });

        Assert.Equal(100m, await ScalarAsync<decimal>("SELECT total FROM sales.orders WHERE id=@id", ctx.Order));
        Assert.Equal(90m, await ScalarAsync<decimal>("SELECT final_total_paid FROM sales.orders WHERE id=@id", ctx.Order));
        Assert.Equal(10m, await ScalarAsync<decimal>("SELECT discount_amount FROM sales.orders WHERE id=@id", ctx.Order));
    }

    private async Task AssertRollbackWithTriggerAsync(
        CheckoutContext ctx, string table, string operation, Func<Task> action)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var function = $"fail_checkout_{suffix}";
        var trigger = $"trg_fail_checkout_{suffix}";
        await using var connection = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        var trackedProduct = ctx.Ingredient ?? ctx.Product;
        var stockBefore = await StockAsync(ctx.Branch, trackedProduct);
        await new NpgsqlCommand($@"
            CREATE FUNCTION sales.{function}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.company_id = {ctx.Company} THEN RAISE EXCEPTION 'forced checkout failure'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER {trigger} BEFORE {operation} ON {table}
            FOR EACH ROW EXECUTE FUNCTION sales.{function}();", connection).ExecuteNonQueryAsync();
        try
        {
            await Assert.ThrowsAsync<PostgresException>(action);
            await AssertOpenAndUntouchedAsync(ctx);
            Assert.Equal(stockBefore, await StockAsync(ctx.Branch, trackedProduct));
        }
        finally
        {
            await new NpgsqlCommand($@"
                DROP TRIGGER IF EXISTS {trigger} ON {table};
                DROP FUNCTION IF EXISTS sales.{function}();", connection).ExecuteNonQueryAsync();
        }
    }

    private async Task AssertOpenAndUntouchedAsync(CheckoutContext ctx)
    {
        Assert.Equal("pending", await ScalarAsync<string>("SELECT status FROM sales.orders WHERE id=@id", ctx.Order));
        Assert.Equal("open", await ScalarAsync<string>("SELECT status FROM sales.tables WHERE id=@id", ctx.Table));
        Assert.Equal(0, await CountAsync("sales.order_payments", "order_id", ctx.Order));
        Assert.Equal(0, await CountAsync("sales.credits", "order_id", ctx.Order));
        Assert.Equal(0, await CountAsync("inventory.movements", "reference_id", ctx.Order));
        if (ctx.Register.HasValue)
            Assert.Equal(0, await ScalarAsync<int>("SELECT order_count FROM sales.cash_registers WHERE id=@id", ctx.Register.Value));
    }

    private static CheckoutCommand Command(CheckoutContext ctx, IReadOnlyList<CheckoutPayment> payments) => new()
    {
        CompanyId = ctx.Company,
        BranchId = ctx.Branch,
        UserId = ctx.User,
        TableId = ctx.Table,
        Payments = payments
    };

    private async Task<CheckoutContext> SeedCheckoutAsync(
        string prefix,
        decimal stock = 10m,
        bool prepared = false,
        decimal ingredientStock = 10m,
        bool requireRegister = true,
        bool openRegister = true,
        bool manualDiscounts = false,
        decimal price = 100m)
    {
        var company = await SeedCompanyAsync($"{prefix} Checkout Company");
        var branch = await SeedBranchAsync(company, $"{prefix} Branch");
        var user = await SeedUserAsync(company, branch, $"{prefix.ToLowerInvariant()}-{Guid.NewGuid():N}@test.com");
        await ExecuteAsync(@"
            UPDATE core.companies
            SET require_cash_register=@requireRegister,
                manual_discount_enabled=@manualDiscounts,
                max_discount_amount=1000,
                max_discount_percent=100
            WHERE id=@company", new { company, requireRegister, manualDiscounts });

        var category = await ScalarAsync<long>(@"
            INSERT INTO inventory.categories (company_id,name,code,is_active)
            VALUES (@company,'Test',@code,true) RETURNING id", company,
            new NpgsqlParameter("@company", company), new NpgsqlParameter("@code", $"C{Guid.NewGuid():N}"[..20]));
        var unit = await ScalarAsync<long>(@"
            INSERT INTO inventory.units (company_id,name,abbreviation,unit_type,is_active)
            VALUES (@company,'Unidad',@abbr,'unit',true) RETURNING id", company,
            new NpgsqlParameter("@company", company), new NpgsqlParameter("@abbr", $"U{Guid.NewGuid():N}"[..8]));
        var product = await InsertProductAsync(
            company, category, unit, $"{prefix} Product", prepared ? "prepared" : "simple", !prepared, price);
        if (!prepared)
            await InsertStockAsync(company, branch, product, stock);

        long? ingredient = null;
        if (prepared)
        {
            ingredient = await InsertProductAsync(company, category, unit, $"{prefix} Ingredient", "supply", true);
            await InsertStockAsync(company, branch, ingredient.Value, ingredientStock);
            await ExecuteAsync(@"
                INSERT INTO inventory.recipes (company_id,product_id,ingredient_id,quantity,unit_id)
                VALUES (@company,@product,@ingredient,2,@unit)", new { company, product, ingredient, unit });
        }

        var table = await ScalarAsync<long>(@"
            INSERT INTO sales.tables (company_id,branch_id,table_number,name,status,created_by)
            VALUES (@company,@branch,1,@name,'open',@user) RETURNING id", company,
            new NpgsqlParameter("@company", company), new NpgsqlParameter("@branch", branch),
            new NpgsqlParameter("@name", $"Mesa {prefix}"), new NpgsqlParameter("@user", user));
        var order = await ScalarAsync<long>(@"
            INSERT INTO sales.orders (company_id,branch_id,table_id,order_number,status,subtotal,total,final_total_paid,created_by)
            VALUES (@company,@branch,@table,@number,'pending',@price,@price,@price,@user) RETURNING id", company,
            new NpgsqlParameter("@company", company), new NpgsqlParameter("@branch", branch),
            new NpgsqlParameter("@table", table), new NpgsqlParameter("@number", $"ORD-{Guid.NewGuid():N}"[..30]),
            new NpgsqlParameter("@user", user), new NpgsqlParameter("@price", price));
        await ExecuteAsync(@"
            INSERT INTO sales.order_items (company_id,order_id,product_id,product_name,quantity,unit_price)
            VALUES (@company,@order,@product,@name,1,@price)",
            new { company, order, product, name = $"{prefix} Product", price });

        long? register = null;
        if (openRegister)
        {
            var opened = await CashRegisterRepository.OpenAsync(new CashRegister
            {
                CompanyId = company,
                BranchId = branch,
                OpenedBy = user,
                Status = "open",
                OpeningAmount = 0,
                OpenedAt = DateTime.UtcNow
            });
            register = opened.Id;
        }

        return new CheckoutContext(company, branch, user, table, order, product, ingredient, register);
    }

    private async Task<long> InsertProductAsync(
        long company, long category, long unit, string name, string type, bool trackStock, decimal price = 100m) =>
        await ScalarAsync<long>(@"
            INSERT INTO inventory.products (
                company_id,name,sku,category_id,unit_id,cost_price,sale_price,
                product_type,track_stock,is_active,is_for_sale
            ) VALUES (@company,@name,@sku,@category,@unit,20,@price,@type,@trackStock,true,true)
            RETURNING id", company,
            new NpgsqlParameter("@company", company), new NpgsqlParameter("@name", name),
            new NpgsqlParameter("@sku", $"SKU-{Guid.NewGuid():N}"[..30]), new NpgsqlParameter("@category", category),
            new NpgsqlParameter("@unit", unit), new NpgsqlParameter("@type", type),
            new NpgsqlParameter("@trackStock", trackStock), new NpgsqlParameter("@price", price));

    private Task InsertStockAsync(long company, long branch, long product, decimal quantity) =>
        ExecuteAsync(@"
            INSERT INTO inventory.stock (company_id,branch_id,product_id,quantity)
            VALUES (@company,@branch,@product,@quantity)", new { company, branch, product, quantity });

    private async Task<decimal> StockAsync(long branch, long product) =>
        await ScalarAsync<decimal>(
            "SELECT quantity FROM inventory.stock WHERE branch_id=@branch AND product_id=@product",
            product, new NpgsqlParameter("@branch", branch), new NpgsqlParameter("@product", product));

    private async Task<int> CountAsync(string table, string column, long id) =>
        await ScalarAsync<int>($"SELECT COUNT(*) FROM {table} WHERE {column}=@id", id);

    private async Task<T> ScalarAsync<T>(string sql, long id) =>
        await ScalarAsync<T>(sql, id, new NpgsqlParameter("@id", id));

    private async Task<T> ScalarAsync<T>(string sql, long _, params NpgsqlParameter[] parameters)
    {
        await using var connection = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        return (T)Convert.ChangeType(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException(), typeof(T));
    }

    private async Task ExecuteAsync(string sql, object values)
    {
        await using var connection = (NpgsqlConnection)await ConnectionFactory.CreateConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var property in values.GetType().GetProperties())
            command.Parameters.AddWithValue($"@{property.Name}", property.GetValue(values) ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record CheckoutContext(
        long Company,
        long Branch,
        long User,
        long Table,
        long Order,
        long Product,
        long? Ingredient,
        long? Register);
}
