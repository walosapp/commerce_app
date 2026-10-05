-- ============================================================
-- QUERIES DE VALIDACIÓN — AUDITORÍA REFUND DE PREPARADOS
-- Fecha: 2025-01-15
-- Propósito: Validar integridad de movimientos y refunds
-- ============================================================

-- ============================================================
-- 1. VERIFICAR MOVIMIENTOS DE UNA VENTA ESPECÍFICA
-- ============================================================

-- Uso: Reemplazar :order_id con el ID de la orden a auditar
-- Ejemplo: :order_id = 5001

SELECT 
    m.id AS movement_id,
    m.product_id,
    p.name AS product_name,
    p.product_type,
    m.movement_type,
    m.quantity,
    m.unit_cost,
    m.total_cost,
    m.reference_type,
    m.reference_id,
    m.notes,
    m.stock_after,
    m.created_at,
    u.first_name || ' ' || u.last_name AS created_by_name
FROM inventory.movements m
JOIN inventory.products p ON p.id = m.product_id
LEFT JOIN core.users u ON u.id = m.created_by
WHERE m.reference_type = 'order'
  AND m.reference_id = :order_id
ORDER BY m.created_at, m.id;

-- Resultado esperado:
-- - Movimientos con movement_type = 'sale' (productos simples)
-- - Movimientos con movement_type = 'recipe_consumption' (ingredientes de preparados)
-- - Todas las cantidades deben ser negativas (salidas)

-- ============================================================
-- 2. VERIFICAR REFUNDS DE UNA ORDEN
-- ============================================================

SELECT 
    r.id AS refund_id,
    r.refund_type,
    r.refund_amount,
    r.reason,
    r.status,
    r.created_at AS refund_created_at,
    ri.id AS refund_item_id,
    ri.order_item_id,
    ri.quantity AS refunded_quantity,
    ri.unit_price,
    ri.subtotal,
    oi.product_name,
    oi.quantity AS original_quantity,
    p.product_type
FROM sales.refunds r
JOIN sales.refund_items ri ON ri.refund_id = r.id
JOIN sales.order_items oi ON oi.id = ri.order_item_id
JOIN inventory.products p ON p.id = oi.product_id
WHERE r.order_id = :order_id
ORDER BY r.created_at, ri.id;

-- Resultado esperado:
-- - refund_type: 'full' o 'partial'
-- - status: 'completed'
-- - refunded_quantity <= original_quantity

-- ============================================================
-- 3. VERIFICAR MOVIMIENTOS DE REFUND
-- ============================================================

SELECT 
    m.id AS movement_id,
    m.product_id,
    p.name AS product_name,
    p.product_type,
    m.movement_type,
    m.quantity,
    m.unit_cost,
    m.reference_type,
    m.reference_id AS refund_id,
    m.notes,
    m.stock_after,
    m.created_at,
    r.order_id
FROM inventory.movements m
JOIN inventory.products p ON p.id = m.product_id
JOIN sales.refunds r ON r.id = m.reference_id
WHERE m.reference_type = 'refund'
  AND r.order_id = :order_id
ORDER BY m.created_at, m.id;

-- Resultado esperado:
-- - Movimientos con movement_type = 'refund' (productos simples)
-- - Movimientos con movement_type = 'refund_recipe' (ingredientes de preparados)
-- - Todas las cantidades deben ser positivas (entradas)

-- ============================================================
-- 4. AUDITORÍA COMPLETA: VENTA → REFUND → BALANCE
-- ============================================================

WITH sale_movements AS (
    SELECT 
        m.product_id,
        p.name AS product_name,
        SUM(m.quantity) AS total_sold,
        AVG(m.unit_cost) AS avg_unit_cost
    FROM inventory.movements m
    JOIN inventory.products p ON p.id = m.product_id
    WHERE m.reference_type = 'order'
      AND m.reference_id = :order_id
      AND m.movement_type IN ('sale', 'recipe_consumption')
    GROUP BY m.product_id, p.name
),
refund_movements AS (
    SELECT 
        m.product_id,
        SUM(m.quantity) AS total_refunded,
        AVG(m.unit_cost) AS avg_unit_cost
    FROM inventory.movements m
    JOIN sales.refunds r ON r.id = m.reference_id
    WHERE m.reference_type = 'refund'
      AND r.order_id = :order_id
      AND m.movement_type IN ('refund', 'refund_recipe')
    GROUP BY m.product_id
)
SELECT 
    s.product_id,
    s.product_name,
    s.total_sold,
    COALESCE(r.total_refunded, 0) AS total_refunded,
    s.total_sold + COALESCE(r.total_refunded, 0) AS net_impact,
    s.avg_unit_cost AS sale_unit_cost,
    r.avg_unit_cost AS refund_unit_cost,
    CASE 
        WHEN s.total_sold + COALESCE(r.total_refunded, 0) = 0 THEN '✅ Completamente devuelto'
        WHEN COALESCE(r.total_refunded, 0) = 0 THEN '⚠️ Sin devoluciones'
        ELSE '⚠️ Parcialmente devuelto'
    END AS status
