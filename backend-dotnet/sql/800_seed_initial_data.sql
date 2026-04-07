-- =============================================
-- Script: 800_seed_initial_data.sql
-- Descripción: Datos iniciales para desarrollo y testing
-- ¿Qué es? Datos de ejemplo para comenzar a usar el sistema
-- ¿Para qué? Facilitar desarrollo y pruebas sin tener que crear datos manualmente
-- =============================================

USE WalosDB;
GO

-- =============================================
-- 1. EMPRESA DE EJEMPLO
-- =============================================
IF NOT EXISTS (SELECT 1 FROM [core].[companies] WHERE tax_id = 'DEMO123456789')
BEGIN
    INSERT INTO [core].[companies] (
        name, legal_name, tax_id, email, phone, 
        address, city, state, country, postal_code,
        currency, timezone, language, is_active
    ) VALUES (
        'Mi Bar & Restaurante',
        'Mi Bar y Restaurante S.A. de C.V.',
        'DEMO123456789',
        'contacto@mibar.com',
        '+52 55 1234 5678',
        'Av. Principal 123',
        'Ciudad de México',
        'CDMX',
        'MX',
        '01000',
        'MXN',
        'America/Mexico_City',
        'es',
        1
    );
    PRINT '✓ Empresa demo creada';
END
GO

DECLARE @company_id BIGINT = (SELECT id FROM [core].[companies] WHERE tax_id = 'DEMO123456789');

-- =============================================
-- 2. SUCURSALES
-- =============================================
IF NOT EXISTS (SELECT 1 FROM [core].[branches] WHERE company_id = @company_id)
BEGIN
    INSERT INTO [core].[branches] (
        company_id, name, code, branch_type, email, phone,
        address, city, state, country, postal_code,
        max_tables, max_capacity, is_active, is_main
    ) VALUES 
    (
        @company_id, 'Bar Principal', 'BAR-01', 'bar',
        'bar@mibar.com', '+52 55 1234 5678',
        'Av. Principal 123', 'Ciudad de México', 'CDMX', 'MX', '01000',
        15, 60, 1, 1
    ),
    (
        @company_id, 'Restaurante Centro', 'REST-01', 'restaurant',
        'restaurante@mibar.com', '+52 55 1234 5679',
        'Calle Secundaria 456', 'Ciudad de México', 'CDMX', 'MX', '01001',
        25, 100, 1, 0
    );
    PRINT '✓ Sucursales demo creadas';
END
GO

DECLARE @company_id BIGINT = (SELECT id FROM [core].[companies] WHERE tax_id = 'DEMO123456789');
DECLARE @branch_bar_id BIGINT = (SELECT id FROM [core].[branches] WHERE company_id = @company_id AND code = 'BAR-01');
DECLARE @branch_rest_id BIGINT = (SELECT id FROM [core].[branches] WHERE company_id = @company_id AND code = 'REST-01');

-- =============================================
-- 3. ROLES
-- =============================================
IF NOT EXISTS (SELECT 1 FROM [core].[roles] WHERE company_id = @company_id)
BEGIN
    INSERT INTO [core].[roles] (
        company_id, name, code, description, permissions, access_level, is_system_role
    ) VALUES 
    (
        @company_id, 'Super Administrador', 'super_admin',
        'Acceso completo a todas las funcionalidades',
        '{"all": {"read": true, "write": true, "delete": true}}',
        10, 1
    ),
    (
        @company_id, 'Gerente', 'manager',
        'Gestión de operaciones y reportes',
        '{"inventory": {"read": true, "write": true}, "sales": {"read": true, "write": true}, "reports": {"read": true}}',
        8, 1
    ),
    (
        @company_id, 'Cajero', 'cashier',
        'Operación de punto de venta',
        '{"sales": {"read": true, "write": true}, "inventory": {"read": true}}',
        5, 1
    ),
    (
        @company_id, 'Mesero', 'waiter',
        'Toma de órdenes y atención a clientes',
        '{"sales": {"read": true, "write": true}}',
        3, 1
    );
    PRINT '✓ Roles demo creados';
END
GO

DECLARE @company_id BIGINT = (SELECT id FROM [core].[companies] WHERE tax_id = 'DEMO123456789');
DECLARE @role_admin_id BIGINT = (SELECT id FROM [core].[roles] WHERE company_id = @company_id AND code = 'super_admin');

-- =============================================
-- 4. USUARIO ADMINISTRADOR
-- =============================================
IF NOT EXISTS (SELECT 1 FROM [core].[users] WHERE email = 'admin@mibar.com')
BEGIN
    -- Password: Admin123! (bcrypt hash)
    INSERT INTO [core].[users] (
        company_id, branch_id, role_id,
        first_name, last_name, email, phone,
        password_hash, language, is_active, email_verified
    ) VALUES (
        @company_id, NULL, @role_admin_id,
        'Admin', 'Sistema', 'admin@mibar.com', '+52 55 1234 5678',
        '$2a$10$YourHashHere', -- Cambiar por hash real en implementación
        'es', 1, 1
    );
    PRINT '✓ Usuario admin demo creado (email: admin@mibar.com, password: Admin123!)';
END
GO

-- =============================================
-- 5. CATEGORÍAS DE INVENTARIO
-- =============================================
DECLARE @company_id BIGINT = (SELECT id FROM [core].[companies] WHERE tax_id = 'DEMO123456789');

