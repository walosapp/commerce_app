-- =============================================
-- Script: 800_seed_initial_data.sql
-- Descripcion: Datos iniciales para desarrollo y testing
-- Target: Supabase (PostgreSQL)
-- =============================================

-- 1. EMPRESA DEMO
INSERT INTO core.companies (
    name, legal_name, tax_id, email, phone,
    address, city, state, country, postal_code,
    currency, timezone, language, display_name, is_active
) VALUES (
    'Mi Bar & Restaurante',
    'Mi Bar y Restaurante S.A.S.',
    'DEMO123456789',
    'contacto@mibar.com',
    '+57 300 123 4567',
    'Calle Principal 123',
    'Bogota',
    'Cundinamarca',
    'CO',
    '110111',
    'COP',
    'America/Bogota',
    'es',
    'Mi Bar & Restaurante',
    TRUE
) ON CONFLICT (tax_id) DO NOTHING;

-- 2. SUCURSALES
DO $$
DECLARE
    v_company_id BIGINT;
BEGIN
    SELECT id INTO v_company_id FROM core.companies WHERE tax_id = 'DEMO123456789';

    INSERT INTO core.branches (
        company_id, name, code, branch_type, email, phone,
        address, city, state, country, postal_code,
        max_tables, max_capacity, is_active, is_main
    ) VALUES
    (
        v_company_id, 'Bar Principal', 'BAR-01', 'bar',
        'bar@mibar.com', '+57 300 123 4567',
        'Calle Principal 123', 'Bogota', 'Cundinamarca', 'CO', '110111',
        15, 60, TRUE, TRUE
    ),
    (
        v_company_id, 'Restaurante Centro', 'REST-01', 'restaurant',
        'restaurante@mibar.com', '+57 300 123 4568',
        'Carrera Secundaria 456', 'Bogota', 'Cundinamarca', 'CO', '110112',
        25, 100, TRUE, FALSE
    )
    ON CONFLICT (company_id, code) DO NOTHING;
END $$;

-- 3. ROLES
DO $$
DECLARE
    v_company_id BIGINT;
BEGIN
    SELECT id INTO v_company_id FROM core.companies WHERE tax_id = 'DEMO123456789';

    INSERT INTO core.roles (
        company_id, name, code, description, permissions, access_level, is_system_role
    ) VALUES
    (
        v_company_id, 'Super Administrador', 'super_admin',
        'Acceso completo a todas las funcionalidades',
        '{"all": {"read": true, "write": true, "delete": true}}'::jsonb,
        10, TRUE
    ),
    (
        v_company_id, 'Gerente', 'manager',
        'Gestion de operaciones y reportes',
        '{"inventory": {"read": true, "write": true}, "sales": {"read": true, "write": true}, "reports": {"read": true}}'::jsonb,
        8, TRUE
    ),
    (
        v_company_id, 'Cajero', 'cashier',
        'Operacion de punto de venta',
        '{"sales": {"read": true, "write": true}, "inventory": {"read": true}}'::jsonb,
        5, TRUE
    ),
    (
        v_company_id, 'Mesero', 'waiter',
        'Toma de ordenes y atencion a clientes',
        '{"sales": {"read": true, "write": true}}'::jsonb,
        3, TRUE
    )
    ON CONFLICT (company_id, code) DO NOTHING;
END $$;

-- 4. USUARIO ADMIN
DO $$
DECLARE
    v_company_id BIGINT;
    v_role_id BIGINT;
BEGIN
    SELECT id INTO v_company_id FROM core.companies WHERE tax_id = 'DEMO123456789';
    SELECT id INTO v_role_id FROM core.roles WHERE company_id = v_company_id AND code = 'super_admin';

    INSERT INTO core.users (
        company_id, branch_id, role_id,
        first_name, last_name, email, phone,
        password_hash, language, is_active, email_verified
    ) VALUES (
        v_company_id, NULL, v_role_id,
        'Admin', 'Sistema', 'admin@mibar.com', '+57 300 123 4567',
        '$2a$10$YourHashHere',
        'es', TRUE, TRUE
    ) ON CONFLICT (email) DO NOTHING;
END $$;

-- 5. CATEGORIAS DE INVENTARIO
DO $$
DECLARE
    v_company_id BIGINT;
BEGIN
    SELECT id INTO v_company_id FROM core.companies WHERE tax_id = 'DEMO123456789';

    IF NOT EXISTS (SELECT 1 FROM inventory.categories WHERE company_id = v_company_id) THEN
        INSERT INTO inventory.categories (
            company_id, name, code, description, icon, color, display_order
        ) VALUES
        (v_company_id, 'Bebidas Alcoholicas', 'BEB-ALC', 'Cervezas, vinos, licores', 'wine', '#8B4513', 1),
        (v_company_id, 'Bebidas No Alcoholicas', 'BEB-NOALC', 'Refrescos, jugos, agua', 'coffee', '#4A90E2', 2),
        (v_company_id, 'Alimentos', 'ALIM', 'Comida preparada e ingredientes', 'utensils', '#E74C3C', 3),
        (v_company_id, 'Insumos', 'INSUMOS', 'Materiales y suministros', 'package', '#95A5A6', 4),
        (v_company_id, 'Desechables', 'DESECH', 'Vasos, platos, servilletas', 'trash-2', '#BDC3C7', 5);
    END IF;
END $$;

-- 6. UNIDADES DE MEDIDA
DO $$
DECLARE
    v_company_id BIGINT;
