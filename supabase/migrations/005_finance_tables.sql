-- =============================================
-- Script: 005_finance_tables.sql
-- Descripcion: Tablas de finanzas (categories, entries)
-- Target: Supabase (PostgreSQL)
-- Multi-tenant: company_id en TODAS las tablas
-- =============================================

-- -------------------------------------------
-- 1. FINANCE CATEGORIES
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS finance.categories (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id       BIGINT REFERENCES core.branches(id),

    name            VARCHAR(150) NOT NULL,
    type            VARCHAR(20) NOT NULL,
    color_hex       VARCHAR(20),
    default_amount  DECIMAL(18,2) NOT NULL DEFAULT 0,
    day_of_month    INT NOT NULL DEFAULT 1,
    nature          VARCHAR(20) NOT NULL DEFAULT 'fixed',
    frequency       VARCHAR(20) NOT NULL DEFAULT 'monthly',
    biweekly_day_1  INT,
    biweekly_day_2  INT,
    auto_include_in_month BOOLEAN NOT NULL DEFAULT TRUE,

    is_system       BOOLEAN NOT NULL DEFAULT FALSE,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,

    created_by      BIGINT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ,
    deleted_at      TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_fin_categories_company_type
    ON finance.categories (company_id, type) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_fin_categories_company_branch
    ON finance.categories (company_id, branch_id) WHERE deleted_at IS NULL;

-- -------------------------------------------
-- 2. FINANCE ENTRIES
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS finance.entries (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id       BIGINT REFERENCES core.branches(id),
    category_id     BIGINT NOT NULL REFERENCES finance.categories(id),
    financial_item_id BIGINT REFERENCES finance.categories(id),

    type            VARCHAR(20) NOT NULL,
    description     VARCHAR(250) NOT NULL,
    amount          DECIMAL(18,2) NOT NULL,
    entry_date      TIMESTAMPTZ NOT NULL,

    nature          VARCHAR(20),
    frequency       VARCHAR(20),
    notes           VARCHAR(1000),

    created_by      BIGINT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ,
    deleted_at      TIMESTAMPTZ,

    CONSTRAINT fk_finance_entries_category FOREIGN KEY (category_id) REFERENCES finance.categories(id)
);

CREATE INDEX IF NOT EXISTS idx_fin_entries_company_date
    ON finance.entries (company_id, entry_date DESC) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_fin_entries_company_category
    ON finance.entries (company_id, category_id) WHERE deleted_at IS NULL;
