-- =============================================
-- Script: 202_create_table_inventory_products.sql
-- Descripción: Productos del inventario
-- ¿Qué es? Catálogo maestro de todos los productos
-- ¿Para qué? Gestionar información de productos, precios y stock
-- =============================================

USE WalosDB;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[inventory].[products]') AND type in (N'U'))
BEGIN
    CREATE TABLE [inventory].[products] (
        -- Identificador único
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        
        -- Multi-tenant
        company_id BIGINT NOT NULL,
        
        -- Información básica
        name NVARCHAR(200) NOT NULL,
        sku NVARCHAR(50) NOT NULL, -- Stock Keeping Unit (código único)
        barcode NVARCHAR(100) NULL, -- Código de barras
        description NVARCHAR(1000) NULL,
        
        -- Clasificación
        category_id BIGINT NOT NULL,
        unit_id BIGINT NOT NULL,
        
        -- Imágenes
        image_url NVARCHAR(500) NULL,
        thumbnail_url NVARCHAR(500) NULL,
        
        -- Precios
        cost_price DECIMAL(18, 2) NOT NULL DEFAULT 0, -- Precio de compra
        sale_price DECIMAL(18, 2) NOT NULL DEFAULT 0, -- Precio de venta
        margin_percentage AS ((sale_price - cost_price) / NULLIF(cost_price, 0) * 100) PERSISTED, -- Margen calculado
        
        -- Control de stock
        min_stock DECIMAL(18, 3) DEFAULT 0, -- Stock mínimo (alerta de bajo stock)
        max_stock DECIMAL(18, 3) DEFAULT 0, -- Stock máximo
        reorder_point DECIMAL(18, 3) DEFAULT 0, -- Punto de reorden
        
        -- Configuración
        is_perishable BIT DEFAULT 0, -- ¿Es perecedero?
        shelf_life_days INT NULL, -- Vida útil en días
        
        -- Tipo de producto
        product_type NVARCHAR(50) DEFAULT 'simple', -- simple, composite, service
        
        -- Estado
        is_active BIT DEFAULT 1,
        is_for_sale BIT DEFAULT 1, -- ¿Se vende al público?
        
        -- Auditoría
        created_at DATETIME2 DEFAULT GETDATE(),
        updated_at DATETIME2 DEFAULT GETDATE(),
        deleted_at DATETIME2 NULL,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        
        -- Foreign Keys
        CONSTRAINT FK_inventory_products_company FOREIGN KEY (company_id) 
            REFERENCES [core].[companies](id),
        CONSTRAINT FK_inventory_products_category FOREIGN KEY (category_id) 
            REFERENCES [inventory].[categories](id),
        CONSTRAINT FK_inventory_products_unit FOREIGN KEY (unit_id) 
            REFERENCES [inventory].[units](id),
        
        -- Constraints
        CONSTRAINT UQ_inventory_products_company_sku UNIQUE (company_id, sku),
        CONSTRAINT CK_inventory_products_prices CHECK (cost_price >= 0 AND sale_price >= 0),
        
        -- Índices
        INDEX IX_inventory_products_company_id (company_id),
        INDEX IX_inventory_products_category_id (category_id),
        INDEX IX_inventory_products_sku (sku),
        INDEX IX_inventory_products_barcode (barcode),
        INDEX IX_inventory_products_is_active (is_active)
    );
    
    PRINT '✓ Tabla [inventory].[products] creada';
END
GO

PRINT '✓ Script 202 ejecutado correctamente';
