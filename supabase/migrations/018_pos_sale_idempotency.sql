-- ============================================================
-- 018: Idempotencia persistente para ventas POS-Deli
-- ============================================================

ALTER TABLE sales.orders
    ADD COLUMN IF NOT EXISTS idempotency_key VARCHAR(100),
    ADD COLUMN IF NOT EXISTS request_fingerprint CHAR(64),
    ADD COLUMN IF NOT EXISTS cash_received DECIMAL(18,2);

ALTER TABLE sales.orders
    DROP CONSTRAINT IF EXISTS chk_orders_idempotency_key;
ALTER TABLE sales.orders
    ADD CONSTRAINT chk_orders_idempotency_key
    CHECK (idempotency_key IS NULL OR LENGTH(BTRIM(idempotency_key)) BETWEEN 1 AND 100);

ALTER TABLE sales.orders
    DROP CONSTRAINT IF EXISTS chk_orders_request_fingerprint;
ALTER TABLE sales.orders
    ADD CONSTRAINT chk_orders_request_fingerprint
    CHECK (request_fingerprint IS NULL OR request_fingerprint ~ '^[0-9a-f]{64}$');

ALTER TABLE sales.orders
    DROP CONSTRAINT IF EXISTS chk_orders_cash_received_nonnegative;
ALTER TABLE sales.orders
    ADD CONSTRAINT chk_orders_cash_received_nonnegative
    CHECK (cash_received IS NULL OR cash_received >= 0);

CREATE UNIQUE INDEX IF NOT EXISTS ux_orders_company_idempotency
    ON sales.orders (company_id, idempotency_key)
    WHERE idempotency_key IS NOT NULL;
