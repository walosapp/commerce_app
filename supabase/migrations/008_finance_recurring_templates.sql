-- =============================================
-- Script: 008_finance_recurring_templates.sql
-- Descripcion: Plantillas recurrentes y soporte para inicio de mes
-- Target: Supabase (PostgreSQL)
-- Multi-tenant: company_id en TODAS las tablas
-- =============================================

-- -------------------------------------------
-- 1. FINANCE RECURRING TEMPLATES
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS finance.recurring_templates (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id       BIGINT REFERENCES core.branches(id),
    category_id     BIGINT NOT NULL REFERENCES finance.categories(id),

    type            VARCHAR(20) NOT NULL,
    description     VARCHAR(250) NOT NULL,
    default_amount  DECIMAL(18,2) NOT NULL,

    day_of_month    INT NOT NULL DEFAULT 1,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,

    created_by      BIGINT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ,
    deleted_at      TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_fin_rec_templates_company_branch
    ON finance.recurring_templates (company_id, branch_id) WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_fin_rec_templates_company_category
    ON finance.recurring_templates (company_id, category_id) WHERE deleted_at IS NULL;

-- -------------------------------------------
-- 2. LINK GENERATED ENTRIES TO TEMPLATE
-- -------------------------------------------
ALTER TABLE finance.entries
    ADD COLUMN IF NOT EXISTS recurring_template_id BIGINT;

-- Month key used to enforce uniqueness per template per month.
-- We compute it in UTC to avoid timezone-dependent casts.
ALTER TABLE finance.entries
    ADD COLUMN IF NOT EXISTS entry_year_month INT
    GENERATED ALWAYS AS (
        (EXTRACT(YEAR FROM (entry_date AT TIME ZONE 'UTC'))::int * 100)
        + EXTRACT(MONTH FROM (entry_date AT TIME ZONE 'UTC'))::int
    ) STORED;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_fin_entries_rec_template'
    ) THEN
        ALTER TABLE finance.entries
            ADD CONSTRAINT fk_fin_entries_rec_template
                FOREIGN KEY (recurring_template_id) REFERENCES finance.recurring_templates(id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_fin_entries_company_rec_template
    ON finance.entries (company_id, recurring_template_id) WHERE deleted_at IS NULL;

-- Ensure we don't generate duplicates for the same template in the same month
CREATE UNIQUE INDEX IF NOT EXISTS ux_fin_entries_company_branch_template_month
    ON finance.entries (
        company_id,
        COALESCE(branch_id, 0),
        recurring_template_id,
        entry_year_month
    )
    WHERE deleted_at IS NULL AND recurring_template_id IS NOT NULL;
