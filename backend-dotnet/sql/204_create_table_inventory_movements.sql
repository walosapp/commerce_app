-- =============================================
-- Script: 204_create_table_inventory_movements.sql
-- Descripción: Movimientos de inventario (entradas/salidas)
-- ¿Qué es? Registro histórico de todos los movimientos de stock
-- ¿Para qué? Trazabilidad completa, auditoría y análisis de inventario
-- =============================================

USE WalosDB;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[inventory].[movements]') AND type in (N'U'))
BEGIN
    CREATE TABLE [inventory].[movements] (
        -- Identificador único
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        
        -- Multi-tenant
        company_id BIGINT NOT NULL,
        branch_id BIGINT NOT NULL,
        product_id BIGINT NOT NULL,
        
        -- Tipo de movimiento
        movement_type NVARCHAR(50) NOT NULL, -- purchase, sale, adjustment, transfer, waste, return
        
        -- Cantidades
        quantity DECIMAL(18, 3) NOT NULL, -- Positivo=entrada, Negativo=salida
        unit_cost DECIMAL(18, 2) NULL, -- Costo unitario en este movimiento
        total_cost AS (ABS(quantity) * ISNULL(unit_cost, 0)) PERSISTED,
        
        -- Referencia a documento origen
        reference_type NVARCHAR(50) NULL, -- purchase_order, sale, adjustment, etc.
        reference_id BIGINT NULL, -- ID del documento relacionado
        
        -- Información adicional
        notes NVARCHAR(1000) NULL,
        
        -- Transferencias entre sucursales
        from_branch_id BIGINT NULL, -- Sucursal origen (para transferencias)
        to_branch_id BIGINT NULL, -- Sucursal destino (para transferencias)
        
        -- Stock después del movimiento
        stock_after DECIMAL(18, 3) NULL,
        
        -- Asistencia de IA
        created_by_ai BIT DEFAULT 0, -- ¿Fue creado por el asistente de IA?
        ai_confidence DECIMAL(5, 2) NULL, -- Nivel de confianza de la IA (0-100)
        ai_metadata NVARCHAR(MAX) NULL, -- JSON con datos de la IA
        
        -- Auditoría
        created_at DATETIME2 DEFAULT GETDATE(),
        created_by BIGINT NULL,
        
        -- Foreign Keys
        CONSTRAINT FK_inventory_movements_company FOREIGN KEY (company_id) 
            REFERENCES [core].[companies](id),
        CONSTRAINT FK_inventory_movements_branch FOREIGN KEY (branch_id) 
            REFERENCES [core].[branches](id),
        CONSTRAINT FK_inventory_movements_product FOREIGN KEY (product_id) 
            REFERENCES [inventory].[products](id),
        CONSTRAINT FK_inventory_movements_from_branch FOREIGN KEY (from_branch_id) 
            REFERENCES [core].[branches](id),
        CONSTRAINT FK_inventory_movements_to_branch FOREIGN KEY (to_branch_id) 
            REFERENCES [core].[branches](id),
        
        -- Índices
        INDEX IX_inventory_movements_company_id (company_id),
        INDEX IX_inventory_movements_branch_id (branch_id),
        INDEX IX_inventory_movements_product_id (product_id),
        INDEX IX_inventory_movements_type (movement_type),
        INDEX IX_inventory_movements_created_at (created_at),
        INDEX IX_inventory_movements_reference (reference_type, reference_id)
    );
    
    PRINT '✓ Tabla [inventory].[movements] creada';
END
GO

PRINT '✓ Script 204 ejecutado correctamente';
