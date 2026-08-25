namespace Walos.Infrastructure.Inventory;

internal static class CommittedInventorySql
{
    public const string Cte = @"
        committed_lines AS (
            SELECT oi.product_id AS product_id, oi.quantity AS quantity
            FROM sales.orders o
            JOIN sales.tables t
              ON t.id = o.table_id AND t.company_id = o.company_id
            JOIN sales.order_items oi
              ON oi.order_id = o.id AND oi.company_id = o.company_id
            JOIN inventory.products product
              ON product.id = oi.product_id AND product.company_id = oi.company_id
            WHERE o.company_id = @CompanyId
              AND o.branch_id = @BranchId
              AND o.id <> @ExcludedOrderId
              AND o.status = 'pending'
              AND t.status = 'open'
              AND t.deleted_at IS NULL
              AND COALESCE(product.product_type, 'simple') <> 'prepared'
              AND product.track_stock = TRUE

            UNION ALL

            SELECT recipe.ingredient_id AS product_id,
                   oi.quantity * recipe.quantity AS quantity
            FROM sales.orders o
            JOIN sales.tables t
              ON t.id = o.table_id AND t.company_id = o.company_id
            JOIN sales.order_items oi
              ON oi.order_id = o.id AND oi.company_id = o.company_id
            JOIN inventory.products prepared
              ON prepared.id = oi.product_id AND prepared.company_id = oi.company_id
            JOIN inventory.recipes recipe
              ON recipe.product_id = prepared.id AND recipe.company_id = prepared.company_id
            JOIN inventory.products ingredient
              ON ingredient.id = recipe.ingredient_id AND ingredient.company_id = recipe.company_id
            WHERE o.company_id = @CompanyId
              AND o.branch_id = @BranchId
              AND o.id <> @ExcludedOrderId
              AND o.status = 'pending'
              AND t.status = 'open'
              AND t.deleted_at IS NULL
              AND prepared.product_type = 'prepared'
              AND ingredient.track_stock = TRUE
        ),
        committed AS (
            SELECT @BranchId::bigint AS branch_id,
                   product_id,
                   SUM(quantity) AS committed_quantity
            FROM committed_lines
            GROUP BY product_id
        )";
}
