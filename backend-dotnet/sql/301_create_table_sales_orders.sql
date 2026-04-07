-- =============================================
-- 301: Crear tabla sales.orders (pedidos/ventas)
-- =============================================

USE [SCM_App_Track_Me];
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[sales].[orders]') AND type = 'U')
BEGIN
    CREATE TABLE [sales].[orders] (
        [id]            BIGINT IDENTITY(1,1) PRIMARY KEY,
        [company_id]    BIGINT NOT NULL,
        [branch_id]     BIGINT NOT NULL,
        [table_id]      BIGINT NOT NULL,
        [order_number]  NVARCHAR(50) NOT NULL,
        [status]        NVARCHAR(20) NOT NULL DEFAULT 'pending', -- pending, completed, cancelled
        [subtotal]      DECIMAL(18,2) NOT NULL DEFAULT 0,
        [tax]           DECIMAL(18,2) NOT NULL DEFAULT 0,
        [total]         DECIMAL(18,2) NOT NULL DEFAULT 0,
        [notes]         NVARCHAR(500) NULL,
        [created_by]    BIGINT NULL,
        [created_at]    DATETIME2 NOT NULL DEFAULT GETDATE(),
        [updated_at]    DATETIME2 NULL,
        [deleted_at]    DATETIME2 NULL,

        CONSTRAINT [FK_sales_orders_company] FOREIGN KEY ([company_id]) REFERENCES [core].[companies]([id]),
        CONSTRAINT [FK_sales_orders_branch] FOREIGN KEY ([branch_id]) REFERENCES [core].[branches]([id]),
        CONSTRAINT [FK_sales_orders_table] FOREIGN KEY ([table_id]) REFERENCES [sales].[tables]([id])
    );

    CREATE INDEX [IX_sales_orders_company_branch] ON [sales].[orders]([company_id], [branch_id]);
    CREATE INDEX [IX_sales_orders_table] ON [sales].[orders]([table_id]);

    PRINT '✓ Tabla [sales].[orders] creada';
END
ELSE
    PRINT '→ Tabla [sales].[orders] ya existe';
GO

IF COL_LENGTH('[sales].[orders]', 'discount_type') IS NULL
BEGIN
    ALTER TABLE [sales].[orders]
    ADD discount_type NVARCHAR(20) NULL;
END
GO

IF COL_LENGTH('[sales].[orders]', 'discount_value') IS NULL
BEGIN
    ALTER TABLE [sales].[orders]
    ADD discount_value DECIMAL(18,2) NULL;
END
GO

IF COL_LENGTH('[sales].[orders]', 'discount_amount') IS NULL
BEGIN
    ALTER TABLE [sales].[orders]
    ADD discount_amount DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_sales_orders_discount_amount DEFAULT 0;
END
GO

IF COL_LENGTH('[sales].[orders]', 'final_total_paid') IS NULL
BEGIN
    ALTER TABLE [sales].[orders]
    ADD final_total_paid DECIMAL(18,2) NULL;
END
GO

IF COL_LENGTH('[sales].[orders]', 'split_reference_count') IS NULL
BEGIN
    ALTER TABLE [sales].[orders]
    ADD split_reference_count INT NOT NULL
        CONSTRAINT DF_sales_orders_split_reference_count DEFAULT 1;
END
GO
