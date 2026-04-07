-- =============================================
-- Script: 006_suppliers_tables.sql
-- Descripcion: Tablas de proveedores (suppliers, supplier_products)
-- Target: Supabase (PostgreSQL)
-- Multi-tenant: company_id en TODAS las tablas
-- =============================================

-- -------------------------------------------
-- 1. SUPPLIERS
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS suppliers.suppliers (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id       BIGINT REFERENCES core.branches(id),

    -- Informacion
    name            VARCHAR(200) NOT NULL,
    contact_name    VARCHAR(200),
    phone           VARCHAR(20),
    email           VARCHAR(100),
    address         VARCHAR(500),
    notes           TEXT,

    -- Estado
    is_active       BOOLEAN DEFAULT TRUE,

    -- Auditoria
    created_by      BIGINT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_suppliers_company ON suppliers.suppliers (company_id);
CREATE INDEX IF NOT EXISTS idx_suppliers_company_branch ON suppliers.suppliers (company_id, branch_id);
CREATE INDEX IF NOT EXISTS idx_suppliers_name ON suppliers.suppliers (company_id, name) WHERE deleted_at IS NULL;

-- -------------------------------------------
-- 2. SUPPLIER_PRODUCTS (relacion proveedor-producto)
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS suppliers.supplier_products (
    id              BIGSERIAL PRIMARY KEY,
    supplier_id     BIGINT NOT NULL REFERENCES suppliers.suppliers(id),
    product_id      BIGINT NOT NULL REFERENCES inventory.products(id),

    supplier_sku    VARCHAR(100),
    unit_cost       DECIMAL(18,2),
    lead_time_days  INT,
    notes           VARCHAR(500),

    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Constraints
    UNIQUE (supplier_id, product_id)
);

CREATE INDEX IF NOT EXISTS idx_supplier_products_supplier ON suppliers.supplier_products (supplier_id);
CREATE INDEX IF NOT EXISTS idx_supplier_products_product ON suppliers.supplier_products (product_id);
