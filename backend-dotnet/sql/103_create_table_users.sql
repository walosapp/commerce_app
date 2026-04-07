-- =============================================
-- Script: 103_create_table_users.sql
-- Descripción: Tabla de usuarios del sistema
-- ¿Qué es? Almacena todos los usuarios que acceden al sistema
-- ¿Para qué? Autenticación, autorización y gestión de personal
-- =============================================

USE WalosDB;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[core].[users]') AND type in (N'U'))
BEGIN
    CREATE TABLE [core].[users] (
        -- Identificador único
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        
        -- Relación con empresa y sucursal
        company_id BIGINT NOT NULL,
        branch_id BIGINT NULL, -- NULL = acceso a todas las sucursales
        role_id BIGINT NOT NULL,
        
        -- Información personal
        first_name NVARCHAR(100) NOT NULL,
        last_name NVARCHAR(100) NOT NULL,
        email NVARCHAR(100) NOT NULL,
        phone NVARCHAR(20) NULL,
        
        -- Autenticación
        password_hash NVARCHAR(255) NOT NULL, -- bcrypt hash
        password_salt NVARCHAR(100) NULL,
        
        -- Tokens
        refresh_token NVARCHAR(500) NULL,
        refresh_token_expires_at DATETIME2 NULL,
        reset_password_token NVARCHAR(100) NULL,
        reset_password_expires_at DATETIME2 NULL,
        
        -- Configuración de usuario
        language NVARCHAR(5) DEFAULT 'es', -- Preferencia de idioma
        timezone NVARCHAR(50) NULL,
        avatar_url NVARCHAR(500) NULL,
        
        -- Seguridad
        failed_login_attempts INT DEFAULT 0,
        locked_until DATETIME2 NULL,
        last_login_at DATETIME2 NULL,
        last_login_ip NVARCHAR(45) NULL,
        
        -- Estado
        is_active BIT DEFAULT 1,
        email_verified BIT DEFAULT 0,
        email_verified_at DATETIME2 NULL,
        
        -- Auditoría
        created_at DATETIME2 DEFAULT GETDATE(),
        updated_at DATETIME2 DEFAULT GETDATE(),
        deleted_at DATETIME2 NULL,
        created_by BIGINT NULL,
        updated_by BIGINT NULL,
        
        -- Foreign Keys
        CONSTRAINT FK_users_company FOREIGN KEY (company_id) 
            REFERENCES [core].[companies](id),
        CONSTRAINT FK_users_branch FOREIGN KEY (branch_id) 
            REFERENCES [core].[branches](id),
        CONSTRAINT FK_users_role FOREIGN KEY (role_id) 
            REFERENCES [core].[roles](id),
        
        -- Constraints
        CONSTRAINT UQ_users_email UNIQUE (email),
        
        -- Índices
        INDEX IX_users_company_id (company_id),
        INDEX IX_users_branch_id (branch_id),
        INDEX IX_users_role_id (role_id),
        INDEX IX_users_email (email),
        INDEX IX_users_is_active (is_active),
        INDEX IX_users_deleted_at (deleted_at)
    );
    
    PRINT '✓ Tabla [core].[users] creada';
END
ELSE
BEGIN
    PRINT 'La tabla [core].[users] ya existe';
END
GO

EXEC sys.sp_addextendedproperty 
    @name=N'MS_Description', 
    @value=N'Usuarios del sistema con autenticación y autorización', 
    @level0type=N'SCHEMA', @level0name=N'core',
    @level1type=N'TABLE', @level1name=N'users';
GO

PRINT '✓ Script 103 ejecutado correctamente';
