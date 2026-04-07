-- =============================================
-- Script: 203_create_table_inventory_stock.sql
-- Descripción: Stock actual por sucursal
-- ¿Qué es? Cantidad disponible de cada producto en cada sucursal
-- ¿Para qué? Control en tiempo real del inventario por ubicación
-- =============================================

USE WalosDB;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[inventory].[stock]') AND type in (N'U'))
BEGIN
    CREATE TABLE [inventory].[stock] (
        -- Identificador único
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        
        -- Multi-tenant
        company_id BIGINT NOT NULL,
        branch_id BIGINT NOT NULL,
        product_id BIGINT NOT NULL,
        
        -- Cantidades
        quantity DECIMAL(18, 3) NOT NULL DEFAULT 0, -- Cantidad actual
        reserved_quantity DECIMAL(18, 3) DEFAULT 0, -- Cantidad reservada (pedidos)
        available_quantity AS (quantity - reserved_quantity) PERSISTED, -- Disponible
        
        -- Ubicación física (opcional)
        location NVARCHAR(100) NULL, -- Ej: "Almacén A - Estante 3"
        
        -- Última actualización de stock
        last_stock_count_at DATETIME2 NULL,
        last_stock_count_by BIGINT NULL,
        
        -- Auditoría
        created_at DATETIME2 DEFAULT GETDATE(),
        updated_at DATETIME2 DEFAULT GETDATE(),
        
        -- Foreign Keys
        CONSTRAINT FK_inventory_stock_company FOREIGN KEY (company_id) 
            REFERENCES [core].[companies](id),
        CONSTRAINT FK_inventory_stock_branch FOREIGN KEY (branch_id) 
            REFERENCES [core].[branches](id),
        CONSTRAINT FK_inventory_stock_product FOREIGN KEY (product_id) 
            REFERENCES [inventory].[products](id),
        
        -- Constraints
        CONSTRAINT UQ_inventory_stock_branch_product UNIQUE (branch_id, product_id),
        CONSTRAINT CK_inventory_stock_quantity CHECK (quantity >= 0),
        CONSTRAINT CK_inventory_stock_reserved CHECK (reserved_quantity >= 0),
        
        -- Índices
        INDEX IX_inventory_stock_company_id (company_id),
        INDEX IX_inventory_stock_branch_id (branch_id),
        INDEX IX_inventory_stock_product_id (product_id),
        INDEX IX_inventory_stock_quantity (quantity)
    );
    
    PRINT '✓ Tabla [inventory].[stock] creada';
END
GO

PRINT '✓ Script 203 ejecutado correctamente';