BEGIN
    SELECT id INTO v_company_id FROM core.companies WHERE tax_id = 'DEMO123456789';

    IF NOT EXISTS (SELECT 1 FROM inventory.units WHERE company_id = v_company_id) THEN
        INSERT INTO inventory.units (
            company_id, name, abbreviation, unit_type, conversion_factor
        ) VALUES
        (v_company_id, 'Pieza', 'Pza', 'quantity', 1.0),
        (v_company_id, 'Litro', 'L', 'volume', 1.0),
        (v_company_id, 'Mililitro', 'mL', 'volume', 0.001),
        (v_company_id, 'Kilogramo', 'Kg', 'weight', 1.0),
        (v_company_id, 'Gramo', 'g', 'weight', 0.001),
        (v_company_id, 'Botella', 'Bot', 'quantity', 1.0),
        (v_company_id, 'Caja', 'Caja', 'quantity', 1.0),
        (v_company_id, 'Paquete', 'Paq', 'quantity', 1.0);
    END IF;
END $$;

-- 7. PRODUCTOS DE EJEMPLO
DO $$
DECLARE
    v_company_id BIGINT;
    v_cat_beb_alc BIGINT;
    v_cat_beb_noalc BIGINT;
    v_cat_alim BIGINT;
    v_unit_pza BIGINT;
    v_unit_bot BIGINT;
BEGIN
    SELECT id INTO v_company_id FROM core.companies WHERE tax_id = 'DEMO123456789';
    SELECT id INTO v_cat_beb_alc FROM inventory.categories WHERE company_id = v_company_id AND code = 'BEB-ALC';
    SELECT id INTO v_cat_beb_noalc FROM inventory.categories WHERE company_id = v_company_id AND code = 'BEB-NOALC';
    SELECT id INTO v_cat_alim FROM inventory.categories WHERE company_id = v_company_id AND code = 'ALIM';
    SELECT id INTO v_unit_pza FROM inventory.units WHERE company_id = v_company_id AND abbreviation = 'Pza';
    SELECT id INTO v_unit_bot FROM inventory.units WHERE company_id = v_company_id AND abbreviation = 'Bot';

    IF NOT EXISTS (SELECT 1 FROM inventory.products WHERE company_id = v_company_id) THEN
        INSERT INTO inventory.products (
            company_id, name, sku, category_id, unit_id,
            cost_price, sale_price, min_stock, reorder_point, is_perishable
        ) VALUES
        (v_company_id, 'Cerveza Corona 355ml', 'BEB-001', v_cat_beb_alc, v_unit_bot, 2200, 5000, 24, 48, FALSE),
        (v_company_id, 'Cerveza Modelo Especial 355ml', 'BEB-002', v_cat_beb_alc, v_unit_bot, 2100, 4800, 24, 48, FALSE),
        (v_company_id, 'Coca-Cola 600ml', 'BEB-003', v_cat_beb_noalc, v_unit_bot, 1500, 3500, 30, 60, FALSE),
        (v_company_id, 'Agua Mineral 1L', 'BEB-004', v_cat_beb_noalc, v_unit_bot, 1000, 2500, 40, 80, FALSE),
        (v_company_id, 'Hamburguesa Clasica', 'ALIM-001', v_cat_alim, v_unit_pza, 8000, 22000, 0, 0, TRUE),
        (v_company_id, 'Pizza Margarita', 'ALIM-002', v_cat_alim, v_unit_pza, 10000, 28000, 0, 0, TRUE),
        (v_company_id, 'Tacos al Pastor (3 pzas)', 'ALIM-003', v_cat_alim, v_unit_pza, 5000, 15000, 0, 0, TRUE);
    END IF;
END $$;

-- 8. STOCK INICIAL
DO $$
DECLARE
    v_company_id BIGINT;
    v_branch_bar_id BIGINT;
BEGIN
    SELECT id INTO v_company_id FROM core.companies WHERE tax_id = 'DEMO123456789';
    SELECT id INTO v_branch_bar_id FROM core.branches WHERE company_id = v_company_id AND code = 'BAR-01';

    IF NOT EXISTS (SELECT 1 FROM inventory.stock WHERE company_id = v_company_id) THEN
        INSERT INTO inventory.stock (company_id, branch_id, product_id, quantity)
        SELECT
            v_company_id,
            v_branch_bar_id,
            p.id,
            CASE
                WHEN p.sku LIKE 'BEB%' THEN 50
                WHEN p.sku LIKE 'ALIM%' THEN 0
                ELSE 100
            END
        FROM inventory.products p
        WHERE p.company_id = v_company_id;
    END IF;
END $$;

-- 9. CATEGORIAS FINANCIERAS INICIALES
DO $$
DECLARE
    v_company_id BIGINT;
BEGIN
    SELECT id INTO v_company_id FROM core.companies WHERE tax_id = 'DEMO123456789';

    IF NOT EXISTS (SELECT 1 FROM finance.categories WHERE company_id = v_company_id) THEN
        INSERT INTO finance.categories (company_id, name, type, color_hex, is_system) VALUES
        (v_company_id, 'Arriendo', 'expense', '#F97316', TRUE),
        (v_company_id, 'Servicios publicos', 'expense', '#EF4444', TRUE),
        (v_company_id, 'Nomina', 'expense', '#8B5CF6', TRUE),
        (v_company_id, 'Propinas', 'expense', '#06B6D4', TRUE),
        (v_company_id, 'Ingreso adicional', 'income', '#22C55E', TRUE),
        (v_company_id, 'Eventos', 'income', '#0EA5E9', TRUE);
    END IF;
END $$;
