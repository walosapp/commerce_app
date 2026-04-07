-- =============================================
-- Script: 200_create_table_inventory_categories.sql
-- Descripción: Categorías de productos de inventario
-- ¿Qué es? Clasificación de productos (bebidas, alimentos, insumos, etc.)
-- ¿Para qué? Organizar el inventario y facilitar reportes por categoría
-- =============================================

USE WalosDB;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[inventory].[categories]') AND type in (N'U'))
BEGIN
    CREATE TABLE [inventory].[categories] (
        -- Identificador único
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        
        -- Multi-tenant
        company_id BIGINT NOT NULL,
        
        -- Información de la categoría
        name NVARCHAR(100) NOT NULL,
        code NVARCHAR(50) NOT NULL,
        description NVARCHAR(500) NULL,
        
        -- Jerarquía (categorías padre-hijo)
        parent_category_id BIGINT NULL,
        
        -- Configuración
        icon NVARCHAR(50) NULL, -- Nombre del icono (lucide-react)
        color NVARCHAR(7) NULL, -- Color hexadecimal
        
        -- Orden de visualización
        display_order INT DEFAULT 0,
        
        -- Estado
        is_active BIT DEFAULT 1,
        
        -- Auditoría
        created_at DATETIME2 DEFAULT GETDATE(),
        updated_at DATETIME2 DEFAULT GETDATE(),
        deleted_at DATETIME2 NULL,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        
        -- Foreign Keys
        CONSTRAINT FK_inventory_categories_company FOREIGN KEY (company_id) 
            REFERENCES [core].[companies](id),
        CONSTRAINT FK_inventory_categories_parent FOREIGN KEY (parent_category_id) 
            REFERENCES [inventory].[categories](id),
        
        -- Constraints
        CONSTRAINT UQ_inventory_categories_company_code UNIQUE (company_id, code),
        
        -- Índices
        INDEX IX_inventory_categories_company_id (company_id),
        INDEX IX_inventory_categories_parent_id (parent_category_id),
        INDEX IX_inventory_categories_is_active (is_active)
    );
    
    PRINT '✓ Tabla [inventory].[categories] creada';
END
GO

PRINT '✓ Script 200 ejecutado correctamente';
