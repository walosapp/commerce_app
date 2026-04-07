-- =============================================
-- Script: 206_create_table_inventory_alerts.sql
-- Descripción: Alertas de inventario generadas por IA
-- ¿Qué es? Notificaciones automáticas sobre stock, vencimientos, etc.
-- ¿Para qué? Gestión proactiva del inventario con IA
-- =============================================

USE WalosDB;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[inventory].[alerts]') AND type in (N'U'))
BEGIN
    CREATE TABLE [inventory].[alerts] (
        -- Identificador único
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        
        -- Multi-tenant
        company_id BIGINT NOT NULL,
        branch_id BIGINT NOT NULL,
        product_id BIGINT NULL, -- NULL para alertas generales
        
        -- Tipo de alerta
        alert_type NVARCHAR(50) NOT NULL, -- low_stock, out_of_stock, expiring_soon, expired, overstocked
        severity NVARCHAR(20) DEFAULT 'medium', -- low, medium, high, critical
        
        -- Mensaje
        title NVARCHAR(200) NOT NULL,
        message NVARCHAR(1000) NOT NULL,
        
        -- Datos de la alerta
        current_value DECIMAL(18, 3) NULL, -- Valor actual (ej: stock actual)
        threshold_value DECIMAL(18, 3) NULL, -- Valor umbral (ej: stock mínimo)
        
        -- Sugerencia de la IA
        ai_suggestion NVARCHAR(1000) NULL,
        suggested_action NVARCHAR(50) NULL, -- reorder, adjust_price, check_expiry
        
        -- Estado
        status NVARCHAR(20) DEFAULT 'active', -- active, acknowledged, resolved, dismissed
        acknowledged_by BIGINT NULL,
        acknowledged_at DATETIME2 NULL,
        resolved_at DATETIME2 NULL,
        
        -- Notificación
        notification_sent BIT DEFAULT 0,
        notification_sent_at DATETIME2 NULL,
        
        -- Auditoría
        created_at DATETIME2 DEFAULT GETDATE(),
        updated_at DATETIME2 DEFAULT GETDATE(),
        
        -- Foreign Keys
        CONSTRAINT FK_inventory_alerts_company FOREIGN KEY (company_id) 
            REFERENCES [core].[companies](id),
        CONSTRAINT FK_inventory_alerts_branch FOREIGN KEY (branch_id) 
            REFERENCES [core].[branches](id),
        CONSTRAINT FK_inventory_alerts_product FOREIGN KEY (product_id) 
            REFERENCES [inventory].[products](id),
        CONSTRAINT FK_inventory_alerts_acknowledged_by FOREIGN KEY (acknowledged_by) 
            REFERENCES [core].[users](id),
        
        -- Índices
        INDEX IX_inventory_alerts_company_id (company_id),
        INDEX IX_inventory_alerts_branch_id (branch_id),
        INDEX IX_inventory_alerts_product_id (product_id),
        INDEX IX_inventory_alerts_type (alert_type),
        INDEX IX_inventory_alerts_status (status),
        INDEX IX_inventory_alerts_created_at (created_at)
    );
    
    PRINT '✓ Tabla [inventory].[alerts] creada';
END
GO

PRINT '✓ Script 206 ejecutado correctamente';
