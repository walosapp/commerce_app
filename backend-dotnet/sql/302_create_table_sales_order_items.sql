-- =============================================
-- 302: Crear tabla sales.order_items (items del pedido)
-- =============================================

USE [SCM_App_Track_Me];
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[sales].[order_items]') AND type = 'U')
BEGIN
    CREATE TABLE [sales].[order_items] (
        [id]            BIGINT IDENTITY(1,1) PRIMARY KEY,
        [order_id]      BIGINT NOT NULL,
        [product_id]    BIGINT NOT NULL,
        [product_name]  NVARCHAR(200) NOT NULL,
        [quantity]       DECIMAL(18,2) NOT NULL DEFAULT 1,
        [unit_price]    DECIMAL(18,2) NOT NULL,
        [subtotal]      AS ([quantity] * [unit_price]) PERSISTED,
        [created_at]    DATETIME2 NOT NULL DEFAULT GETDATE(),

        CONSTRAINT [FK_sales_order_items_order] FOREIGN KEY ([order_id]) REFERENCES [sales].[orders]([id]),
        CONSTRAINT [FK_sales_order_items_product] FOREIGN KEY ([product_id]) REFERENCES [inventory].[products]([id])
    );

    CREATE INDEX [IX_sales_order_items_order] ON [sales].[order_items]([order_id]);

    PRINT '✓ Tabla [sales].[order_items] creada';
END
ELSE
    PRINT '→ Tabla [sales].[order_items] ya existe';
GO
