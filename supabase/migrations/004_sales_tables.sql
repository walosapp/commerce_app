-- =============================================
-- Script: 004_sales_tables.sql
-- Descripcion: Tablas de ventas (tables, orders, order_items)
-- Target: Supabase (PostgreSQL)
-- Multi-tenant: company_id en TODAS las tablas
-- =============================================

-- -------------------------------------------
-- 1. TABLES (mesas)
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS sales.tables (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id       BIGINT NOT NULL REFERENCES core.branches(id),

    table_number    INT NOT NULL,
    name            VARCHAR(100) NOT NULL DEFAULT '',
    status          VARCHAR(20) NOT NULL DEFAULT 'open',

    -- Auditoria
    created_by      BIGINT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ,
    deleted_at      TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_sales_tables_company_branch ON sales.tables (company_id, branch_id);
CREATE INDEX IF NOT EXISTS idx_sales_tables_status ON sales.tables (company_id, branch_id, status) WHERE deleted_at IS NULL;

-- -------------------------------------------
-- 2. ORDERS (pedidos/ventas)
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS sales.orders (
    id                      BIGSERIAL PRIMARY KEY,
    company_id              BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id               BIGINT NOT NULL REFERENCES core.branches(id),
    table_id                BIGINT NOT NULL REFERENCES sales.tables(id),

    order_number            VARCHAR(50) NOT NULL,
    status                  VARCHAR(20) NOT NULL DEFAULT 'pending',

    -- Montos
    subtotal                DECIMAL(18,2) NOT NULL DEFAULT 0,
    tax                     DECIMAL(18,2) NOT NULL DEFAULT 0,
    total                   DECIMAL(18,2) NOT NULL DEFAULT 0,

    -- Descuentos
    discount_type           VARCHAR(20),
    discount_value          DECIMAL(18,2),
    discount_amount         DECIMAL(18,2) NOT NULL DEFAULT 0,
    final_total_paid        DECIMAL(18,2),

    -- Division de cuenta
    split_reference_count   INT NOT NULL DEFAULT 1,

    notes                   VARCHAR(500),

    -- Auditoria
    created_by              BIGINT,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ,
    deleted_at              TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_sales_orders_company_branch ON sales.orders (company_id, branch_id);
CREATE INDEX IF NOT EXISTS idx_sales_orders_table ON sales.orders (company_id, table_id);
CREATE INDEX IF NOT EXISTS idx_sales_orders_status ON sales.orders (company_id, branch_id, status) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_sales_orders_created ON sales.orders (company_id, created_at DESC);

-- -------------------------------------------
-- 3. ORDER_ITEMS (items del pedido)
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS sales.order_items (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    order_id        BIGINT NOT NULL REFERENCES sales.orders(id),
    product_id      BIGINT NOT NULL REFERENCES inventory.products(id),

    product_name    VARCHAR(200) NOT NULL,
    quantity        DECIMAL(18,2) NOT NULL DEFAULT 1,
    unit_price      DECIMAL(18,2) NOT NULL,
    subtotal        DECIMAL(18,2) GENERATED ALWAYS AS (quantity * unit_price) STORED,

    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_sales_order_items_order ON sales.order_items (order_id);
CREATE INDEX IF NOT EXISTS idx_sales_order_items_company ON sales.order_items (company_id);
CREATE INDEX IF NOT EXISTS idx_sales_order_items_product ON sales.order_items (company_id, product_id);
