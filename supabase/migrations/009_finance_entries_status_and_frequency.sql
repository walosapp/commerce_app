-- =============================================
-- Script: 009_finance_entries_status_and_frequency.sql
-- Descripcion: Estados por ocurrencia mensual (pending/posted/skipped) y soporte real para frequency/nature
-- Target: Supabase (PostgreSQL)
-- =============================================

-- -------------------------------------------
-- 1. Enhance recurring templates
-- -------------------------------------------
ALTER TABLE finance.recurring_templates
    ADD COLUMN IF NOT EXISTS nature VARCHAR(20) NOT NULL DEFAULT 'fixed';

ALTER TABLE finance.recurring_templates
    ADD COLUMN IF NOT EXISTS frequency VARCHAR(20) NOT NULL DEFAULT 'monthly';

-- For biweekly schedules (two configurable days of month)
ALTER TABLE finance.recurring_templates
    ADD COLUMN IF NOT EXISTS biweekly_day_1 INT;

ALTER TABLE finance.recurring_templates
    ADD COLUMN IF NOT EXISTS biweekly_day_2 INT;

-- -------------------------------------------
-- 2. Enhance entries for monthly workflow
-- -------------------------------------------
ALTER TABLE finance.entries
    ADD COLUMN IF NOT EXISTS status VARCHAR(20) NOT NULL DEFAULT 'posted';

ALTER TABLE finance.entries
    ADD COLUMN IF NOT EXISTS occurrence_in_month INT NOT NULL DEFAULT 1;

-- Optional: mark if user manually created it (not from templates)
ALTER TABLE finance.entries
    ADD COLUMN IF NOT EXISTS is_manual BOOLEAN NOT NULL DEFAULT TRUE;

-- Backfill: entries generated from templates should not be manual
UPDATE finance.entries
SET is_manual = FALSE
WHERE recurring_template_id IS NOT NULL;

-- -------------------------------------------
-- 3. Update uniqueness rules
--    Old index ensured 1 entry per template per month.
--    New requirement: allow multiple occurrences per month (weekly/quincenal), so uniqueness includes occurrence_in_month.
-- -------------------------------------------
DROP INDEX IF EXISTS finance.ux_fin_entries_company_branch_template_month;

CREATE UNIQUE INDEX IF NOT EXISTS ux_fin_entries_company_branch_template_month_occ
    ON finance.entries (
        company_id,
        COALESCE(branch_id, 0),
        recurring_template_id,
        entry_year_month,
        occurrence_in_month
    )
    WHERE deleted_at IS NULL AND recurring_template_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_fin_entries_company_status
    ON finance.entries (company_id, status) WHERE deleted_at IS NULL;
