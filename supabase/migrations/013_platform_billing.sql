-- =============================================
-- Script: 013_platform_billing.sql
-- Descripcion: Schema platform - billing B2B, suscripciones, API keys IA
-- Target: Supabase (PostgreSQL)
-- =============================================

-- -------------------------------------------
-- 1. Campos AI en core.companies
-- -------------------------------------------
ALTER TABLE core.companies ADD COLUMN IF NOT EXISTS ai_key_managed       BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE core.companies ADD COLUMN IF NOT EXISTS ai_provider           VARCHAR(30) DEFAULT 'openai';
ALTER TABLE core.companies ADD COLUMN IF NOT EXISTS ai_api_key_enc        TEXT;
ALTER TABLE core.companies ADD COLUMN IF NOT EXISTS ai_tokens_used        BIGINT NOT NULL DEFAULT 0;
ALTER TABLE core.companies ADD COLUMN IF NOT EXISTS ai_tokens_reset_at    TIMESTAMPTZ DEFAULT NOW();
ALTER TABLE core.companies ADD COLUMN IF NOT EXISTS ai_estimated_cost     DECIMAL(12,4) NOT NULL DEFAULT 0;

-- -------------------------------------------
-- 2. Schema platform
-- -------------------------------------------
CREATE SCHEMA IF NOT EXISTS platform;

-- -------------------------------------------
-- 3. Catalogo maestro de servicios (solo Walos edita)
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS platform.service_catalog (
    id              BIGSERIAL PRIMARY KEY,
    code            VARCHAR(50) NOT NULL UNIQUE,
    name            VARCHAR(200) NOT NULL,
    description     TEXT,
    base_price      DECIMAL(12,2) NOT NULL DEFAULT 0,
    billing_unit    VARCHAR(20) NOT NULL DEFAULT 'month',   -- month | year | usage
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    display_order   INT DEFAULT 0,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- -------------------------------------------
-- 4. Servicios contratados por comercio
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS platform.company_subscriptions (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    service_code        VARCHAR(50) NOT NULL REFERENCES platform.service_catalog(code),
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    custom_price        DECIMAL(12,2),                      -- NULL = usa base_price del catalogo
    billing_frequency   VARCHAR(20) NOT NULL DEFAULT 'monthly',  -- monthly | annual
    next_billing_date   DATE,
    started_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    cancelled_at        TIMESTAMPTZ,
    notes               TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (company_id, service_code)
);

-- -------------------------------------------
-- 5. Facturas
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS platform.billing_invoices (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    invoice_number  VARCHAR(50) NOT NULL UNIQUE,
    period_start    DATE NOT NULL,
    period_end      DATE NOT NULL,
    subtotal        DECIMAL(12,2) NOT NULL DEFAULT 0,
    tax_rate        DECIMAL(5,2) NOT NULL DEFAULT 19,        -- IVA 19% Colombia
    tax_amount      DECIMAL(12,2) NOT NULL DEFAULT 0,
    total           DECIMAL(12,2) NOT NULL DEFAULT 0,
    status          VARCHAR(20) NOT NULL DEFAULT 'draft',    -- draft | sent | paid | overdue | cancelled
    sent_at         TIMESTAMPTZ,
    paid_at         TIMESTAMPTZ,
    due_date        DATE,
    payment_method  VARCHAR(30),                             -- card | pse | manual
    payment_ref     VARCHAR(200),                            -- referencia Wompi
    notes           TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- -------------------------------------------
-- 6. Lineas de factura
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS platform.billing_invoice_items (
    id              BIGSERIAL PRIMARY KEY,
    invoice_id      BIGINT NOT NULL REFERENCES platform.billing_invoices(id) ON DELETE CASCADE,
    service_code    VARCHAR(50),
    description     VARCHAR(500) NOT NULL,
    quantity        DECIMAL(10,2) NOT NULL DEFAULT 1,
    unit_price      DECIMAL(12,2) NOT NULL,
    subtotal        DECIMAL(12,2) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- -------------------------------------------
-- 7. Metodos de pago registrados por comercio
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS platform.payment_methods (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    type            VARCHAR(20) NOT NULL,                    -- card | pse
    provider        VARCHAR(30) DEFAULT 'wompi',
    provider_token  TEXT,                                    -- token Wompi (no numero completo)
    last4           VARCHAR(4),                              -- ultimos 4 digitos TC
    bank_name       VARCHAR(100),                            -- para PSE
    holder_name     VARCHAR(200),
    is_default      BOOLEAN NOT NULL DEFAULT FALSE,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- -------------------------------------------
-- 8. Indices
-- -------------------------------------------
CREATE INDEX IF NOT EXISTS idx_company_subscriptions_company ON platform.company_subscriptions (company_id);
CREATE INDEX IF NOT EXISTS idx_billing_invoices_company      ON platform.billing_invoices (company_id, status);
CREATE INDEX IF NOT EXISTS idx_billing_invoices_due          ON platform.billing_invoices (due_date) WHERE status IN ('sent', 'overdue');
CREATE INDEX IF NOT EXISTS idx_payment_methods_company       ON platform.payment_methods (company_id);

-- -------------------------------------------
-- 9. Seed catalogo inicial de servicios
-- -------------------------------------------
INSERT INTO platform.service_catalog (code, name, description, base_price, billing_unit, display_order) VALUES
('platform_base',   'Plataforma Base',         'Acceso a inventario, ventas, finanzas, reportes', 0, 'month', 1),
('ai_agents',       'Agentes IA',              'Asistente inteligente con IA para gestion y analisis', 0, 'month', 2),
('e_invoicing',     'Facturacion Electronica', 'Emision de facturas electronicas DIAN',            0, 'month', 3),
('delivery_module', 'Modulo Domicilios',       'Gestion de pedidos y domicilios',                  0, 'month', 4),
('multi_branch',    'Multi-Sucursal',          'Gestion de multiples sucursales',                  0, 'month', 5)
ON CONFLICT (code) DO NOTHING;
