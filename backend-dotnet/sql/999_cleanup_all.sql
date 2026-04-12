-- =============================================
-- Script: 999_cleanup_sqlserver.sql
-- Descripcion: Elimina TODOS los objetos creados por Walos en SQL Server.
--              Cubre ambas bases: WalosDB y SCM_App_Track_Me.
--              Usar para limpiar SQL Server ya que el proyecto
--              migro a PostgreSQL (Supabase).
-- ADVERTENCIA: Este script es DESTRUCTIVO e IRREVERSIBLE.
-- Fecha: 2026-04-08
-- =============================================

-- =============================================
-- PARTE A: Limpiar base WalosDB (si existe)
-- =============================================
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'WalosDB')
BEGIN
    PRINT '========================================';
    PRINT '  LIMPIANDO WalosDB';
    PRINT '========================================';
END
GO

USE WalosDB;
GO

-- ------- FINANCE -------
IF OBJECT_ID('[finance].[entries]', 'U') IS NOT NULL DROP TABLE [finance].[entries];
IF OBJECT_ID('[finance].[categories]', 'U') IS NOT NULL DROP TABLE [finance].[categories];
PRINT '  x Tablas [finance] eliminadas';
GO

-- ------- SALES (orden inverso por FK) -------
IF OBJECT_ID('[sales].[order_items]', 'U') IS NOT NULL DROP TABLE [sales].[order_items];
IF OBJECT_ID('[sales].[orders]', 'U') IS NOT NULL DROP TABLE [sales].[orders];
IF OBJECT_ID('[sales].[tables]', 'U') IS NOT NULL DROP TABLE [sales].[tables];
PRINT '  x Tablas [sales] eliminadas';
GO

-- ------- INVENTORY (orden inverso por FK) -------
IF OBJECT_ID('[inventory].[alerts]', 'U') IS NOT NULL DROP TABLE [inventory].[alerts];
IF OBJECT_ID('[inventory].[ai_interactions]', 'U') IS NOT NULL DROP TABLE [inventory].[ai_interactions];
IF OBJECT_ID('[inventory].[movements]', 'U') IS NOT NULL DROP TABLE [inventory].[movements];
IF OBJECT_ID('[inventory].[stock]', 'U') IS NOT NULL DROP TABLE [inventory].[stock];
IF OBJECT_ID('[inventory].[products]', 'U') IS NOT NULL DROP TABLE [inventory].[products];
IF OBJECT_ID('[inventory].[units]', 'U') IS NOT NULL DROP TABLE [inventory].[units];
IF OBJECT_ID('[inventory].[categories]', 'U') IS NOT NULL DROP TABLE [inventory].[categories];
PRINT '  x Tablas [inventory] eliminadas';
GO

-- ------- CORE (orden inverso por FK) -------
IF OBJECT_ID('[core].[users]', 'U') IS NOT NULL DROP TABLE [core].[users];
IF OBJECT_ID('[core].[roles]', 'U') IS NOT NULL DROP TABLE [core].[roles];
IF OBJECT_ID('[core].[branches]', 'U') IS NOT NULL DROP TABLE [core].[branches];
IF OBJECT_ID('[core].[companies]', 'U') IS NOT NULL DROP TABLE [core].[companies];
PRINT '  x Tablas [core] eliminadas';
GO

-- ------- SCHEMAS -------
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'finance') EXEC('DROP SCHEMA [finance]');
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'sales') EXEC('DROP SCHEMA [sales]');
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'inventory') EXEC('DROP SCHEMA [inventory]');
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'core') EXEC('DROP SCHEMA [core]');
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'suppliers') EXEC('DROP SCHEMA [suppliers]');
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'catalogs') EXEC('DROP SCHEMA [catalogs]');
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'audit') EXEC('DROP SCHEMA [audit]');
PRINT '  x Schemas eliminados en WalosDB';
GO

USE master;
GO

-- =============================================
-- PARTE B: Limpiar base SCM_App_Track_Me (si existe)
-- =============================================
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'SCM_App_Track_Me')
BEGIN
    PRINT '';
    PRINT '========================================';
    PRINT '  LIMPIANDO SCM_App_Track_Me';
    PRINT '========================================';
