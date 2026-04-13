-- =============================================
-- Script: 008_finance_recurring_templates.sql
-- Descripcion: Ajuste del modelo financiero para usar finance.categories como catalogo principal de items
-- Target: Supabase (PostgreSQL)
-- =============================================

ALTER TABLE finance.categories
    ADD COLUMN IF NOT EXISTS branch_id BIGINT REFERENCES core.branches(id),
    ADD COLUMN IF NOT EXISTS default_amount DECIMAL(18,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS day_of_month INT NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS nature VARCHAR(20) NOT NULL DEFAULT 'fixed',
    ADD COLUMN IF NOT EXISTS frequency VARCHAR(20) NOT NULL DEFAULT 'monthly',
    ADD COLUMN IF NOT EXISTS biweekly_day_1 INT,
    ADD COLUMN IF NOT EXISTS biweekly_day_2 INT,
    ADD COLUMN IF NOT EXISTS auto_include_in_month BOOLEAN NOT NULL DEFAULT TRUE;

ALTER TABLE finance.entries
    ADD COLUMN IF NOT EXISTS financial_item_id BIGINT REFERENCES finance.categories(id);

CREATE INDEX IF NOT EXISTS idx_fin_categories_company_branch
    ON finance.categories (company_id, branch_id) WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_fin_entries_company_item
    ON finance.entries (company_id, financial_item_id) WHERE deleted_at IS NULL;