FROM sale_movements s
LEFT JOIN refund_movements r ON r.product_id = s.product_id
ORDER BY s.product_id;

-- Resultado esperado:
-- - net_impact = 0 para refunds completos
-- - net_impact < 0 para refunds parciales
-- - sale_unit_cost = refund_unit_cost (debe coincidir)

-- ============================================================
-- 5. DETECTAR BUG: REFUND CON RECETA INCORRECTA
-- ============================================================

-- Esta query identifica casos donde el refund restituye cantidades
-- diferentes a las consumidas originalmente (evidencia del bug)

WITH sale_ingredients AS (
    SELECT 
        m.product_id,
        p.name AS product_name,
        ABS(SUM(m.quantity)) AS quantity_consumed
    FROM inventory.movements m
    JOIN inventory.products p ON p.id = m.product_id
    WHERE m.reference_type = 'order'
      AND m.reference_id = :order_id
      AND m.movement_type = 'recipe_consumption'
    GROUP BY m.product_id, p.name
),
refund_ingredients AS (
    SELECT 
        m.product_id,
        p.name AS product_name,
        SUM(m.quantity) AS quantity_refunded
    FROM inventory.movements m
    JOIN sales.refunds r ON r.id = m.reference_id
    JOIN inventory.products p ON p.id = m.product_id
    WHERE m.reference_type = 'refund'
      AND r.order_id = :order_id
      AND m.movement_type = 'refund_recipe'
    GROUP BY m.product_id, p.name
)
SELECT 
    COALESCE(s.product_id, r.product_id) AS product_id,
    COALESCE(s.product_name, r.product_name) AS product_name,
    COALESCE(s.quantity_consumed, 0) AS consumed_in_sale,
    COALESCE(r.quantity_refunded, 0) AS refunded,
    COALESCE(s.quantity_consumed, 0) - COALESCE(r.quantity_refunded, 0) AS difference,
    CASE 
        WHEN s.product_id IS NULL THEN '❌ BUG: Ingrediente NO consumido en venta pero SÍ restituido'
        WHEN r.product_id IS NULL THEN '⚠️ Ingrediente consumido pero NO restituido (refund parcial?)'
        WHEN ABS(COALESCE(s.quantity_consumed, 0) - COALESCE(r.quantity_refunded, 0)) > 0.001 
            THEN '❌ BUG: Cantidad restituida NO coincide con consumida'
        ELSE '✅ Correcto'
    END AS validation_status
FROM sale_ingredients s
FULL OUTER JOIN refund_ingredients r ON r.product_id = s.product_id
ORDER BY validation_status DESC, product_id;

-- Resultado esperado (sistema actual con bug):
-- - Filas con '❌ BUG' si la receta cambió entre venta y refund
-- - Filas con '✅ Correcto' si la receta NO cambió

-- Resultado esperado (sistema corregido):
-- - Todas las filas con '✅ Correcto' o '⚠️' (refund parcial)

-- ============================================================
-- 6. VERIFICAR RECETA ACTUAL VS. HISTÓRICA
-- ============================================================

-- Esta query compara la receta actual con los ingredientes
-- realmente consumidos en una venta

WITH current_recipe AS (
    SELECT 
        r.product_id,
        r.ingredient_id,
        i.name AS ingredient_name,
        r.quantity AS current_quantity
    FROM inventory.recipes r
    JOIN inventory.products i ON i.id = r.ingredient_id
    WHERE r.product_id = :prepared_product_id
),
historical_consumption AS (
    SELECT 
        oi.product_id AS prepared_product_id,
        m.product_id AS ingredient_id,
        p.name AS ingredient_name,
        ABS(m.quantity) / oi.quantity AS quantity_per_unit
    FROM inventory.movements m
    JOIN sales.order_items oi ON oi.order_id = m.reference_id
    JOIN inventory.products p ON p.id = m.product_id
    WHERE m.reference_type = 'order'
      AND m.reference_id = :order_id
      AND m.movement_type = 'recipe_consumption'
      AND oi.product_id = :prepared_product_id
)
SELECT 
    COALESCE(c.ingredient_id, h.ingredient_id) AS ingredient_id,
    COALESCE(c.ingredient_name, h.ingredient_name) AS ingredient_name,
    c.current_quantity AS current_recipe_qty,
    h.quantity_per_unit AS historical_qty,
    CASE 
        WHEN c.ingredient_id IS NULL THEN '⚠️ Ingrediente eliminado de receta'
        WHEN h.ingredient_id IS NULL THEN '⚠️ Ingrediente nuevo en receta'
        WHEN ABS(COALESCE(c.current_quantity, 0) - COALESCE(h.quantity_per_unit, 0)) > 0.001 
            THEN '⚠️ Cantidad cambió'
        ELSE '✅ Sin cambios'
    END AS change_status
