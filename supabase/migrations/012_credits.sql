-- =============================================
-- Script: 012_credits.sql
-- Descripcion: Tablas para credito en mesas (pago parcial)
-- =============================================

CREATE TABLE IF NOT EXISTS sales.credits (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id       BIGINT REFERENCES core.branches(id),
    order_id        BIGINT REFERENCES sales.orders(id),

    customer_name   VARCHAR(200) NOT NULL,
    order_number    VARCHAR(50),

    original_total  DECIMAL(18,2) NOT NULL,
    amount_paid     DECIMAL(18,2) NOT NULL DEFAULT 0,
    credit_amount   DECIMAL(18,2) NOT NULL,

    status          VARCHAR(20) NOT NULL DEFAULT 'pending',
    -- pending | partial | paid | cancelled

    notes           TEXT,
    paid_at         TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by      BIGINT
);

CREATE INDEX IF NOT EXISTS idx_credits_company_status ON sales.credits (company_id, status);
CREATE INDEX IF NOT EXISTS idx_credits_company_name   ON sales.credits (company_id, customer_name);
CREATE INDEX IF NOT EXISTS idx_credits_order          ON sales.credits (order_id);

CREATE TABLE IF NOT EXISTS sales.credit_payments (
    id          BIGSERIAL PRIMARY KEY,
    company_id  BIGINT NOT NULL REFERENCES core.companies(id),
    credit_id   BIGINT NOT NULL REFERENCES sales.credits(id) ON DELETE CASCADE,
    amount      DECIMAL(18,2) NOT NULL,
    notes       TEXT,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by  BIGINT
);

CREATE INDEX IF NOT EXISTS idx_credit_payments_credit ON sales.credit_payments (credit_id);
