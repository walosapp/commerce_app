-- =============================================
-- Script: 201_create_table_inventory_units.sql
-- Descripción: Unidades de medida para productos
-- ¿Qué es? Define cómo se miden los productos (litros, kg, piezas, etc.)
-- ¿Para qué? Estandarizar mediciones y conversiones de inventario
-- =============================================

USE WalosDB;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[inventory].[units]') AND type in (N'U'))
BEGIN
    CREATE TABLE [inventory].[units] (
        -- Identificador único
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        
        -- Multi-tenant
        company_id BIGINT NOT NULL,
        
        -- Información de la unidad
        name NVARCHAR(100) NOT NULL, -- Litro, Kilogramo, Pieza, Botella
        abbreviation NVARCHAR(10) NOT NULL, -- L, Kg, Pza, Bot
        
        -- Tipo de unidad
        unit_type NVARCHAR(50) NOT NULL, -- volume, weight, quantity, length
        
        -- Conversión a unidad base (para cálculos)
        base_unit_id BIGINT NULL, -- Referencia a la unidad base
        conversion_factor DECIMAL(18, 6) DEFAULT 1.0, -- Factor de conversión
        -- Ejemplo: 1 litro = 1000 mililitros (conversion_factor = 1000)
        
        -- Estado
        is_active BIT DEFAULT 1,
        
        -- Auditoría
        created_at DATETIME2 DEFAULT GETDATE(),
        updated_at DATETIME2 DEFAULT GETDATE(),
        deleted_at DATETIME2 NULL,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        
        -- Foreign Keys
        CONSTRAINT FK_inventory_units_company FOREIGN KEY (company_id) 
            REFERENCES [core].[companies](id),
        CONSTRAINT FK_inventory_units_base FOREIGN KEY (base_unit_id) 
            REFERENCES [inventory].[units](id),
        
        -- Constraints
        CONSTRAINT UQ_inventory_units_company_abbreviation UNIQUE (company_id, abbreviation),
        
        -- Índices
        INDEX IX_inventory_units_company_id (company_id),
        INDEX IX_inventory_units_type (unit_type)
    );
    
    PRINT '✓ Tabla [inventory].[units] creada';
END
GO

PRINT '✓ Script 201 ejecutado correctamente';
