-- ============================================================================
-- MANTENIMIENTO MANUAL DESTRUCTIVO - OPT-IN EXPLICITO
--
-- Script: supabase/scripts/cleanup_data_keep_inventory.sql
-- Descripcion: Limpia ABSOLUTAMENTE TODOS los datos del sistema.
--              Recrea una cuenta tecnica solo con un hash BCrypt suministrado
--              por el operador en walos.bootstrap_dev_password_hash.
--
-- PELIGRO: NO forma parte de la secuencia automatica de migraciones y NUNCA
-- debe ejecutarse mediante `supabase db reset`, CI/CD ni en produccion real.
-- Antes de ejecutarlo manualmente, confirmar por escrito el proyecto/entorno,
-- revisar el contenido completo y disponer de respaldo verificable.
-- Cualquier error aborta esta transaccion y evita una limpieza parcial.
-- ============================================================================

BEGIN;

-- Fail closed ANTES de la primera sentencia destructiva. El operador debe
-- ejecutar en esta misma sesion:
--   SELECT set_config('walos.bootstrap_dev_password_hash', '<bcrypt>', false);
-- El hash nunca debe persistirse en este archivo, documentacion ni Git.
DO $$
DECLARE
    v_password_hash TEXT := current_setting('walos.bootstrap_dev_password_hash', TRUE);
BEGIN
    IF v_password_hash IS NULL OR v_password_hash !~ '^\$2[aby]\$[0-9]{2}\$.{53}$' THEN
        RAISE EXCEPTION 'walos.bootstrap_dev_password_hash debe contener un hash BCrypt suministrado por el operador';
    END IF;
END $$;

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
-- 10. Recrear roles reservados de plataforma
-- =============================================
INSERT INTO core.roles (
    company_id, name, code, description,
    permissions, access_level, is_system_role, is_active
)
SELECT id, 'Desarrollador', 'dev',
       'Super administrador del sistema SaaS. Acceso total.',
       '{"all": {"read": true, "write": true, "delete": true, "admin": true}}'::jsonb,
       100, TRUE, TRUE
FROM core.companies WHERE tax_id = 'WALOS-SYSTEM-001'
UNION ALL
SELECT id, 'Administrador de Plataforma', 'platform_admin',
       'Administracion global de comercios y funcionalidades de Walos.',
       '{"platform": {"admin": true}}'::jsonb,
       100, TRUE, TRUE
FROM core.companies WHERE tax_id = 'WALOS-SYSTEM-001';

-- =============================================
-- 11. Recrear usuario tecnico con credencial suministrada por el operador
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
    current_setting('walos.bootstrap_dev_password_hash'),
    'es', TRUE, TRUE
FROM core.companies c
JOIN core.branches  b ON b.company_id = c.id AND b.code = 'HQ-01'
JOIN core.roles     r ON r.company_id = c.id AND r.code = 'dev'
WHERE c.tax_id = 'WALOS-SYSTEM-001';

-- =============================================
-- 12. Restaurar features V1 si existe migracion 019
-- =============================================
-- TRUNCATE ... CASCADE elimina company_features al recrear core.companies.
-- SQL dinamico permite seguir usando este cleanup en instalaciones donde 019
-- todavia no fue aplicada, sin crear schema ni tablas por fuera de migraciones.
DO $$
BEGIN
    IF to_regclass('platform.features') IS NOT NULL
       AND to_regclass('platform.company_features') IS NOT NULL THEN
        EXECUTE $feature_cleanup$
            DELETE FROM platform.features
            WHERE code NOT IN (
                'dashboard', 'inventory', 'restaurant', 'pos', 'cash',
                'purchases', 'suppliers', 'delivery', 'finance', 'ai'
            )
        $feature_cleanup$;

        EXECUTE $feature_catalog$
            INSERT INTO platform.features
                (code, name, description, default_enabled, is_mandatory, is_active, display_order)
            VALUES
                ('dashboard',   'Dashboard',     'Resumen operativo del comercio',                 TRUE,  TRUE,  TRUE, 1),
                ('inventory',   'Inventario',    'Productos, existencias, movimientos y alertas',  TRUE,  FALSE, TRUE, 2),
                ('restaurant',  'Restaurante',   'Mesas, ordenes y cierre de venta restaurante',   TRUE,  FALSE, TRUE, 3),
                ('pos',         'POS-Deli',      'Venta rapida de mostrador',                      TRUE,  FALSE, TRUE, 4),
                ('cash',        'Caja',          'Apertura, movimientos y cierre de caja',         TRUE,  FALSE, TRUE, 5),
                ('purchases',   'Compras',       'Ordenes de compra y recepcion',                  TRUE,  FALSE, TRUE, 6),
                ('suppliers',   'Proveedores',   'Catalogo y gestion de proveedores',              TRUE,  FALSE, TRUE, 7),
                ('delivery',    'Domicilios',    'Pedidos y seguimiento de domicilios',            TRUE,  FALSE, TRUE, 8),
                ('finance',     'Finanzas',      'Ingresos, egresos y reportes financieros',       TRUE,  FALSE, TRUE, 9),
                ('ai',          'Asistente IA',  'Asistente inteligente de Walos',                 FALSE, FALSE, TRUE, 10)
            ON CONFLICT (code) DO UPDATE SET
                name = EXCLUDED.name,
                description = EXCLUDED.description,
                default_enabled = EXCLUDED.default_enabled,
                is_mandatory = EXCLUDED.is_mandatory,
                is_active = EXCLUDED.is_active,
                display_order = EXCLUDED.display_order,
                updated_at = NOW(),
                updated_by = NULL
        $feature_catalog$;

        EXECUTE $feature_defaults$
            INSERT INTO platform.company_features
                (company_id, feature_code, is_enabled)
            SELECT c.id, f.code, f.default_enabled
            FROM core.companies c
            CROSS JOIN platform.features f
            ON CONFLICT (company_id, feature_code) DO NOTHING
        $feature_defaults$;
    END IF;
END $$;

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

SELECT set_config('walos.bootstrap_dev_password_hash', '', FALSE);
COMMIT;
