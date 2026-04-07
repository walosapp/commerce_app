-- =============================================
-- Script: 001_create_database.sql
-- Descripción: Crea la base de datos principal del sistema Walos
-- Autor: Sistema Walos
-- Fecha: 2026-03-02
-- =============================================

-- Verificar si la base de datos existe
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'WalosDB')
BEGIN
    CREATE DATABASE WalosDB
    COLLATE Modern_Spanish_CI_AS;
    PRINT 'Base de datos WalosDB creada exitosamente';
END
ELSE
BEGIN
    PRINT 'La base de datos WalosDB ya existe';
END
GO

-- Usar la base de datos
USE WalosDB;
GO

-- Configurar opciones de la base de datos
ALTER DATABASE WalosDB SET RECOVERY SIMPLE;
ALTER DATABASE WalosDB SET AUTO_UPDATE_STATISTICS ON;
ALTER DATABASE WalosDB SET AUTO_CREATE_STATISTICS ON;
GO

PRINT '✓ Script 001 ejecutado correctamente';
