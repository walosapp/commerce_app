-- =============================================
-- Script: 001_create_schemas.sql
-- Descripcion: Crea esquemas para organizar tablas por modulo
-- Target: Supabase (PostgreSQL)
-- =============================================

CREATE SCHEMA IF NOT EXISTS core;
CREATE SCHEMA IF NOT EXISTS inventory;
CREATE SCHEMA IF NOT EXISTS sales;
CREATE SCHEMA IF NOT EXISTS finance;
CREATE SCHEMA IF NOT EXISTS suppliers;
CREATE SCHEMA IF NOT EXISTS delivery;
CREATE SCHEMA IF NOT EXISTS audit;
