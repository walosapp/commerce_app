-- =============================================
-- Script: 009_finance_entries_status_and_frequency.sql
-- Descripcion: Estados y unicidad por item financiero mensual
-- Target: Supabase (PostgreSQL)
-- =============================================

ALTER TABLE finance.entries
    ADD COLUMN IF NOT EXISTS status VARCHAR(20) NOT NULL DEFAULT 'posted',
    ADD COLUMN IF NOT EXISTS occurrence_in_month INT NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS is_manual BOOLEAN NOT NULL DEFAULT TRUE;

ALTER TABLE finance.entries
    ADD COLUMN IF NOT EXISTS entry_year_month INT
    GENERATED ALWAYS AS (
        (EXTRACT(YEAR FROM (entry_date AT TIME ZONE 'UTC'))::int * 100)
        + EXTRACT(MONTH FROM (entry_date AT TIME ZONE 'UTC'))::int
    ) STORED;

DROP INDEX IF EXISTS finance.ux_fin_entries_company_branch_template_month;
DROP INDEX IF EXISTS finance.ux_fin_entries_company_branch_template_month_occ;

CREATE UNIQUE INDEX IF NOT EXISTS ux_fin_entries_company_branch_item_month_occ
    ON finance.entries (
        company_id,
        COALESCE(branch_id, 0),
        financial_item_id,
        entry_year_month,
        occurrence_in_month
    )
    WHERE deleted_at IS NULL AND financial_item_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_fin_entries_company_status
    ON finance.entries (company_id, status) WHERE deleted_at IS NULL;
