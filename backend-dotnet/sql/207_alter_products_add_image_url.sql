-- =============================================
-- 207: Agregar columna image_url a productos
-- =============================================

USE [SCM_App_Track_Me];
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('[inventory].[products]') 
    AND name = 'image_url'
)
BEGIN
    ALTER TABLE [inventory].[products]
    ADD [image_url] NVARCHAR(500) NULL;
    
    PRINT '✓ Columna image_url agregada a inventory.products';
END
ELSE
BEGIN
    PRINT '→ Columna image_url ya existe';
END
GO
