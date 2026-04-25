-- =============================================
-- Script: 016_cash_registers.sql
-- Descripcion: Control de Caja (Cash Register / Shift Management)
-- Target: Supabase (PostgreSQL)
-- Multi-tenant: company_id en TODAS las tablas
-- =============================================

-- -------------------------------------------
-- 1. CASH_REGISTERS (Turnos de caja)
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS sales.cash_registers (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id       BIGINT NOT NULL REFERENCES core.branches(id),
    
    opened_by       BIGINT NOT NULL REFERENCES core.users(id),
    closed_by       BIGINT REFERENCES core.users(id),

    status          VARCHAR(20) NOT NULL DEFAULT 'open',
    -- open | closed

    opening_amount  DECIMAL(18,2) NOT NULL DEFAULT 0,
    closing_amount  DECIMAL(18,2),

    expected_cash   DECIMAL(18,2),
    difference      DECIMAL(18,2),

    total_sales     DECIMAL(18,2) NOT NULL DEFAULT 0,
    total_cash_sales    DECIMAL(18,2) NOT NULL DEFAULT 0,
    total_card_sales    DECIMAL(18,2) NOT NULL DEFAULT 0,
    total_transfer_sales DECIMAL(18,2) NOT NULL DEFAULT 0,
    total_other_sales   DECIMAL(18,2) NOT NULL DEFAULT 0,
    total_discounts     DECIMAL(18,2) NOT NULL DEFAULT 0,
    total_credits       DECIMAL(18,2) NOT NULL DEFAULT 0,
    total_tips          DECIMAL(18,2) NOT NULL DEFAULT 0,

    cash_in         DECIMAL(18,2) NOT NULL DEFAULT 0,
    cash_out        DECIMAL(18,2) NOT NULL DEFAULT 0,
    order_count     INT NOT NULL DEFAULT 0,

    notes           TEXT,

    opened_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    closed_at       TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_cash_registers_company ON sales.cash_registers (company_id, branch_id, status);
CREATE INDEX IF NOT EXISTS idx_cash_registers_user ON sales.cash_registers (opened_by, status);
CREATE INDEX IF NOT EXISTS idx_cash_registers_dates ON sales.cash_registers (company_id, opened_at DESC);

-- -------------------------------------------
-- 2. CASH_MOVEMENTS (Entradas/Salidas manuales de caja)
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS sales.cash_movements (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    cash_register_id BIGINT NOT NULL REFERENCES sales.cash_registers(id),

    type            VARCHAR(10) NOT NULL,
    -- in | out

    amount          DECIMAL(18,2) NOT NULL,
    reason          VARCHAR(300) NOT NULL,
    notes           TEXT,

    created_by      BIGINT NOT NULL REFERENCES core.users(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_cash_movements_register ON sales.cash_movements (cash_register_id);
CREATE INDEX IF NOT EXISTS idx_cash_movements_type ON sales.cash_movements (cash_register_id, type);

-- -------------------------------------------
-- 3. ORDER_PAYMENTS (Pagos por orden)
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS sales.order_payments (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    order_id        BIGINT NOT NULL REFERENCES sales.orders(id),

    method          VARCHAR(20) NOT NULL,
    -- cash | card | transfer | nequi | other

    amount          DECIMAL(18,2) NOT NULL,
    reference       VARCHAR(200),

    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_order_payments_order ON sales.order_payments (order_id);
CREATE INDEX IF NOT EXISTS idx_order_payments_method ON sales.order_payments (company_id, method, created_at);

-- -------------------------------------------
-- 4. Columnas nuevas en sales.orders
-- -------------------------------------------
ALTER TABLE sales.orders 
    ADD COLUMN IF NOT EXISTS cash_register_id BIGINT REFERENCES sales.cash_registers(id),
    ADD COLUMN IF NOT EXISTS payment_method VARCHAR(20) DEFAULT 'cash',
    ADD COLUMN IF NOT EXISTS tip_amount DECIMAL(18,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS tip_included BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS refund_status VARCHAR(20);
    -- NULL | partial_refund | full_refund

CREATE INDEX IF NOT EXISTS idx_orders_cash_register ON sales.orders (cash_register_id);
CREATE INDEX IF NOT EXISTS idx_orders_payment_method ON sales.orders (company_id, payment_method, created_at);
CREATE INDEX IF NOT EXISTS idx_orders_refund_status ON sales.orders (company_id, refund_status) WHERE refund_status IS NOT NULL;

-- -------------------------------------------
-- 5. Columnas nuevas en core.companies (configuración)
-- -------------------------------------------
ALTER TABLE core.companies 
    ADD COLUMN IF NOT EXISTS default_tip_percent DECIMAL(5,2) NOT NULL DEFAULT 10,
    ADD COLUMN IF NOT EXISTS tip_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS require_cash_register BOOLEAN NOT NULL DEFAULT TRUE;
    -- Si TRUE, no se puede facturar sin caja abierta

-- -------------------------------------------
-- 6. REFUNDS (Devoluciones)
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS sales.refunds (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id       BIGINT NOT NULL REFERENCES core.branches(id),
    order_id        BIGINT NOT NULL REFERENCES sales.orders(id),

    refund_type     VARCHAR(20) NOT NULL,
    -- full | partial

    refund_amount   DECIMAL(18,2) NOT NULL,
    reason          VARCHAR(500) NOT NULL,
    status          VARCHAR(20) NOT NULL DEFAULT 'completed',
    -- completed | pending_approval

    approved_by     BIGINT REFERENCES core.users(id),

    created_by      BIGINT NOT NULL REFERENCES core.users(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_refunds_order ON sales.refunds (order_id);
CREATE INDEX IF NOT EXISTS idx_refunds_company ON sales.refunds (company_id, created_at DESC);

-- -------------------------------------------
-- 7. REFUND_ITEMS (Items devueltos)
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS sales.refund_items (
    id              BIGSERIAL PRIMARY KEY,
    refund_id       BIGINT NOT NULL REFERENCES sales.refunds(id) ON DELETE CASCADE,
    order_item_id   BIGINT NOT NULL REFERENCES sales.order_items(id),
    quantity        DECIMAL(18,2) NOT NULL,
    unit_price      DECIMAL(18,2) NOT NULL,
    subtotal        DECIMAL(18,2) NOT NULL,

    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_refund_items_refund ON sales.refund_items (refund_id);
