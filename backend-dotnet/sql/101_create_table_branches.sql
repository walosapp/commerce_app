-- =============================================
-- Script: 101_create_table_branches.sql
-- Descripción: Tabla de sucursales
-- ¿Qué es? Almacena las diferentes sucursales de cada empresa
-- ¿Para qué? Permite gestionar múltiples ubicaciones (bar, restaurante, etc.)
-- =============================================

USE WalosDB;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[core].[branches]') AND type in (N'U'))
BEGIN
    CREATE TABLE [core].[branches] (
        -- Identificador único
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        
        -- Relación con empresa (multi-tenant)
        company_id BIGINT NOT NULL,
        
        -- Información básica
        name NVARCHAR(200) NOT NULL,
        code NVARCHAR(20) NOT NULL, -- Código interno (ej: BAR-01, REST-01)
        branch_type NVARCHAR(50) NOT NULL, -- bar, restaurant, cafe, mixed
        
        -- Contacto
        email NVARCHAR(100) NULL,
        phone NVARCHAR(20) NULL,
        
        -- Dirección
        address NVARCHAR(500) NOT NULL,
        city NVARCHAR(100) NOT NULL,
        state NVARCHAR(100) NULL,
        country NVARCHAR(2) DEFAULT 'MX',
        postal_code NVARCHAR(10) NULL,
        
        -- Geolocalización (para futuras funcionalidades)
        latitude DECIMAL(10, 8) NULL,
        longitude DECIMAL(11, 8) NULL,
        
        -- Horarios (JSON con horarios por día)
        business_hours NVARCHAR(MAX) NULL, -- {"monday": {"open": "09:00", "close": "22:00"}, ...}
        
        -- Configuración
        max_tables INT NULL, -- Número de mesas (para restaurante)
        max_capacity INT NULL, -- Capacidad de personas
        
        -- Estado
        is_active BIT DEFAULT 1,
        is_main BIT DEFAULT 0, -- Sucursal principal
        
        -- Auditoría
        created_at DATETIME2 DEFAULT GETDATE(),
        updated_at DATETIME2 DEFAULT GETDATE(),
        deleted_at DATETIME2 NULL,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        
        -- Foreign Keys
        CONSTRAINT FK_branches_company FOREIGN KEY (company_id) 
            REFERENCES [core].[companies](id),
        
        -- Constraints
        CONSTRAINT UQ_branches_company_code UNIQUE (company_id, code),
        
        -- Índices
        INDEX IX_branches_company_id (company_id),
        INDEX IX_branches_is_active (is_active),
        INDEX IX_branches_deleted_at (deleted_at)
    );
    
    PRINT '✓ Tabla [core].[branches] creada';
END
ELSE
BEGIN
    PRINT 'La tabla [core].[branches] ya existe';
END
GO

EXEC sys.sp_addextendedproperty 
    @name=N'MS_Description', 
    @value=N'Sucursales de cada empresa', 
    @level0type=N'SCHEMA', @level0name=N'core',
    @level1type=N'TABLE', @level1name=N'branches';
GO

PRINT '✓ Script 101 ejecutado correctamente';
