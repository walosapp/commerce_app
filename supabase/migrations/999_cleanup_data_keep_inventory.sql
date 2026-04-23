-- =============================================
-- Script: 999_cleanup_data_keep_inventory.sql
-- Descripcion: Limpia ABSOLUTAMENTE TODOS los datos del sistema.
--              Solo se preserva el usuario superadmin dev@walos.app
-- WARNING: Extremadamente destructivo - NO ejecutar en produccion real
-- =============================================

-- =============================================
-- 1. PLATFORM
-- =============================================
TRUNCATE TABLE platform.billing_invoice_items,
               platform.billing_invoices,
               platform.company_subscriptions,
               platform.payment_methods
CASCADE;

-- =============================================
-- 2. SALES
-- =============================================
TRUNCATE TABLE sales.credit_payments,
               sales.credits,
               sales.order_items,
               sales.orders,
               sales.tables
CASCADE;

-- =============================================
-- 3. FINANCE
-- =============================================
TRUNCATE TABLE finance.entries,
               finance.categories
CASCADE;

-- =============================================
-- 4. SUPPLIERS
-- =============================================
TRUNCATE TABLE suppliers.purchase_order_items,
               suppliers.purchase_orders,
               suppliers.supplier_products,
               suppliers.suppliers
CASCADE;

-- =============================================
-- 5. DELIVERY
-- =============================================
TRUNCATE TABLE delivery.order_items,
               delivery.status_history,
               delivery.orders
CASCADE;

-- =============================================
-- 6. INVENTORY
-- =============================================
TRUNCATE TABLE inventory.ai_interactions,
               inventory.alerts,
               inventory.movements,
               inventory.stock,
               inventory.recipes,
               inventory.products,
               inventory.categories,
               inventory.units
CASCADE;

-- =============================================
-- 7. CORE — borrar todo
-- =============================================
TRUNCATE TABLE core.users,
               core.roles,
               core.branches,
               core.companies
CASCADE;

-- =============================================
-- 8. Recrear empresa sistema
-- =============================================
INSERT INTO core.companies (
    name, legal_name, tax_id, email,
    display_name, currency, timezone, language,
    is_active, subscription_plan
) VALUES (
    'Walos System', 'Walos Technologies S.A.S.', 'WALOS-SYSTEM-001', 'dev@walos.app',
    'Walos', 'COP', 'America/Bogota', 'es', TRUE, 'enterprise'
);

-- =============================================
-- 9. Recrear sucursal principal
-- =============================================
INSERT INTO core.branches (
    company_id, name, code, branch_type,
    address, city, state, country, is_active, is_main
)
SELECT id, 'Sede Principal', 'HQ-01', 'office',
       'Virtual', 'Bogota', 'Cundinamarca', 'CO', TRUE, TRUE
FROM core.companies WHERE tax_id = 'WALOS-SYSTEM-001';

-- =============================================
-- 10. Recrear rol dev
-- =============================================
INSERT INTO core.roles (
    company_id, name, code, description,
    permissions, access_level, is_system_role
)
SELECT id, 'Desarrollador', 'dev',
       'Super administrador del sistema SaaS. Acceso total.',
       '{"all": {"read": true, "write": true, "delete": true, "admin": true}}'::jsonb,
       100, TRUE
FROM core.companies WHERE tax_id = 'WALOS-SYSTEM-001';

-- =============================================
-- 11. Recrear usuario superadmin (pw: walos2024)
-- =============================================
INSERT INTO core.users (
    company_id, branch_id, role_id,
    first_name, last_name, email, phone,
    password_hash, language, is_active, email_verified
)
SELECT
    c.id,
    b.id,
    r.id,
    'Super', 'Admin', 'dev@walos.app', NULL,
    '$2a$11$8YxKYDqiRWQLMY.L44IYxe5oGyWGlAx01SU6UF69UO5Y1o9urLBia',
    'es', TRUE, TRUE
FROM core.companies c
JOIN core.branches  b ON b.company_id = c.id AND b.code = 'HQ-01'
JOIN core.roles     r ON r.company_id = c.id AND r.code = 'dev'
WHERE c.tax_id = 'WALOS-SYSTEM-001';

-- =============================================
-- VERIFICACION post-limpieza
-- =============================================
SELECT 'SUPERADMIN PRESERVADO' as resultado,
       email, first_name, last_name, is_active
FROM core.users
WHERE email = 'dev@walos.app';

SELECT 'CONTEOS FINALES' as resultado,
       (SELECT COUNT(*) FROM core.companies)              as empresas,
       (SELECT COUNT(*) FROM core.branches)               as sucursales,
       (SELECT COUNT(*) FROM core.users)                  as usuarios,
       (SELECT COUNT(*) FROM inventory.products)          as productos,
       (SELECT COUNT(*) FROM inventory.stock)             as stock,
       (SELECT COUNT(*) FROM sales.orders)                as ventas,
       (SELECT COUNT(*) FROM finance.entries)             as entradas_finanzas,
       (SELECT COUNT(*) FROM suppliers.suppliers)         as proveedores,
       (SELECT COUNT(*) FROM platform.company_subscriptions) as suscripciones;
