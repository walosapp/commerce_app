-- =============================================
-- Script: 007_delivery_tables.sql
-- Descripcion: Tablas de pedidos y domicilios (orders, items, status_history)
-- Target: Supabase (PostgreSQL)
-- Multi-tenant: company_id en TODAS las tablas
-- =============================================

-- -------------------------------------------
-- 1. DELIVERY ORDERS
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS delivery.orders (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id           BIGINT NOT NULL REFERENCES core.branches(id),

    -- Origen del pedido
    source              VARCHAR(30) NOT NULL DEFAULT 'manual',
    external_order_id   VARCHAR(200),
    order_number        VARCHAR(50) NOT NULL,

    -- Estado
    status              VARCHAR(30) NOT NULL DEFAULT 'new',

    -- Cliente
    customer_name       VARCHAR(200),
    customer_phone      VARCHAR(30),
    customer_address    VARCHAR(500),
    notes               TEXT,

    -- Montos
    subtotal            DECIMAL(18,2) NOT NULL DEFAULT 0,
    delivery_fee        DECIMAL(18,2) NOT NULL DEFAULT 0,
    discount_amount     DECIMAL(18,2) NOT NULL DEFAULT 0,
    total               DECIMAL(18,2) NOT NULL DEFAULT 0,

    -- Timestamps de estados
    accepted_at         TIMESTAMPTZ,
    prepared_at         TIMESTAMPTZ,
    dispatched_at       TIMESTAMPTZ,
    delivered_at        TIMESTAMPTZ,

    -- Razones de rechazo/devolucion
    rejected_reason     TEXT,
    returned_reason     TEXT,

    -- Auditoria
    created_by          BIGINT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at          TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_del_orders_company_branch ON delivery.orders (company_id, branch_id);
CREATE INDEX IF NOT EXISTS idx_del_orders_status ON delivery.orders (company_id, branch_id, status) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_del_orders_source ON delivery.orders (company_id, source);
CREATE INDEX IF NOT EXISTS idx_del_orders_created ON delivery.orders (company_id, created_at DESC);

-- -------------------------------------------
-- 2. DELIVERY ORDER ITEMS
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS delivery.order_items (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    order_id        BIGINT NOT NULL REFERENCES delivery.orders(id),
    product_id      BIGINT NOT NULL REFERENCES inventory.products(id),

    product_name    VARCHAR(200) NOT NULL,
    quantity        DECIMAL(18,2) NOT NULL DEFAULT 1,
    unit_price      DECIMAL(18,2) NOT NULL,
    subtotal        DECIMAL(18,2) GENERATED ALWAYS AS (quantity * unit_price) STORED,
    notes           VARCHAR(500),

    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_del_order_items_order ON delivery.order_items (order_id);
CREATE INDEX IF NOT EXISTS idx_del_order_items_company ON delivery.order_items (company_id);

-- -------------------------------------------
-- 3. STATUS HISTORY (historial de cambios de estado)
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS delivery.status_history (
    id              BIGSERIAL PRIMARY KEY,
    order_id        BIGINT NOT NULL REFERENCES delivery.orders(id),

    from_status     VARCHAR(30),
    to_status       VARCHAR(30) NOT NULL,
    comment         TEXT,
    changed_by      BIGINT,

    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_del_status_history_order ON delivery.status_history (order_id);
CREATE INDEX IF NOT EXISTS idx_del_status_history_created ON delivery.status_history (created_at DESC);
