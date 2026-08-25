using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;
using Walos.Domain.Policies;

namespace Walos.Infrastructure.Repositories;

public sealed class CheckoutRepository : ICheckoutRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<CheckoutRepository> _logger;

    public CheckoutRepository(IDbConnectionFactory connectionFactory, ILogger<CheckoutRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<CheckoutResult> ProcessAsync(CheckoutCommand command)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            var table = await LockTableAsync(connection, transaction, command)
                ?? throw new NotFoundException("Mesa");
            var order = await LockOrderAsync(connection, transaction, command)
                ?? throw new BusinessException("No hay orden asociada a esta mesa");
            var items = await LoadItemsAsync(connection, transaction, order.Id, command.CompanyId);
            if (items.Count == 0)
                throw new BusinessException("La orden no tiene productos");

            var operations = await LoadOperationsAsync(connection, transaction, command.CompanyId)
                ?? throw new BusinessException("No fue posible cargar reglas operativas");

            if (table.Status == "invoiced" && order.Status == "completed")
            {
                var replay = await TryBuildReplayAsync(connection, transaction, command, table, order, items, operations);
                if (replay is null)
                    throw new BusinessException("La venta ya fue procesada con datos diferentes", "checkout_already_processed");

                transaction.Commit();
                return replay;
            }

            if (table.Status != "open" || order.Status != "pending")
                throw new BusinessException("La mesa ya fue facturada o cancelada", "checkout_already_processed");

            var calculation = Calculate(command, order.Subtotal, operations, enforceDiscountPolicy: true);

            var cashRegisterId = await LockActiveRegisterAsync(connection, transaction, command);
            if (operations.RequireCashRegister && cashRegisterId is null)
                throw new BusinessException("Debes abrir una caja antes de facturar");

            var movements = await BuildInventoryMovementsAsync(connection, transaction, command, order, table, items);
            await ApplyInventoryAsync(connection, transaction, command, order.Id, movements);
            await InsertPaymentsAsync(connection, transaction, command, order.Id, calculation.Payments);

            if (cashRegisterId.HasValue)
                await UpdateCashRegisterAsync(connection, transaction, command, cashRegisterId.Value, calculation);

            var credit = await InsertCreditAsync(connection, transaction, command, order, table, calculation);
            await CompleteOrderAsync(
                connection, transaction, command, order, cashRegisterId, calculation);
            var invoicedAt = await CompleteTableAsync(connection, transaction, command);

            transaction.Commit();

            _logger.LogInformation(
                "Checkout restaurante completado. Company={CompanyId}, Branch={BranchId}, Table={TableId}, Order={OrderId}",
                command.CompanyId, command.BranchId, command.TableId, order.Id);

            return BuildResult(table, order, items, calculation, invoicedAt, credit, isReplay: false);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task AddItemsAsync(AddOrderItemsCommand command)
    {
        if (command.Items.Count == 0 || command.Items.Any(item => item.ProductId <= 0))
            throw new ValidationException("Todos los items deben tener producto y cantidad validos");
        foreach (var item in command.Items)
            SaleItemPolicy.NormalizeQuantity(item.Quantity);

        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();
        try
        {
            var lockCommand = new CheckoutCommand
            {
                CompanyId = command.CompanyId,
                BranchId = command.BranchId,
                TableId = command.TableId
            };
            var table = await LockTableAsync(connection, transaction, lockCommand)
                ?? throw new NotFoundException("Mesa");
            var order = await LockOrderAsync(connection, transaction, lockCommand)
                ?? throw new BusinessException("No hay orden asociada a esta mesa");
            if (table.Status != "open" || order.Status != "pending")
                throw new BusinessException("La mesa ya fue procesada", "checkout_already_processed");

            var existingItems = await LoadItemsAsync(connection, transaction, order.Id, command.CompanyId);
            foreach (var requested in command.Items.GroupBy(item => item.ProductId).Select(group => new
                     {
                         ProductId = group.Key,
                         Quantity = group.Sum(item => item.Quantity),
                         Item = group.First()
                     }))
            {
                var existing = existingItems.FirstOrDefault(item => item.ProductId == requested.ProductId);
                int affected;
                if (existing is null)
                {
                    affected = await connection.ExecuteAsync(@"
                        INSERT INTO sales.order_items (
                            company_id, order_id, product_id, product_name,
                            quantity, unit_price, created_at
                        ) VALUES (
                            @CompanyId, @OrderId, @ProductId, @ProductName,
                            @Quantity, @UnitPrice, NOW()
                        )", new
                    {
                        command.CompanyId,
                        OrderId = order.Id,
                        requested.ProductId,
                        requested.Item.ProductName,
                        requested.Quantity,
                        requested.Item.UnitPrice
                    }, transaction);
                }
                else
                {
                    affected = await connection.ExecuteAsync(@"
                        UPDATE sales.order_items
                        SET quantity = quantity + @Quantity
                        WHERE id = @Id
                          AND company_id = @CompanyId
                          AND order_id = @OrderId",
                        new
                        {
                            existing.Id,
                            command.CompanyId,
                            OrderId = order.Id,
                            requested.Quantity
                        }, transaction);
                }

                if (affected != 1)
                    throw new BusinessException("La orden dejo de estar disponible durante la modificacion");
            }

            await RecalculatePendingOrderAsync(connection, transaction, command.CompanyId, command.BranchId, order.Id);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task UpdateItemQuantityAsync(UpdateOrderItemQuantityCommand command)
    {
        SaleItemPolicy.NormalizeQuantity(command.Quantity);

        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();
        try
        {
            var target = await connection.QuerySingleOrDefaultAsync<ItemMutationTarget>(@"
                SELECT t.id AS TableId, o.id AS OrderId
                FROM sales.order_items oi
                JOIN sales.orders o ON o.id = oi.order_id AND o.company_id = oi.company_id
                JOIN sales.tables t ON t.id = o.table_id AND t.company_id = o.company_id
                WHERE oi.id = @OrderItemId
                  AND oi.company_id = @CompanyId
                  AND o.branch_id = @BranchId
                  AND t.branch_id = @BranchId",
                new { command.OrderItemId, command.CompanyId, command.BranchId }, transaction)
                ?? throw new NotFoundException("Item");

            var lockCommand = new CheckoutCommand
            {
                CompanyId = command.CompanyId,
                BranchId = command.BranchId,
                TableId = target.TableId
            };
            var table = await LockTableAsync(connection, transaction, lockCommand)
                ?? throw new NotFoundException("Mesa");
            var order = await LockOrderAsync(connection, transaction, lockCommand)
                ?? throw new BusinessException("No hay orden asociada a esta mesa");
            if (order.Id != target.OrderId || table.Status != "open" || order.Status != "pending")
                throw new BusinessException("La mesa ya fue procesada", "checkout_already_processed");

            var items = await LoadItemsAsync(connection, transaction, order.Id, command.CompanyId);
            if (items.All(item => item.Id != command.OrderItemId))
                throw new NotFoundException("Item");

            var affected = await connection.ExecuteAsync(@"
                UPDATE sales.order_items
                SET quantity = @Quantity
                WHERE id = @OrderItemId AND company_id = @CompanyId AND order_id = @OrderId",
                new { command.OrderItemId, command.CompanyId, OrderId = order.Id, command.Quantity }, transaction);
            if (affected != 1)
                throw new BusinessException("La orden dejo de estar disponible durante la modificacion");

            await RecalculatePendingOrderAsync(connection, transaction, command.CompanyId, command.BranchId, order.Id);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task RecalculatePendingOrderAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        long companyId,
        long branchId,
        long orderId)
    {
        var affected = await connection.ExecuteAsync(@"
            UPDATE sales.orders
            SET subtotal = COALESCE((
                    SELECT SUM(subtotal) FROM sales.order_items
                    WHERE order_id = @OrderId AND company_id = @CompanyId
                ), 0),
                total = COALESCE((
                    SELECT SUM(subtotal) FROM sales.order_items
                    WHERE order_id = @OrderId AND company_id = @CompanyId
                ), 0),
                updated_at = NOW()
            WHERE id = @OrderId
              AND company_id = @CompanyId
              AND branch_id = @BranchId
              AND status = 'pending'",
            new { CompanyId = companyId, BranchId = branchId, OrderId = orderId }, transaction);
        if (affected != 1)
            throw new BusinessException("La orden dejo de estar disponible durante la modificacion");
    }

    private static Task<CheckoutTableRow?> LockTableAsync(
        IDbConnection connection, IDbTransaction transaction, CheckoutCommand command) =>
        connection.QuerySingleOrDefaultAsync<CheckoutTableRow>(@"
            SELECT id AS Id, table_number AS TableNumber, name AS Name,
                   status AS Status, updated_at AS UpdatedAt
            FROM sales.tables
            WHERE id = @TableId
              AND company_id = @CompanyId
              AND branch_id = @BranchId
              AND deleted_at IS NULL
            FOR UPDATE", new { command.TableId, command.CompanyId, command.BranchId }, transaction);

    private static Task<CheckoutOrderRow?> LockOrderAsync(
        IDbConnection connection, IDbTransaction transaction, CheckoutCommand command) =>
        connection.QuerySingleOrDefaultAsync<CheckoutOrderRow>(@"
            SELECT id AS Id, order_number AS OrderNumber, status AS Status,
                   subtotal AS Subtotal, total AS Total,
                   discount_type AS DiscountType, discount_value AS DiscountValue,
                   discount_amount AS DiscountAmount, final_total_paid AS FinalTotalPaid,
                   split_reference_count AS SplitReferenceCount,
                   cash_register_id AS CashRegisterId, payment_method AS PaymentMethod,
                   tip_amount AS TipAmount, tip_included AS TipIncluded,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM sales.orders
            WHERE table_id = @TableId
              AND company_id = @CompanyId
              AND branch_id = @BranchId
              AND deleted_at IS NULL
            ORDER BY created_at DESC, id DESC
            LIMIT 1
            FOR UPDATE", new { command.TableId, command.CompanyId, command.BranchId }, transaction);

    private static async Task<List<CheckoutItemRow>> LoadItemsAsync(
        IDbConnection connection, IDbTransaction transaction, long orderId, long companyId)
    {
        var rows = await connection.QueryAsync<CheckoutItemRow>(@"
            SELECT oi.id AS Id, oi.company_id AS CompanyId,
                   oi.order_id AS OrderId, oi.product_id AS ProductId,
                   oi.product_name AS ProductName, oi.quantity AS Quantity,
                   oi.unit_price AS UnitPrice, oi.subtotal AS Subtotal,
                   (p.id IS NOT NULL) AS ProductExists,
                   p.product_type AS ProductType, p.track_stock AS TrackStock,
                   p.is_active AS IsActive, p.is_for_sale AS IsForSale
            FROM sales.order_items oi
            LEFT JOIN inventory.products p
              ON p.id = oi.product_id
             AND p.company_id = oi.company_id
             AND p.deleted_at IS NULL
            WHERE oi.order_id = @OrderId AND oi.company_id = @CompanyId
            ORDER BY oi.id
            FOR UPDATE OF oi", new { OrderId = orderId, CompanyId = companyId }, transaction);
        return rows.ToList();
    }

    private static Task<CheckoutOperationsRow?> LoadOperationsAsync(
        IDbConnection connection, IDbTransaction transaction, long companyId) =>
        connection.QuerySingleOrDefaultAsync<CheckoutOperationsRow>(@"
            SELECT manual_discount_enabled AS ManualDiscountEnabled,
                   max_discount_percent AS MaxDiscountPercent,
                   max_discount_amount AS MaxDiscountAmount,
                   discount_requires_override AS DiscountRequiresOverride,
                   discount_override_threshold_percent AS DiscountOverrideThresholdPercent,
                   require_cash_register AS RequireCashRegister
            FROM core.companies
            WHERE id = @CompanyId AND deleted_at IS NULL",
            new { CompanyId = companyId }, transaction);

    private static CheckoutCalculation Calculate(
        CheckoutCommand command,
        decimal subtotal,
        CheckoutOperationsRow operations,
        bool enforceDiscountPolicy)
    {
        var discountType = NormalizeDiscountType(command.DiscountType);
        var discountValue = PaymentPolicy.RoundMoney(command.DiscountValue);
        if (discountType is not ("none" or "fixed" or "percentage"))
            throw new ValidationException("Tipo de descuento no permitido");
        if (discountValue < 0)
            throw new ValidationException("El descuento no puede ser negativo");

        decimal discountAmount;
        decimal discountPercent;
        if (discountType == "percentage")
        {
            discountPercent = discountValue;
            discountAmount = PaymentPolicy.RoundMoney(subtotal * discountPercent / 100m);
        }
        else if (discountType == "fixed")
        {
            discountAmount = discountValue;
            discountPercent = subtotal > 0 ? PaymentPolicy.RoundMoney(discountAmount / subtotal * 100m) : 0;
        }
        else
        {
            discountValue = 0;
            discountAmount = 0;
            discountPercent = 0;
        }

        if (discountAmount > subtotal)
            throw new ValidationException("El descuento no puede ser mayor al subtotal");

        if (enforceDiscountPolicy && discountType != "none")
        {
            if (!operations.ManualDiscountEnabled)
                throw new BusinessException("El descuento manual esta deshabilitado en configuracion");
            if (discountType == "percentage" && discountValue > operations.MaxDiscountPercent)
                throw new ValidationException($"El descuento porcentual no puede superar {operations.MaxDiscountPercent:N2}%");
            if (discountType == "fixed" && discountValue > operations.MaxDiscountAmount)
                throw new ValidationException($"El descuento fijo no puede superar {operations.MaxDiscountAmount:N0}");
            if (operations.DiscountRequiresOverride &&
                discountPercent >= operations.DiscountOverrideThresholdPercent &&
                !command.OverrideConfirmed)
            {
                throw new ValidationException(
                    $"Este descuento requiere confirmacion adicional desde {operations.DiscountOverrideThresholdPercent:N2}%");
            }
        }

        var netTotal = PaymentPolicy.RoundMoney(subtotal - discountAmount);
        if (command.SubmittedFinalTotal > 0 && Math.Abs(command.SubmittedFinalTotal - netTotal) > 1m)
            throw new ValidationException("El total final no coincide con el descuento aplicado");
        if (command.CreditAmountPaid < 0)
            throw new ValidationException("El valor pagado del credito no puede ser negativo");
        if (command.TipAmount < 0)
            throw new ValidationException("La propina no puede ser negativa");

        if (command.HasCredit && command.CreditAmountPaid > netTotal)
            throw new ValidationException("El valor pagado no puede superar el total de la venta");

        var actualPaid = command.HasCredit && command.CreditAmountPaid < netTotal
            ? PaymentPolicy.RoundMoney(command.CreditAmountPaid)
            : netTotal;
        if (actualPaid > netTotal)
            throw new ValidationException("El valor pagado no puede superar el total de la venta");

        var payments = command.Payments.Select(payment =>
        {
            var method = PaymentPolicy.NormalizeMethod(payment.Method);
            var amount = PaymentPolicy.NormalizePositiveAmount(payment.Amount);
            return new CheckoutPayment(method, amount, NormalizeOptional(payment.Reference));
        }).ToList();

        var expectedPayment = PaymentPolicy.RoundMoney(
            actualPaid + (command.TipIncluded ? PaymentPolicy.RoundMoney(command.TipAmount) : 0));
        if (expectedPayment > 0 && payments.Count == 0)
            throw new ValidationException("Debe especificar al menos un metodo de pago");
        if (expectedPayment == 0 && payments.Count > 0)
            throw new ValidationException("No debe registrar pagos cuando toda la cuenta queda a credito");
        PaymentPolicy.ValidatePaymentTotal(expectedPayment, payments.Select(payment => payment.Amount));

        return new CheckoutCalculation
        {
            DiscountType = discountType,
            DiscountValue = discountValue,
            DiscountAmount = discountAmount,
            NetTotal = netTotal,
            ActualPaid = actualPaid,
            CreditAmount = command.HasCredit ? PaymentPolicy.RoundMoney(netTotal - actualPaid) : 0,
            TipAmount = PaymentPolicy.RoundMoney(command.TipAmount),
            SplitCount = Math.Max(1, command.SplitCount),
            Payments = payments
        };
    }

    private static async Task<long?> LockActiveRegisterAsync(
        IDbConnection connection, IDbTransaction transaction, CheckoutCommand command) =>
        await connection.QuerySingleOrDefaultAsync<long?>(@"
            SELECT id
            FROM sales.cash_registers
            WHERE company_id = @CompanyId
              AND branch_id = @BranchId
              AND opened_by = @UserId
              AND status = 'open'
              AND deleted_at IS NULL
            ORDER BY opened_at DESC, id DESC
            LIMIT 1
            FOR UPDATE", new { command.CompanyId, command.BranchId, command.UserId }, transaction);

    private static async Task<List<InventoryMovementPlan>> BuildInventoryMovementsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        CheckoutCommand command,
        CheckoutOrderRow order,
        CheckoutTableRow table,
        List<CheckoutItemRow> items)
    {
        foreach (var item in items)
        {
            if (!SaleItemPolicy.IsQuantitySupported(item.Quantity) || item.UnitPrice < 0)
                throw new ValidationException($"El item {item.Id} tiene cantidad o precio invalido");
            if (!item.ProductExists)
                throw new ValidationException($"El producto {item.ProductId} no existe en el comercio");
            if (!item.IsActive || !item.IsForSale)
                throw new ValidationException($"El producto {item.ProductName} ya no esta disponible para venta");
        }

        var movements = items
            .Where(item => item.ProductType != "prepared")
            .GroupBy(item => new { item.ProductId, item.TrackStock, item.UnitPrice })
            .Select(group => new InventoryMovementPlan
            {
                ProductId = group.Key.ProductId,
                Quantity = Math.Round(group.Sum(item => item.Quantity), 3, MidpointRounding.AwayFromZero),
                UnitCost = group.Key.UnitPrice,
                MovementType = "sale",
                RequiresStock = group.Key.TrackStock,
                Notes = $"Venta Mesa {table.TableNumber} - {order.OrderNumber}"
            }).ToList();

        var preparedIds = items
            .Where(item => item.ProductType == "prepared")
            .Select(item => item.ProductId)
            .Distinct()
            .ToArray();

        if (preparedIds.Length > 0)
        {
            var recipeRows = await connection.QueryAsync<CheckoutRecipeRow>(@"
                SELECT r.product_id AS ProductId, r.ingredient_id AS IngredientId,
                       r.quantity AS Quantity, ingredient.track_stock AS TrackStock,
                       ingredient.cost_price AS UnitCost
                FROM inventory.recipes r
                JOIN inventory.products prepared
                  ON prepared.id = r.product_id AND prepared.company_id = r.company_id
                JOIN inventory.products ingredient
                  ON ingredient.id = r.ingredient_id AND ingredient.company_id = r.company_id
                WHERE r.company_id = @CompanyId
                  AND r.product_id = ANY(@PreparedIds)
                  AND prepared.product_type = 'prepared'
                  AND ingredient.deleted_at IS NULL",
                new { command.CompanyId, PreparedIds = preparedIds }, transaction);

            var soldByProduct = items
                .Where(item => item.ProductType == "prepared")
                .GroupBy(item => item.ProductId)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

            movements.AddRange(recipeRows
                .GroupBy(row => new { row.IngredientId, row.TrackStock, row.UnitCost })
                .Select(group => new InventoryMovementPlan
                {
                    ProductId = group.Key.IngredientId,
                    Quantity = Math.Round(
                        group.Sum(row => row.Quantity * soldByProduct[row.ProductId]),
                        3,
                        MidpointRounding.AwayFromZero),
                    UnitCost = group.Key.UnitCost,
                    MovementType = "recipe_consumption",
                    RequiresStock = group.Key.TrackStock,
                    Notes = $"Consumo receta - Venta Mesa {table.TableNumber} - {order.OrderNumber}"
                }));
        }

        return movements.Where(movement => movement.Quantity > 0).ToList();
    }

    private static async Task ApplyInventoryAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        CheckoutCommand command,
        long orderId,
        List<InventoryMovementPlan> movements)
    {
        var required = movements
            .Where(movement => movement.RequiresStock)
            .GroupBy(movement => movement.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(movement => movement.Quantity));

        if (required.Count > 0)
        {
            var locked = (await connection.QueryAsync<CheckoutStockRow>(@"
                SELECT product_id AS ProductId, quantity AS Quantity
                FROM inventory.stock
                WHERE company_id = @CompanyId
                  AND branch_id = @BranchId
                  AND product_id = ANY(@ProductIds)
                ORDER BY product_id
                FOR UPDATE",
                new { command.CompanyId, command.BranchId, ProductIds = required.Keys.OrderBy(id => id).ToArray() },
                transaction)).ToDictionary(row => row.ProductId);

            foreach (var requirement in required.OrderBy(entry => entry.Key))
            {
                if (!locked.TryGetValue(requirement.Key, out var stock) || stock.Quantity < requirement.Value)
                    throw new BusinessException($"Stock insuficiente para el producto {requirement.Key}");
            }
        }

        foreach (var movement in movements.OrderBy(movement => movement.ProductId).ThenBy(movement => movement.MovementType))
        {
            decimal? stockAfter = null;
            if (movement.RequiresStock)
            {
                stockAfter = await connection.QuerySingleOrDefaultAsync<decimal?>(@"
                    UPDATE inventory.stock
                    SET quantity = quantity - @Quantity, updated_at = NOW()
                    WHERE company_id = @CompanyId
                      AND branch_id = @BranchId
                      AND product_id = @ProductId
                      AND quantity >= @Quantity
                    RETURNING quantity",
                    new { command.CompanyId, command.BranchId, movement.ProductId, movement.Quantity }, transaction);

                if (stockAfter is null)
                    throw new BusinessException($"Stock insuficiente para el producto {movement.ProductId}");
            }

            await connection.ExecuteAsync(@"
                INSERT INTO inventory.movements (
                    company_id, branch_id, product_id, movement_type,
                    quantity, unit_cost, reference_type, reference_id,
                    notes, stock_after, created_by, created_at
                ) VALUES (
                    @CompanyId, @BranchId, @ProductId, @MovementType,
                    @Quantity, @UnitCost, 'order', @OrderId,
                    @Notes, @StockAfter, @UserId, NOW()
                )",
                new
                {
                    command.CompanyId,
                    command.BranchId,
                    movement.ProductId,
                    movement.MovementType,
                    movement.Quantity,
                    movement.UnitCost,
                    OrderId = orderId,
                    movement.Notes,
                    StockAfter = stockAfter,
                    command.UserId
                }, transaction);
        }
    }

    private static async Task InsertPaymentsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        CheckoutCommand command,
        long orderId,
        List<CheckoutPayment> payments)
    {
        foreach (var payment in payments)
        {
            await connection.ExecuteAsync(@"
                INSERT INTO sales.order_payments (
                    company_id, order_id, method, amount, reference, created_at
                ) VALUES (
                    @CompanyId, @OrderId, @Method, @Amount, @Reference, NOW()
                )", new
            {
                command.CompanyId,
                OrderId = orderId,
                payment.Method,
                payment.Amount,
                payment.Reference
            }, transaction);
        }
    }

    private static async Task UpdateCashRegisterAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        CheckoutCommand command,
        long cashRegisterId,
        CheckoutCalculation calculation)
    {
        var accountingPayments = calculation.Payments
            .Select(payment => new
            {
                Method = PaymentPolicy.ToAccountingMethod(payment.Method),
                payment.Amount
            }).ToList();
        var cash = accountingPayments.Where(payment => payment.Method == "cash").Sum(payment => payment.Amount);
        var card = accountingPayments.Where(payment => payment.Method == "card").Sum(payment => payment.Amount);
        var transfer = accountingPayments.Where(payment => payment.Method == "transfer").Sum(payment => payment.Amount);
        var other = accountingPayments.Where(payment => payment.Method == "other").Sum(payment => payment.Amount);

        var affected = await connection.ExecuteAsync(@"
            UPDATE sales.cash_registers
            SET total_sales = total_sales + @NetTotal,
                total_cash_sales = total_cash_sales + @Cash,
                total_card_sales = total_card_sales + @Card,
                total_transfer_sales = total_transfer_sales + @Transfer,
                total_other_sales = total_other_sales + @Other,
                total_discounts = total_discounts + @DiscountAmount,
                total_credits = total_credits + @CreditAmount,
                total_tips = total_tips + @IncludedTip,
                order_count = order_count + 1,
                updated_at = NOW()
            WHERE id = @CashRegisterId
              AND company_id = @CompanyId
              AND branch_id = @BranchId
              AND opened_by = @UserId
              AND status = 'open'",
            new
            {
                CashRegisterId = cashRegisterId,
                command.CompanyId,
                command.BranchId,
                command.UserId,
                calculation.NetTotal,
                Cash = cash,
                Card = card,
                Transfer = transfer,
                Other = other,
                calculation.DiscountAmount,
                calculation.CreditAmount,
                IncludedTip = command.TipIncluded ? calculation.TipAmount : 0
            }, transaction);

        if (affected != 1)
            throw new BusinessException("La caja dejo de estar disponible durante la venta");
    }

    private static async Task<CheckoutCreditRow?> InsertCreditAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        CheckoutCommand command,
        CheckoutOrderRow order,
        CheckoutTableRow table,
        CheckoutCalculation calculation)
    {
        if (calculation.CreditAmount <= 0)
            return null;

        return await connection.QuerySingleAsync<CheckoutCreditRow>(@"
            INSERT INTO sales.credits (
                company_id, branch_id, order_id, customer_name, order_number,
                original_total, amount_paid, credit_amount, status, notes,
                created_by, created_at
            ) VALUES (
                @CompanyId, @BranchId, @OrderId, @CustomerName, @OrderNumber,
                @OriginalTotal, @AmountPaid, @CreditAmount, 'pending', @Notes,
                @UserId, NOW()
            )
            RETURNING id AS Id, credit_amount AS CreditAmount",
            new
            {
                command.CompanyId,
                command.BranchId,
                OrderId = order.Id,
                CustomerName = ResolveCreditCustomer(command, table),
                order.OrderNumber,
                OriginalTotal = calculation.NetTotal,
                AmountPaid = calculation.ActualPaid,
                calculation.CreditAmount,
                Notes = NormalizeOptional(command.CreditNotes),
                command.UserId
            }, transaction);
    }

    private static async Task<DateTime> CompleteOrderAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        CheckoutCommand command,
        CheckoutOrderRow order,
        long? cashRegisterId,
        CheckoutCalculation calculation)
    {
        var invoicedAt = await connection.QuerySingleOrDefaultAsync<DateTime?>(@"
            UPDATE sales.orders
            SET discount_type = @DiscountType,
                discount_value = @DiscountValue,
                discount_amount = @DiscountAmount,
                final_total_paid = @ActualPaid,
                split_reference_count = @SplitCount,
                cash_register_id = @CashRegisterId,
                payment_method = @PaymentMethod,
                tip_amount = @TipAmount,
                tip_included = @TipIncluded,
                total = @Subtotal,
                status = 'completed',
                updated_at = NOW()
            WHERE id = @OrderId
              AND company_id = @CompanyId
              AND branch_id = @BranchId
              AND status = 'pending'
            RETURNING updated_at",
            new
            {
                OrderId = order.Id,
                command.CompanyId,
                command.BranchId,
                DiscountType = calculation.DiscountType == "none" ? null : calculation.DiscountType,
                calculation.DiscountValue,
                calculation.DiscountAmount,
                calculation.ActualPaid,
                calculation.SplitCount,
                CashRegisterId = cashRegisterId,
                PaymentMethod = ResolvePaymentMethod(calculation.Payments),
                calculation.TipAmount,
                command.TipIncluded,
                order.Subtotal
            }, transaction);

        return invoicedAt ?? throw new BusinessException("La orden dejo de estar disponible durante la venta");
    }

    private static async Task<DateTime> CompleteTableAsync(
        IDbConnection connection, IDbTransaction transaction, CheckoutCommand command)
    {
        var terminalAt = await connection.QuerySingleOrDefaultAsync<DateTime?>(@"
            UPDATE sales.tables
            SET status = 'invoiced', updated_at = NOW()
            WHERE id = @TableId
              AND company_id = @CompanyId
              AND branch_id = @BranchId
              AND status = 'open'
            RETURNING updated_at",
            new { command.TableId, command.CompanyId, command.BranchId }, transaction);
        return terminalAt ?? throw new BusinessException("La mesa dejo de estar disponible durante la venta");
    }

    private static async Task<CheckoutResult?> TryBuildReplayAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        CheckoutCommand command,
        CheckoutTableRow table,
        CheckoutOrderRow order,
        List<CheckoutItemRow> items,
        CheckoutOperationsRow operations)
    {
        CheckoutCalculation requested;
        try
        {
            requested = Calculate(command, order.Subtotal, operations, enforceDiscountPolicy: false);
        }
        catch (ValidationException)
        {
            return null;
        }

        var persistedPayments = (await connection.QueryAsync<CheckoutPayment>(@"
            SELECT method AS Method, amount AS Amount, reference AS Reference
            FROM sales.order_payments
            WHERE company_id = @CompanyId AND order_id = @OrderId",
            new { command.CompanyId, OrderId = order.Id }, transaction)).ToList();
        var persistedCredit = await connection.QuerySingleOrDefaultAsync<CheckoutCreditReplayRow>(@"
            SELECT id AS Id, customer_name AS CustomerName, notes AS Notes
            FROM sales.credits
            WHERE company_id = @CompanyId AND branch_id = @BranchId AND order_id = @OrderId
            ORDER BY id
            LIMIT 1",
            new { command.CompanyId, command.BranchId, OrderId = order.Id }, transaction);

        if (!MatchesOrder(order, command, requested) ||
            !PaymentsEqual(requested.Payments, persistedPayments) ||
            !CreditEqual(command, requested, persistedCredit))
        {
            return null;
        }

        return BuildResult(
            table,
            order,
            items,
            requested,
            table.UpdatedAt ?? order.CreatedAt,
            persistedCredit is null
                ? null
                : new CheckoutCreditRow { Id = persistedCredit.Id, CreditAmount = requested.CreditAmount },
            isReplay: true);
    }

    private static bool MatchesOrder(
        CheckoutOrderRow order, CheckoutCommand command, CheckoutCalculation calculation) =>
        NormalizeDiscountType(order.DiscountType) == calculation.DiscountType &&
        order.DiscountValue == calculation.DiscountValue &&
        order.DiscountAmount == calculation.DiscountAmount &&
        order.FinalTotalPaid == calculation.ActualPaid &&
        order.SplitReferenceCount == calculation.SplitCount &&
        order.TipAmount == calculation.TipAmount &&
        order.TipIncluded == command.TipIncluded;

    private static bool PaymentsEqual(
        IReadOnlyCollection<CheckoutPayment> requested,
        IReadOnlyCollection<CheckoutPayment> persisted)
    {
        static string Key(CheckoutPayment payment) =>
            $"{NormalizePaymentMethod(payment.Method)}\u001f{payment.Amount:0.00}\u001f{NormalizeOptional(payment.Reference)}";
        return requested.Select(Key).OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(persisted.Select(Key).OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal);
    }

    private static bool CreditEqual(
        CheckoutCommand command,
        CheckoutCalculation calculation,
        CheckoutCreditReplayRow? credit)
    {
        if (calculation.CreditAmount <= 0)
            return credit is null;
        var customerMatches = string.IsNullOrWhiteSpace(command.CreditCustomerName) ||
                              credit?.CustomerName == command.CreditCustomerName.Trim();
        return credit is not null &&
               customerMatches &&
               NormalizeOptional(credit.Notes) == NormalizeOptional(command.CreditNotes);
    }

    private static CheckoutResult BuildResult(
        CheckoutTableRow table,
        CheckoutOrderRow order,
        List<CheckoutItemRow> items,
        CheckoutCalculation calculation,
        DateTime invoicedAt,
        CheckoutCreditRow? credit,
        bool isReplay) => new()
        {
            TableNumber = table.TableNumber,
            OrderNumber = order.OrderNumber,
            Subtotal = order.Subtotal,
            DiscountType = calculation.DiscountType,
            DiscountValue = calculation.DiscountValue,
            DiscountAmount = calculation.DiscountAmount,
            AmountPaid = calculation.ActualPaid,
            TipAmount = calculation.TipAmount,
            SplitCount = calculation.SplitCount,
            Items = items.Select(item => new OrderItem
            {
                Id = item.Id,
                CompanyId = item.CompanyId,
                OrderId = item.OrderId,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Subtotal = item.Subtotal
            }).ToList(),
            Payments = calculation.Payments,
            InvoicedAt = invoicedAt,
            CreditId = credit?.Id,
            CreditAmount = credit?.CreditAmount,
            IsReplay = isReplay
        };

    private static string ResolveCreditCustomer(CheckoutCommand command, CheckoutTableRow table) =>
        !string.IsNullOrWhiteSpace(command.CreditCustomerName)
            ? command.CreditCustomerName.Trim()
            : !string.IsNullOrWhiteSpace(table.Name) ? table.Name.Trim() : $"Mesa {table.TableNumber}";

    private static string NormalizeDiscountType(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value.Trim().ToLowerInvariant();

    private static string NormalizePaymentMethod(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ResolvePaymentMethod(IReadOnlyCollection<CheckoutPayment> payments) =>
        PaymentPolicy.ResolvePersistedMethod(payments.Select(payment => payment.Method));

    private sealed class CheckoutTableRow
    {
        public long Id { get; init; }
        public int TableNumber { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime? UpdatedAt { get; init; }
    }

    private sealed class CheckoutOrderRow
    {
        public long Id { get; init; }
        public string OrderNumber { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public decimal Subtotal { get; init; }
        public decimal Total { get; init; }
        public string? DiscountType { get; init; }
        public decimal DiscountValue { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal FinalTotalPaid { get; init; }
        public int SplitReferenceCount { get; init; }
        public long? CashRegisterId { get; init; }
        public string? PaymentMethod { get; init; }
        public decimal TipAmount { get; init; }
        public bool TipIncluded { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }

    private sealed class CheckoutItemRow
    {
        public long Id { get; init; }
        public long CompanyId { get; init; }
        public long OrderId { get; init; }
        public long ProductId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal Subtotal { get; init; }
        public bool ProductExists { get; init; }
        public string ProductType { get; init; } = "simple";
        public bool TrackStock { get; init; }
        public bool IsActive { get; init; }
        public bool IsForSale { get; init; }
    }

    private sealed class CheckoutOperationsRow
    {
        public bool ManualDiscountEnabled { get; init; }
        public decimal MaxDiscountPercent { get; init; }
        public decimal MaxDiscountAmount { get; init; }
        public bool DiscountRequiresOverride { get; init; }
        public decimal DiscountOverrideThresholdPercent { get; init; }
        public bool RequireCashRegister { get; init; }
    }

    private sealed class CheckoutRecipeRow
    {
        public long ProductId { get; init; }
        public long IngredientId { get; init; }
        public decimal Quantity { get; init; }
        public bool TrackStock { get; init; }
        public decimal UnitCost { get; init; }
    }

    private sealed class CheckoutStockRow
    {
        public long ProductId { get; init; }
        public decimal Quantity { get; init; }
    }

    private sealed class InventoryMovementPlan
    {
        public long ProductId { get; init; }
        public decimal Quantity { get; init; }
        public decimal UnitCost { get; init; }
        public string MovementType { get; init; } = string.Empty;
        public bool RequiresStock { get; init; }
        public string Notes { get; init; } = string.Empty;
    }

    private sealed class CheckoutCalculation
    {
        public string DiscountType { get; init; } = "none";
        public decimal DiscountValue { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal NetTotal { get; init; }
        public decimal ActualPaid { get; init; }
        public decimal CreditAmount { get; init; }
        public decimal TipAmount { get; init; }
        public int SplitCount { get; init; }
        public List<CheckoutPayment> Payments { get; init; } = [];
    }

    private sealed class CheckoutCreditRow
    {
        public long Id { get; init; }
        public decimal CreditAmount { get; init; }
    }

    private sealed class CheckoutCreditReplayRow
    {
        public long Id { get; init; }
        public string CustomerName { get; init; } = string.Empty;
        public string? Notes { get; init; }
    }

    private sealed class ItemMutationTarget
    {
        public long TableId { get; init; }
        public long OrderId { get; init; }
    }
}
