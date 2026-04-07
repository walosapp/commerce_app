-- =============================================
-- Script: 104_alter_table_companies_add_settings_fields.sql
-- Descripcion: Agrega campos de configuracion visual para branding y temas
-- =============================================

USE WalosDB;
GO

IF COL_LENGTH('[core].[companies]', 'display_name') IS NULL
BEGIN
    ALTER TABLE [core].[companies]
    ADD display_name NVARCHAR(200) NULL;

    PRINT '✓ Columna display_name agregada a [core].[companies]';
END
ELSE
BEGIN
    PRINT 'La columna display_name ya existe';
END
GO

IF COL_LENGTH('[core].[companies]', 'theme_preference') IS NULL
BEGIN
    ALTER TABLE [core].[companies]
    ADD theme_preference NVARCHAR(30) NOT NULL
        CONSTRAINT DF_companies_theme_preference DEFAULT 'light';

    PRINT '✓ Columna theme_preference agregada a [core].[companies]';
END
ELSE
BEGIN
    PRINT 'La columna theme_preference ya existe';
END
GO

UPDATE [core].[companies]
SET
    display_name = ISNULL(display_name, name),
    theme_preference = ISNULL(theme_preference, 'light')
WHERE deleted_at IS NULL;
GO

PRINT '✓ Script 104 ejecutado correctamente';

IF COL_LENGTH('[core].[companies]', 'manual_discount_enabled') IS NULL
BEGIN
    ALTER TABLE [core].[companies]
    ADD manual_discount_enabled BIT NOT NULL
        CONSTRAINT DF_companies_manual_discount_enabled DEFAULT 1;
END
GO

IF COL_LENGTH('[core].[companies]', 'max_discount_percent') IS NULL
BEGIN
    ALTER TABLE [core].[companies]
    ADD max_discount_percent DECIMAL(5,2) NOT NULL
        CONSTRAINT DF_companies_max_discount_percent DEFAULT 15;
END
GO

IF COL_LENGTH('[core].[companies]', 'max_discount_amount') IS NULL
BEGIN
    ALTER TABLE [core].[companies]
    ADD max_discount_amount DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_companies_max_discount_amount DEFAULT 50000;
END
GO

IF COL_LENGTH('[core].[companies]', 'discount_requires_override') IS NULL
BEGIN
    ALTER TABLE [core].[companies]
    ADD discount_requires_override BIT NOT NULL
        CONSTRAINT DF_companies_discount_requires_override DEFAULT 0;
END
GO

IF COL_LENGTH('[core].[companies]', 'discount_override_threshold_percent') IS NULL
BEGIN
    ALTER TABLE [core].[companies]
    ADD discount_override_threshold_percent DECIMAL(5,2) NOT NULL
        CONSTRAINT DF_companies_discount_override_threshold_percent DEFAULT 10;
END
GO

UPDATE [core].[companies]
SET
    manual_discount_enabled = ISNULL(manual_discount_enabled, 1),
    max_discount_percent = ISNULL(max_discount_percent, 15),
    max_discount_amount = ISNULL(max_discount_amount, 50000),
    discount_requires_override = ISNULL(discount_requires_override, 0),
    discount_override_threshold_percent = ISNULL(discount_override_threshold_percent, 10)
WHERE deleted_at IS NULL;
GO
