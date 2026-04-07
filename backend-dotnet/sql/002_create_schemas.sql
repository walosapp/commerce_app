-- =============================================
-- Script: 002_create_schemas.sql
-- Descripción: Crea los esquemas para organizar las tablas por módulo
-- ¿Por qué esquemas? Organizan lógicamente las tablas, mejoran seguridad y mantenibilidad
-- =============================================

USE WalosDB;
GO

-- Schema para tablas core (empresas, usuarios, roles)
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'core')
BEGIN
    EXEC('CREATE SCHEMA core');
    PRINT '✓ Schema [core] creado';
END
GO

-- Schema para módulo de inventario
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'inventory')
BEGIN
    EXEC('CREATE SCHEMA inventory');
    PRINT '✓ Schema [inventory] creado';
END
GO

-- Schema para módulo de ventas
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'sales')
BEGIN
    EXEC('CREATE SCHEMA sales');
    PRINT '✓ Schema [sales] creado';
END
GO

-- Schema para módulo de proveedores
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'suppliers')
BEGIN
    EXEC('CREATE SCHEMA suppliers');
    PRINT '✓ Schema [suppliers] creado';
END
GO

-- Schema para catálogos generales
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'catalogs')
BEGIN
    EXEC('CREATE SCHEMA catalogs');
    PRINT '✓ Schema [catalogs] creado';
END
GO

-- Schema para auditoría y logs
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'audit')
BEGIN
    EXEC('CREATE SCHEMA audit');
    PRINT '✓ Schema [audit] creado';
END
GO

PRINT '✓ Script 002 ejecutado correctamente';
