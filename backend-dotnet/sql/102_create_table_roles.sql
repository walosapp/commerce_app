-- =============================================
-- Script: 102_create_table_roles.sql
-- Descripción: Tabla de roles del sistema
-- ¿Qué es? Define los diferentes roles de usuario (admin, gerente, mesero, etc.)
-- ¿Para qué? Control de acceso basado en roles (RBAC)
-- =============================================

USE WalosDB;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[core].[roles]') AND type in (N'U'))
BEGIN
    CREATE TABLE [core].[roles] (
        -- Identificador único
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        
        -- Relación con empresa
        company_id BIGINT NOT NULL,
        
        -- Información del rol
        name NVARCHAR(100) NOT NULL,
        code NVARCHAR(50) NOT NULL, -- super_admin, admin, manager, waiter, chef, cashier
        description NVARCHAR(500) NULL,
        
        -- Permisos (JSON con estructura de permisos)
        -- Ejemplo: {"inventory": {"read": true, "write": true}, "sales": {"read": true}}
        permissions NVARCHAR(MAX) NOT NULL,
        
        -- Nivel de acceso
        access_level INT DEFAULT 1, -- 1=básico, 5=medio, 10=completo
        
        -- Tipo de rol
        is_system_role BIT DEFAULT 0, -- Roles del sistema no se pueden eliminar
        is_active BIT DEFAULT 1,
        
        -- Auditoría
        created_at DATETIME2 DEFAULT GETDATE(),
        updated_at DATETIME2 DEFAULT GETDATE(),
        deleted_at DATETIME2 NULL,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        
        -- Foreign Keys
        CONSTRAINT FK_roles_company FOREIGN KEY (company_id) 
            REFERENCES [core].[companies](id),
        
        -- Constraints
        CONSTRAINT UQ_roles_company_code UNIQUE (company_id, code),
        
        -- Índices
        INDEX IX_roles_company_id (company_id),
        INDEX IX_roles_code (code),
        INDEX IX_roles_is_active (is_active)
    );
    
    PRINT '✓ Tabla [core].[roles] creada';
END
ELSE
BEGIN
    PRINT 'La tabla [core].[roles] ya existe';
END
GO

EXEC sys.sp_addextendedproperty 
    @name=N'MS_Description', 
    @value=N'Roles de usuario para control de acceso', 
    @level0type=N'SCHEMA', @level0name=N'core',
    @level1type=N'TABLE', @level1name=N'roles';
GO

PRINT '✓ Script 102 ejecutado correctamente';
