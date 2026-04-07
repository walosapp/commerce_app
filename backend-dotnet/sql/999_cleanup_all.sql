-- =============================================
-- Script: 999_cleanup_all.sql
-- Descripcion: Elimina TODOS los objetos creados por los scripts de Walos
--              en la base de datos SCM_App_Track_Me.
--              Usar SOLO para limpiar despues de pruebas.
-- ADVERTENCIA: Este script es DESTRUCTIVO e IRREVERSIBLE.
-- =============================================

USE SCM_App_Track_Me;
GO

PRINT '========================================';
PRINT '  LIMPIEZA COMPLETA - SCM_App_Track_Me';
PRINT '========================================';
PRINT '';
PRINT 'ADVERTENCIA: Se eliminaran TODAS las tablas y esquemas de Walos.';
PRINT '';

-- =============================================
-- PASO 1: Eliminar tablas del modulo INVENTORY
-- (orden inverso por dependencias / foreign keys)
-- =============================================
PRINT '--- Eliminando tablas de [inventory] ---';

IF OBJECT_ID('[inventory].[alerts]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [inventory].[alerts];
    PRINT '  x Tabla [inventory].[alerts] eliminada';
END
GO

IF OBJECT_ID('[inventory].[ai_interactions]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [inventory].[ai_interactions];
    PRINT '  x Tabla [inventory].[ai_interactions] eliminada';
END
GO

IF OBJECT_ID('[inventory].[movements]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [inventory].[movements];
    PRINT '  x Tabla [inventory].[movements] eliminada';
END
GO

IF OBJECT_ID('[inventory].[stock]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [inventory].[stock];
    PRINT '  x Tabla [inventory].[stock] eliminada';
END
GO

IF OBJECT_ID('[inventory].[products]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [inventory].[products];
    PRINT '  x Tabla [inventory].[products] eliminada';
END
GO

IF OBJECT_ID('[inventory].[units]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [inventory].[units];
    PRINT '  x Tabla [inventory].[units] eliminada';
END
GO

IF OBJECT_ID('[inventory].[categories]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [inventory].[categories];
    PRINT '  x Tabla [inventory].[categories] eliminada';
END
GO

-- =============================================
-- PASO 2: Eliminar tablas del modulo CORE
-- (orden inverso por dependencias / foreign keys)
-- =============================================
PRINT '--- Eliminando tablas de [core] ---';

IF OBJECT_ID('[core].[users]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [core].[users];
    PRINT '  x Tabla [core].[users] eliminada';
END
GO

IF OBJECT_ID('[core].[roles]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [core].[roles];
    PRINT '  x Tabla [core].[roles] eliminada';
END
GO

IF OBJECT_ID('[core].[branches]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [core].[branches];
    PRINT '  x Tabla [core].[branches] eliminada';
END
GO

IF OBJECT_ID('[core].[companies]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [core].[companies];
    PRINT '  x Tabla [core].[companies] eliminada';
END
GO

-- =============================================
-- PASO 3: Eliminar esquemas
-- (solo si estan vacios, es decir, sin tablas)
-- =============================================
PRINT '--- Eliminando esquemas ---';

IF EXISTS (SELECT * FROM sys.schemas WHERE name = 'inventory')
BEGIN
    DROP SCHEMA [inventory];
    PRINT '  x Schema [inventory] eliminado';
END
GO

IF EXISTS (SELECT * FROM sys.schemas WHERE name = 'core')
BEGIN
    DROP SCHEMA [core];
    PRINT '  x Schema [core] eliminado';
END
GO

IF EXISTS (SELECT * FROM sys.schemas WHERE name = 'sales')
BEGIN
    DROP SCHEMA [sales];
    PRINT '  x Schema [sales] eliminado';
END
GO

IF EXISTS (SELECT * FROM sys.schemas WHERE name = 'suppliers')
BEGIN
    DROP SCHEMA [suppliers];
    PRINT '  x Schema [suppliers] eliminado';
END
GO

IF EXISTS (SELECT * FROM sys.schemas WHERE name = 'catalogs')
BEGIN
    DROP SCHEMA [catalogs];
    PRINT '  x Schema [catalogs] eliminado';
END
GO

IF EXISTS (SELECT * FROM sys.schemas WHERE name = 'audit')
BEGIN
    DROP SCHEMA [audit];
    PRINT '  x Schema [audit] eliminado';
END
GO

PRINT '';
PRINT '========================================';
PRINT '  LIMPIEZA COMPLETADA';
PRINT '  Base de datos: SCM_App_Track_Me';
PRINT '  Todas las tablas y esquemas de Walos';
PRINT '  han sido eliminados.';
PRINT '========================================';
