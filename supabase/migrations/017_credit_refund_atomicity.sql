-- ============================================================
-- 017: Atomicidad e idempotencia de abonos y devoluciones
-- ============================================================

ALTER TABLE sales.credit_payments
    ADD COLUMN IF NOT EXISTS payment_method VARCHAR(20),
    ADD COLUMN IF NOT EXISTS cash_register_id BIGINT REFERENCES sales.cash_registers(id);

ALTER TABLE sales.credit_payments
    DROP CONSTRAINT IF EXISTS chk_credit_payments_method;
ALTER TABLE sales.credit_payments
    ADD CONSTRAINT chk_credit_payments_method
    CHECK (payment_method IS NULL OR payment_method IN ('cash', 'card', 'transfer', 'nequi', 'other'));

ALTER TABLE sales.credit_payments
    DROP CONSTRAINT IF EXISTS chk_credit_payments_amount_positive;
ALTER TABLE sales.credit_payments
    ADD CONSTRAINT chk_credit_payments_amount_positive CHECK (amount > 0) NOT VALID;

CREATE INDEX IF NOT EXISTS idx_credit_payments_cash_register
    ON sales.credit_payments (cash_register_id)
    WHERE cash_register_id IS NOT NULL;

ALTER TABLE sales.refunds
    ADD COLUMN IF NOT EXISTS idempotency_key VARCHAR(100),
    ADD COLUMN IF NOT EXISTS request_fingerprint CHAR(64),
    ADD COLUMN IF NOT EXISTS cash_register_id BIGINT REFERENCES sales.cash_registers(id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_refunds_company_idempotency
    ON sales.refunds (company_id, idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_refunds_cash_register
    ON sales.refunds (cash_register_id)
    WHERE cash_register_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_refund_items_refund_order_item
    ON sales.refund_items (refund_id, order_item_id);

ALTER TABLE sales.refund_items
    DROP CONSTRAINT IF EXISTS chk_refund_items_quantity_positive;
ALTER TABLE sales.refund_items
    ADD CONSTRAINT chk_refund_items_quantity_positive CHECK (quantity > 0) NOT VALID;

ALTER TABLE sales.refund_items
    DROP CONSTRAINT IF EXISTS chk_refund_items_subtotal_nonnegative;
ALTER TABLE sales.refund_items
    ADD CONSTRAINT chk_refund_items_subtotal_nonnegative CHECK (subtotal >= 0) NOT VALID;