IF NOT EXISTS (SELECT 1 FROM [inventory].[categories] WHERE company_id = @company_id)
BEGIN
    INSERT INTO [inventory].[categories] (
        company_id, name, code, description, icon, color, display_order
    ) VALUES 
    (@company_id, 'Bebidas Alcohólicas', 'BEB-ALC', 'Cervezas, vinos, licores', 'wine', '#8B4513', 1),
    (@company_id, 'Bebidas No Alcohólicas', 'BEB-NOALC', 'Refrescos, jugos, agua', 'coffee', '#4A90E2', 2),
    (@company_id, 'Alimentos', 'ALIM', 'Comida preparada y ingredientes', 'utensils', '#E74C3C', 3),
    (@company_id, 'Insumos', 'INSUMOS', 'Materiales y suministros', 'package', '#95A5A6', 4),
    (@company_id, 'Desechables', 'DESECH', 'Vasos, platos, servilletas', 'trash-2', '#BDC3C7', 5);
    PRINT '✓ Categorías de inventario creadas';
END
GO

-- =============================================
-- 6. UNIDADES DE MEDIDA
-- =============================================
DECLARE @company_id BIGINT = (SELECT id FROM [core].[companies] WHERE tax_id = 'DEMO123456789');

IF NOT EXISTS (SELECT 1 FROM [inventory].[units] WHERE company_id = @company_id)
BEGIN
    INSERT INTO [inventory].[units] (
        company_id, name, abbreviation, unit_type, conversion_factor
    ) VALUES 
    (@company_id, 'Pieza', 'Pza', 'quantity', 1.0),
    (@company_id, 'Litro', 'L', 'volume', 1.0),
    (@company_id, 'Mililitro', 'mL', 'volume', 0.001),
    (@company_id, 'Kilogramo', 'Kg', 'weight', 1.0),
    (@company_id, 'Gramo', 'g', 'weight', 0.001),
    (@company_id, 'Botella', 'Bot', 'quantity', 1.0),
    (@company_id, 'Caja', 'Caja', 'quantity', 1.0),
    (@company_id, 'Paquete', 'Paq', 'quantity', 1.0);
    PRINT '✓ Unidades de medida creadas';
END
GO

-- =============================================
-- 7. PRODUCTOS DE EJEMPLO
-- =============================================
DECLARE @company_id BIGINT = (SELECT id FROM [core].[companies] WHERE tax_id = 'DEMO123456789');
DECLARE @cat_beb_alc BIGINT = (SELECT id FROM [inventory].[categories] WHERE company_id = @company_id AND code = 'BEB-ALC');
DECLARE @cat_beb_noalc BIGINT = (SELECT id FROM [inventory].[categories] WHERE company_id = @company_id AND code = 'BEB-NOALC');
DECLARE @cat_alim BIGINT = (SELECT id FROM [inventory].[categories] WHERE company_id = @company_id AND code = 'ALIM');
DECLARE @unit_pza BIGINT = (SELECT id FROM [inventory].[units] WHERE company_id = @company_id AND abbreviation = 'Pza');
DECLARE @unit_bot BIGINT = (SELECT id FROM [inventory].[units] WHERE company_id = @company_id AND abbreviation = 'Bot');

IF NOT EXISTS (SELECT 1 FROM [inventory].[products] WHERE company_id = @company_id)
BEGIN
    INSERT INTO [inventory].[products] (
        company_id, name, sku, category_id, unit_id,
        cost_price, sale_price, min_stock, reorder_point, is_perishable
    ) VALUES 
    (@company_id, 'Cerveza Corona 355ml', 'BEB-001', @cat_beb_alc, @unit_bot, 18.00, 35.00, 24, 48, 0),
    (@company_id, 'Cerveza Modelo Especial 355ml', 'BEB-002', @cat_beb_alc, @unit_bot, 17.00, 33.00, 24, 48, 0),
    (@company_id, 'Coca-Cola 600ml', 'BEB-003', @cat_beb_noalc, @unit_bot, 12.00, 25.00, 30, 60, 0),
    (@company_id, 'Agua Mineral 1L', 'BEB-004', @cat_beb_noalc, @unit_bot, 8.00, 18.00, 40, 80, 0),
    (@company_id, 'Hamburguesa Clásica', 'ALIM-001', @cat_alim, @unit_pza, 45.00, 120.00, 0, 0, 1),
    (@company_id, 'Pizza Margarita', 'ALIM-002', @cat_alim, @unit_pza, 60.00, 180.00, 0, 0, 1),
    (@company_id, 'Tacos al Pastor (3 pzas)', 'ALIM-003', @cat_alim, @unit_pza, 30.00, 85.00, 0, 0, 1);
    PRINT '✓ Productos de ejemplo creados';
END
GO

-- =============================================
-- 8. STOCK INICIAL
-- =============================================
DECLARE @company_id BIGINT = (SELECT id FROM [core].[companies] WHERE tax_id = 'DEMO123456789');
DECLARE @branch_bar_id BIGINT = (SELECT id FROM [core].[branches] WHERE company_id = @company_id AND code = 'BAR-01');

IF NOT EXISTS (SELECT 1 FROM [inventory].[stock] WHERE company_id = @company_id)
BEGIN
    INSERT INTO [inventory].[stock] (
        company_id, branch_id, product_id, quantity
    )
    SELECT 
        @company_id,
        @branch_bar_id,
        p.id,
        CASE 
            WHEN p.sku LIKE 'BEB%' THEN 50
            WHEN p.sku LIKE 'ALIM%' THEN 0
            ELSE 100
        END
    FROM [inventory].[products] p
    WHERE p.company_id = @company_id;
    
    PRINT '✓ Stock inicial creado';
END
GO

PRINT '========================================';
PRINT '✓✓✓ DATOS INICIALES CARGADOS ✓✓✓';
PRINT '========================================';
PRINT 'Empresa: Mi Bar & Restaurante';
PRINT 'Usuario: admin@mibar.com';
PRINT 'Password: Admin123!';
PRINT '========================================';