FROM current_recipe c
FULL OUTER JOIN historical_consumption h ON h.ingredient_id = c.ingredient_id
ORDER BY change_status DESC, ingredient_id;

-- Resultado esperado:
-- - '✅ Sin cambios' si la receta NO cambió
-- - '⚠️' si la receta cambió (evidencia del riesgo de bug)

-- ============================================================
-- 7. IDENTIFICAR ÓRDENES CON RIESGO DE BUG
-- ============================================================

-- Esta query identifica órdenes de productos preparados que:
-- 1. Tienen movimientos de venta registrados
-- 2. La receta actual es diferente a la histórica
-- 3. Podrían tener refunds incorrectos

WITH prepared_orders AS (
    SELECT DISTINCT
        o.id AS order_id,
        o.order_number,
        o.created_at AS order_date,
        oi.product_id AS prepared_product_id,
        p.name AS product_name,
        oi.quantity
    FROM sales.orders o
    JOIN sales.order_items oi ON oi.order_id = o.id
    JOIN inventory.products p ON p.id = oi.product_id
    WHERE o.status = 'completed'
      AND p.product_type = 'prepared'
      AND o.company_id = :company_id
      AND o.created_at >= :start_date
),
recipe_changes AS (
    SELECT 
        po.order_id,
        po.prepared_product_id,
        COUNT(DISTINCT CASE 
            WHEN cr.ingredient_id IS NULL THEN hc.ingredient_id 
            WHEN hc.ingredient_id IS NULL THEN cr.ingredient_id
            WHEN ABS(cr.quantity - hc.quantity_per_unit) > 0.001 THEN cr.ingredient_id
        END) AS changed_ingredients
    FROM prepared_orders po
    LEFT JOIN inventory.recipes cr ON cr.product_id = po.prepared_product_id
    LEFT JOIN (
        SELECT 
            oi.product_id AS prepared_product_id,
            m.product_id AS ingredient_id,
            ABS(m.quantity) / oi.quantity AS quantity_per_unit
        FROM inventory.movements m
        JOIN sales.order_items oi ON oi.order_id = m.reference_id
        WHERE m.reference_type = 'order'
          AND m.movement_type = 'recipe_consumption'
    ) hc ON hc.prepared_product_id = po.prepared_product_id
    GROUP BY po.order_id, po.prepared_product_id
)
SELECT 
    po.order_id,
    po.order_number,
    po.order_date,
    po.product_name,
    po.quantity,
    rc.changed_ingredients,
    CASE 
        WHEN EXISTS (
            SELECT 1 FROM sales.refunds r 
            WHERE r.order_id = po.order_id
        ) THEN '⚠️ Tiene refunds (revisar)'
        ELSE 'Sin refunds'
    END AS refund_status,
    CASE 
        WHEN rc.changed_ingredients > 0 THEN '❌ RIESGO ALTO: Receta cambió'
        ELSE '✅ Sin riesgo'
    END AS risk_level
FROM prepared_orders po
LEFT JOIN recipe_changes rc ON rc.order_id = po.order_id
WHERE rc.changed_ingredients > 0
ORDER BY refund_status DESC, po.order_date DESC;

-- Resultado esperado:
-- - Lista de órdenes con riesgo de refunds incorrectos
-- - Priorizar revisión de órdenes con '⚠️ Tiene refunds'

-- ============================================================
-- 8. VALIDAR INTEGRIDAD DE STOCK DESPUÉS DE REFUND
-- ============================================================

-- Esta query verifica que el stock actual coincida con la suma
-- de todos los movimientos históricos

WITH all_movements AS (
    SELECT 
        product_id,
        SUM(quantity) AS total_movement
    FROM inventory.movements
    WHERE company_id = :company_id
      AND branch_id = :branch_id
    GROUP BY product_id
),
current_stock AS (
    SELECT 
        product_id,
        quantity AS current_quantity
    FROM inventory.stock
    WHERE company_id = :company_id
      AND branch_id = :branch_id
)
SELECT 
    p.id AS product_id,
    p.name AS product_name,
    COALESCE(cs.current_quantity, 0) AS current_stock,
    COALESCE(am.total_movement, 0) AS calculated_from_movements,
    COALESCE(cs.current_quantity, 0) - COALESCE(am.total_movement, 0) AS difference,
    CASE 
        WHEN ABS(COALESCE(cs.current_quantity, 0) - COALESCE(am.total_movement, 0)) < 0.001 
            THEN '✅ Correcto'
        ELSE '❌ INCONSISTENCIA DETECTADA'
    END AS validation_status