END
GO

IF EXISTS (SELECT name FROM sys.databases WHERE name = 'SCM_App_Track_Me')
BEGIN
    EXEC('
        USE [SCM_App_Track_Me];

        IF OBJECT_ID(''[finance].[entries]'', ''U'') IS NOT NULL DROP TABLE [finance].[entries];
        IF OBJECT_ID(''[finance].[categories]'', ''U'') IS NOT NULL DROP TABLE [finance].[categories];

        IF OBJECT_ID(''[sales].[order_items]'', ''U'') IS NOT NULL DROP TABLE [sales].[order_items];
        IF OBJECT_ID(''[sales].[orders]'', ''U'') IS NOT NULL DROP TABLE [sales].[orders];
        IF OBJECT_ID(''[sales].[tables]'', ''U'') IS NOT NULL DROP TABLE [sales].[tables];

        IF OBJECT_ID(''[inventory].[alerts]'', ''U'') IS NOT NULL DROP TABLE [inventory].[alerts];
        IF OBJECT_ID(''[inventory].[ai_interactions]'', ''U'') IS NOT NULL DROP TABLE [inventory].[ai_interactions];
        IF OBJECT_ID(''[inventory].[movements]'', ''U'') IS NOT NULL DROP TABLE [inventory].[movements];
        IF OBJECT_ID(''[inventory].[stock]'', ''U'') IS NOT NULL DROP TABLE [inventory].[stock];
        IF OBJECT_ID(''[inventory].[products]'', ''U'') IS NOT NULL DROP TABLE [inventory].[products];
        IF OBJECT_ID(''[inventory].[units]'', ''U'') IS NOT NULL DROP TABLE [inventory].[units];
        IF OBJECT_ID(''[inventory].[categories]'', ''U'') IS NOT NULL DROP TABLE [inventory].[categories];

        IF OBJECT_ID(''[core].[users]'', ''U'') IS NOT NULL DROP TABLE [core].[users];
        IF OBJECT_ID(''[core].[roles]'', ''U'') IS NOT NULL DROP TABLE [core].[roles];
        IF OBJECT_ID(''[core].[branches]'', ''U'') IS NOT NULL DROP TABLE [core].[branches];
        IF OBJECT_ID(''[core].[companies]'', ''U'') IS NOT NULL DROP TABLE [core].[companies];

        IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = ''finance'') EXEC(''DROP SCHEMA [finance]'');
        IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = ''sales'') EXEC(''DROP SCHEMA [sales]'');
        IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = ''inventory'') EXEC(''DROP SCHEMA [inventory]'');
        IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = ''core'') EXEC(''DROP SCHEMA [core]'');
        IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = ''suppliers'') EXEC(''DROP SCHEMA [suppliers]'');
        IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = ''catalogs'') EXEC(''DROP SCHEMA [catalogs]'');
        IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = ''audit'') EXEC(''DROP SCHEMA [audit]'');
    ');
    PRINT '  x Tablas y schemas eliminados en SCM_App_Track_Me';
END
GO

-- =============================================
-- PARTE C (OPCIONAL): Eliminar las bases de datos completas
-- Descomentar las lineas si deseas eliminar las bases por completo.
-- =============================================
-- USE master;
-- GO
-- IF EXISTS (SELECT name FROM sys.databases WHERE name = 'WalosDB')
-- BEGIN
--     ALTER DATABASE WalosDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
--     DROP DATABASE WalosDB;
--     PRINT '  x Base WalosDB eliminada';
-- END
-- GO
-- IF EXISTS (SELECT name FROM sys.databases WHERE name = 'SCM_App_Track_Me')
-- BEGIN
--     ALTER DATABASE SCM_App_Track_Me SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
--     DROP DATABASE SCM_App_Track_Me;
--     PRINT '  x Base SCM_App_Track_Me eliminada';
-- END
-- GO

PRINT '';
PRINT '========================================';
PRINT '  LIMPIEZA SQL SERVER COMPLETADA';
PRINT '  Proyecto migrado a PostgreSQL/Supabase';
PRINT '  Los scripts SQL Server ya no se usan.';
PRINT '========================================';
