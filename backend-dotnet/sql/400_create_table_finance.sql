USE [SCM_App_Track_Me];
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'finance')
BEGIN
    EXEC('CREATE SCHEMA [finance]');
END
GO

IF OBJECT_ID('[finance].[categories]', 'U') IS NULL
BEGIN
    CREATE TABLE [finance].[categories] (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        company_id BIGINT NOT NULL,
        name NVARCHAR(150) NOT NULL,
        type NVARCHAR(20) NOT NULL,
        color_hex NVARCHAR(20) NULL,
        is_system BIT NOT NULL CONSTRAINT DF_finance_categories_is_system DEFAULT 0,
        is_active BIT NOT NULL CONSTRAINT DF_finance_categories_is_active DEFAULT 1,
        created_by BIGINT NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_finance_categories_created_at DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NULL,
        deleted_at DATETIME2 NULL
    );

    CREATE INDEX IX_finance_categories_company_type ON [finance].[categories](company_id, type) WHERE deleted_at IS NULL;
END
GO

IF OBJECT_ID('[finance].[entries]', 'U') IS NULL
BEGIN
    CREATE TABLE [finance].[entries] (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        company_id BIGINT NOT NULL,
        branch_id BIGINT NULL,
        category_id BIGINT NOT NULL,
        type NVARCHAR(20) NOT NULL,
        description NVARCHAR(250) NOT NULL,
        amount DECIMAL(18,2) NOT NULL,
        entry_date DATETIME2 NOT NULL,
        nature NVARCHAR(20) NULL,
        frequency NVARCHAR(20) NULL,
        notes NVARCHAR(1000) NULL,
        created_by BIGINT NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_finance_entries_created_at DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NULL,
        deleted_at DATETIME2 NULL,
        CONSTRAINT FK_finance_entries_category FOREIGN KEY (category_id) REFERENCES [finance].[categories](id)
    );

    CREATE INDEX IX_finance_entries_company_date ON [finance].[entries](company_id, entry_date DESC) WHERE deleted_at IS NULL;
    CREATE INDEX IX_finance_entries_company_category ON [finance].[entries](company_id, category_id) WHERE deleted_at IS NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM [finance].[categories] WHERE company_id = 1 AND name = 'Arriendo')
BEGIN
    INSERT INTO [finance].[categories] (company_id, name, type, color_hex, is_system)
    VALUES
        (1, 'Arriendo', 'expense', '#F97316', 1),
        (1, 'Servicios publicos', 'expense', '#EF4444', 1),
        (1, 'Nomina', 'expense', '#8B5CF6', 1),
        (1, 'Propinas', 'expense', '#06B6D4', 1),
        (1, 'Ingreso adicional', 'income', '#22C55E', 1),
        (1, 'Eventos', 'income', '#0EA5E9', 1);
END
GO
