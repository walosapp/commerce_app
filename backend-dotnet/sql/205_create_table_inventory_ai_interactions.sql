-- =============================================
-- Script: 205_create_table_inventory_ai_interactions.sql
-- Descripción: Interacciones con el asistente de IA de inventario
-- ¿Qué es? Registro de conversaciones y acciones del asistente de IA
-- ¿Para qué? Mejorar la IA, auditoría y análisis de uso
-- =============================================

USE WalosDB;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[inventory].[ai_interactions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [inventory].[ai_interactions] (
        -- Identificador único
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        
        -- Multi-tenant
        company_id BIGINT NOT NULL,
        branch_id BIGINT NOT NULL,
        user_id BIGINT NOT NULL,
        
        -- Sesión de conversación
        session_id NVARCHAR(100) NOT NULL, -- UUID para agrupar conversaciones
        
        -- Tipo de interacción
        interaction_type NVARCHAR(50) NOT NULL, -- voice, text, suggestion, alert
        
        -- Entrada del usuario
        user_input NVARCHAR(MAX) NULL, -- Texto o transcripción de voz
        user_input_language NVARCHAR(5) DEFAULT 'es',
        
        -- Respuesta de la IA
        ai_response NVARCHAR(MAX) NULL,
        ai_action NVARCHAR(50) NULL, -- add_stock, alert_low_stock, calculate_margin, etc.
        
        -- Datos procesados por la IA (JSON)
        processed_data NVARCHAR(MAX) NULL,
        -- Ejemplo: {"products": [{"name": "Cerveza Corona", "quantity": 24, "cost": 480}]}
        
        -- Resultado de la acción
        action_status NVARCHAR(20) DEFAULT 'pending', -- pending, success, failed, cancelled
        action_result NVARCHAR(MAX) NULL, -- JSON con resultado
        
        -- Confianza y validación
        confidence_score DECIMAL(5, 2) NULL, -- 0-100
        requires_confirmation BIT DEFAULT 1,
        confirmed_by_user BIT DEFAULT 0,
        confirmed_at DATETIME2 NULL,
        
        -- Metadata de la IA
        ai_model NVARCHAR(50) NULL, -- gpt-4, gpt-3.5-turbo, etc.
        tokens_used INT NULL,
        processing_time_ms INT NULL,
        
        -- Auditoría
        created_at DATETIME2 DEFAULT GETDATE(),
        
        -- Foreign Keys
        CONSTRAINT FK_inventory_ai_interactions_company FOREIGN KEY (company_id) 
            REFERENCES [core].[companies](id),
        CONSTRAINT FK_inventory_ai_interactions_branch FOREIGN KEY (branch_id) 
            REFERENCES [core].[branches](id),
        CONSTRAINT FK_inventory_ai_interactions_user FOREIGN KEY (user_id) 
            REFERENCES [core].[users](id),
        
        -- Índices
        INDEX IX_inventory_ai_interactions_company_id (company_id),
        INDEX IX_inventory_ai_interactions_branch_id (branch_id),
        INDEX IX_inventory_ai_interactions_user_id (user_id),
        INDEX IX_inventory_ai_interactions_session_id (session_id),
        INDEX IX_inventory_ai_interactions_created_at (created_at)
    );
    
    PRINT '✓ Tabla [inventory].[ai_interactions] creada';
END
GO

PRINT '✓ Script 205 ejecutado correctamente';
