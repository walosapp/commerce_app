-- =============================================
-- Script: 999_cleanup_data_keep_inventory.sql
-- Descripcion: Limpia TODOS los datos excepto inventario y stock
--              Util para resetear datos de prueba manteniendo catalogo de productos
-- WARNING: Destructivo - usar con precaucion en produccion
-- =============================================

-- Iniciar transaccion para seguridad
BEGIN;

-- =============================================
-- 1. SALES (ventas, mesas, ordenes, creditos)
-- =============================================
-- Hijos primero (FKs)
TRUNCATE TABLE sales.credit_payments CASCADE;
TRUNCATE TABLE sales.credits CASCADE;
TRUNCATE TABLE sales.order_items CASCADE;
-- Padres
TRUNCATE TABLE sales.orders CASCADE;
TRUNCATE TABLE sales.tables CASCADE;

-- =============================================
-- 2. FINANCE (finanzas)
-- =============================================
-- Hijos primero
TRUNCATE TABLE finance.entries CASCADE;
-- Padres
TRUNCATE TABLE finance.categories CASCADE;

-- =============================================
-- 3. SUPPLIERS (proveedores y pedidos)
-- =============================================
-- Hijos primero
TRUNCATE TABLE suppliers.purchase_order_items CASCADE;
-- Padres
TRUNCATE TABLE suppliers.purchase_orders CASCADE;
TRUNCATE TABLE suppliers.supplier_products CASCADE;
TRUNCATE TABLE suppliers.suppliers CASCADE;

-- =============================================
-- 4. DELIVERY (domicilios)
-- =============================================
-- Hijos primero
TRUNCATE TABLE delivery.order_items CASCADE;
TRUNCATE TABLE delivery.status_history CASCADE;
-- Padres
TRUNCATE TABLE delivery.orders CASCADE;

-- =============================================
-- 5. INVENTORY - TABLAS PRESERVADAS (NO TOCAR)
-- =============================================
-- Las siguientes tablas NO se truncan:
-- - inventory.categories    (catalogo de categorias)
-- - inventory.units           (unidades de medida)
-- - inventory.products      (catalogo de productos)
-- - inventory.stock         (stock actual por sucursal)
-- - inventory.movements     (historial de movimientos)
-- - inventory.recipes       (recetas/BOM de productos preparados)
-- - inventory.ai_interactions (interacciones con IA)
-- - inventory.alerts        (alertas configuradas)

-- =============================================
-- 6. CORE - TABLAS PRESERVADAS (NO TOCAR)
-- =============================================
-- Las siguientes tablas NO se truncan:
-- - core.companies          (empresas/comercios)
-- - core.branches           (sucursales)
-- - core.users              (usuarios del sistema)

-- Resetear secuencias (opcional - para que IDs comiencen desde 1)
-- Solo si quieres que los nuevos registros tengan IDs bajos
-- Descomenta las lineas si las necesitas:

-- SELECT setval('sales.tables_id_seq', 1, false);
-- SELECT setval('sales.orders_id_seq', 1, false);
-- SELECT setval('sales.order_items_id_seq', 1, false);
-- SELECT setval('sales.credits_id_seq', 1, false);
-- SELECT setval('sales.credit_payments_id_seq', 1, false);
-- SELECT setval('finance.categories_id_seq', 1, false);
-- SELECT setval('finance.entries_id_seq', 1, false);
-- SELECT setval('suppliers.suppliers_id_seq', 1, false);
-- SELECT setval('suppliers.supplier_products_id_seq', 1, false);
-- SELECT setval('suppliers.purchase_orders_id_seq', 1, false);
-- SELECT setval('suppliers.purchase_order_items_id_seq', 1, false);
-- SELECT setval('delivery.orders_id_seq', 1, false);
-- SELECT setval('delivery.order_items_id_seq', 1, false);
-- SELECT setval('delivery.status_history_id_seq', 1, false);

COMMIT;

-- =============================================
-- VERIFICACION: Conteos post-limpieza
-- =============================================
SELECT 'INVENTORY (PRESERVADO)' as modulo, 
       (SELECT COUNT(*) FROM inventory.categories) as categorias,
       (SELECT COUNT(*) FROM inventory.units) as unidades,
       (SELECT COUNT(*) FROM inventory.products) as productos,
       (SELECT COUNT(*) FROM inventory.stock) as registros_stock,
       (SELECT COUNT(*) FROM inventory.movements) as movimientos,
       (SELECT COUNT(*) FROM inventory.recipes) as recetas;

SELECT 'CORE (PRESERVADO)' as modulo,
       (SELECT COUNT(*) FROM core.companies) as empresas,
       (SELECT COUNT(*) FROM core.branches) as sucursales,
       (SELECT COUNT(*) FROM core.users) as usuarios;

SELECT 'SALES (LIMPIADO)' as modulo,
       (SELECT COUNT(*) FROM sales.tables) as mesas,
       (SELECT COUNT(*) FROM sales.orders) as ordenes,
       (SELECT COUNT(*) FROM sales.order_items) as items,
       (SELECT COUNT(*) FROM sales.credits) as creditos;

SELECT 'FINANCE (LIMPIADO)' as modulo,
       (SELECT COUNT(*) FROM finance.categories) as categorias,
       (SELECT COUNT(*) FROM finance.entries) as entradas;

SELECT 'SUPPLIERS (LIMPIADO)' as modulo,
       (SELECT COUNT(*) FROM suppliers.suppliers) as proveedores,
       (SELECT COUNT(*) FROM suppliers.purchase_orders) as pedidos,
       (SELECT COUNT(*) FROM suppliers.supplier_products) as productos_proveedor;

SELECT 'DELIVERY (LIMPIADO)' as modulo,
       (SELECT COUNT(*) FROM delivery.orders) as domicilios,
       (SELECT COUNT(*) FROM delivery.order_items) as items_domicilio;