FROM inventory.products p
LEFT JOIN current_stock cs ON cs.product_id = p.id
LEFT JOIN all_movements am ON am.product_id = p.id
WHERE p.company_id = :company_id
  AND p.track_stock = TRUE
  AND ABS(COALESCE(cs.current_quantity, 0) - COALESCE(am.total_movement, 0)) > 0.001
ORDER BY ABS(COALESCE(cs.current_quantity, 0) - COALESCE(am.total_movement, 0)) DESC;

-- Resultado esperado:
-- - Lista vacía si todo está correcto
-- - Productos con inconsistencias si hay refunds incorrectos

-- ============================================================
-- 9. QUERY PARA IMPLEMENTACIÓN: OBTENER MOVIMIENTOS ORIGINALES
-- ============================================================

-- Esta es la query exacta que debe usar el código corregido

SELECT 
    m.product_id,
    ABS(m.quantity) AS quantity,
    m.unit_cost,
    p.track_stock
FROM inventory.movements m
JOIN inventory.products p ON p.id = m.product_id
WHERE m.company_id = :company_id
  AND m.branch_id = :branch_id
  AND m.reference_type = 'order'
  AND m.reference_id = :order_id
  AND m.movement_type = 'recipe_consumption'
  AND m.quantity < 0
ORDER BY m.product_id;

-- Uso en código C#:
-- var originalMovements = await connection.QueryAsync<OriginalMovementRow>(@"
--     SELECT m.product_id AS ProductId,
--            ABS(m.quantity) AS Quantity,
--            m.unit_cost AS UnitCost,
--            p.track_stock AS TrackStock
--     FROM inventory.movements m
--     JOIN inventory.products p ON p.id = m.product_id
--     WHERE m.company_id = @CompanyId
--       AND m.branch_id = @BranchId
--       AND m.reference_type = 'order'
--       AND m.reference_id = @OrderId
--       AND m.movement_type = 'recipe_consumption'
--       AND m.quantity < 0
--     ORDER BY m.product_id", new
-- {
--     CompanyId = command.CompanyId,
--     BranchId = command.BranchId,
--     OrderId = order.Id
-- }, transaction);

-- ============================================================
-- 10. ESTADÍSTICAS GENERALES
-- ============================================================

-- Resumen de órdenes, refunds y movimientos

SELECT 
    'Órdenes completadas' AS metric,
    COUNT(*) AS count
FROM sales.orders
WHERE company_id = :company_id
  AND status = 'completed'
  AND created_at >= :start_date

UNION ALL

SELECT 
    'Órdenes con productos preparados' AS metric,
    COUNT(DISTINCT o.id) AS count
FROM sales.orders o
JOIN sales.order_items oi ON oi.order_id = o.id
JOIN inventory.products p ON p.id = oi.product_id
WHERE o.company_id = :company_id
  AND o.status = 'completed'
  AND p.product_type = 'prepared'
  AND o.created_at >= :start_date

UNION ALL

SELECT 
    'Refunds procesados' AS metric,
    COUNT(*) AS count
FROM sales.refunds
WHERE company_id = :company_id
  AND created_at >= :start_date

UNION ALL

SELECT 
    'Refunds de productos preparados' AS metric,
    COUNT(DISTINCT r.id) AS count
FROM sales.refunds r
JOIN sales.refund_items ri ON ri.refund_id = r.id
JOIN sales.order_items oi ON oi.id = ri.order_item_id
JOIN inventory.products p ON p.id = oi.product_id
WHERE r.company_id = :company_id
  AND p.product_type = 'prepared'
  AND r.created_at >= :start_date

UNION ALL

SELECT 
    'Movimientos de inventario (total)' AS metric,
    COUNT(*) AS count
FROM inventory.movements
WHERE company_id = :company_id
  AND created_at >= :start_date

UNION ALL

SELECT 
    'Movimientos tipo recipe_consumption' AS metric,
    COUNT(*) AS count
FROM inventory.movements
WHERE company_id = :company_id
  AND movement_type = 'recipe_consumption'
  AND created_at >= :start_date

UNION ALL

SELECT 
    'Movimientos tipo refund_recipe' AS metric,
    COUNT(*) AS count
FROM inventory.movements
WHERE company_id = :company_id
  AND movement_type = 'refund_recipe'
  AND created_at >= :start_date;

-- ============================================================
-- PARÁMETROS DE EJEMPLO
-- ============================================================

-- Reemplazar estos valores según el caso a auditar:
-- :company_id = 1
-- :branch_id = 1
-- :order_id = 5001
-- :prepared_product_id = 100
-- :start_date = '2024-01-01'

-- ============================================================
-- FIN DE QUERIES DE VALIDACIÓN
-- ============================================================
