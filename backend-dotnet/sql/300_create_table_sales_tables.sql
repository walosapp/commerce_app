-- =============================================
-- 300: Crear tabla sales.tables (mesas)
-- =============================================

USE [SCM_App_Track_Me];
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[sales].[tables]') AND type = 'U')
BEGIN
    CREATE TABLE [sales].[tables] (
        [id]            BIGINT IDENTITY(1,1) PRIMARY KEY,
        [company_id]    BIGINT NOT NULL,
        [branch_id]     BIGINT NOT NULL,
        [table_number]  INT NOT NULL,
        [name]          NVARCHAR(100) NOT NULL DEFAULT '',
        [status]        NVARCHAR(20) NOT NULL DEFAULT 'open', -- open, invoiced, cancelled
        [created_by]    BIGINT NULL,
        [created_at]    DATETIME2 NOT NULL DEFAULT GETDATE(),
        [updated_at]    DATETIME2 NULL,
        [deleted_at]    DATETIME2 NULL,

        CONSTRAINT [FK_sales_tables_company] FOREIGN KEY ([company_id]) REFERENCES [core].[companies]([id]),
        CONSTRAINT [FK_sales_tables_branch] FOREIGN KEY ([branch_id]) REFERENCES [core].[branches]([id])
    );

    CREATE INDEX [IX_sales_tables_company_branch] ON [sales].[tables]([company_id], [branch_id]);
    CREATE INDEX [IX_sales_tables_status] ON [sales].[tables]([status]);

    PRINT '✓ Tabla [sales].[tables] creada';
END
ELSE
    PRINT '→ Tabla [sales].[tables] ya existe';
GO
