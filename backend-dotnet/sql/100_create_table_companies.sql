-- =============================================
-- Script: 100_create_table_companies.sql
-- Descripción: Tabla de empresas (multi-tenant)
-- ¿Qué es? Almacena las empresas que usan el sistema
-- ¿Para qué? Permite que múltiples negocios usen la misma aplicación de forma aislada
-- =============================================

USE WalosDB;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[core].[companies]') AND type in (N'U'))
BEGIN
    CREATE TABLE [core].[companies] (
        -- Identificador único de la empresa
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        
        -- Información básica
        name NVARCHAR(200) NOT NULL,
        legal_name NVARCHAR(300) NOT NULL,
        tax_id NVARCHAR(50) NOT NULL UNIQUE, -- RFC, RUT, CUIT según país
        
        -- Contacto
        email NVARCHAR(100) NULL,
        phone NVARCHAR(20) NULL,
        website NVARCHAR(200) NULL,
        
        -- Dirección
        address NVARCHAR(500) NULL,
        city NVARCHAR(100) NULL,
        state NVARCHAR(100) NULL,
        country NVARCHAR(2) DEFAULT 'MX', -- ISO 3166-1 alpha-2
        postal_code NVARCHAR(10) NULL,
        
        -- Configuración
        currency NVARCHAR(3) DEFAULT 'MXN', -- ISO 4217
        timezone NVARCHAR(50) DEFAULT 'America/Mexico_City',
        language NVARCHAR(5) DEFAULT 'es', -- ISO 639-1
        
        -- Logo y branding
        logo_url NVARCHAR(500) NULL,
        primary_color NVARCHAR(7) DEFAULT '#1a73e8',
        
        -- Estado y control
        is_active BIT DEFAULT 1,
        subscription_plan NVARCHAR(50) DEFAULT 'basic', -- basic, premium, enterprise
        subscription_expires_at DATETIME2 NULL,
        
        -- Auditoría
        created_at DATETIME2 DEFAULT GETDATE(),
        updated_at DATETIME2 DEFAULT GETDATE(),
        deleted_at DATETIME2 NULL, -- Soft delete
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        
        -- Índices
        INDEX IX_companies_tax_id (tax_id),
        INDEX IX_companies_is_active (is_active),
        INDEX IX_companies_deleted_at (deleted_at)
    );
    
    PRINT '✓ Tabla [core].[companies] creada';
END
ELSE
BEGIN
    PRINT 'La tabla [core].[companies] ya existe';
END
GO

-- Comentarios en la tabla
EXEC sys.sp_addextendedproperty 
    @name=N'MS_Description', 
    @value=N'Empresas registradas en el sistema (multi-tenant)', 
    @level0type=N'SCHEMA', @level0name=N'core',
    @level1type=N'TABLE', @level1name=N'companies';
GO

PRINT '✓ Script 100 ejecutado correctamente';
