-- ============================================================
-- 020: Trazabilidad historica por item para refunds preparados
-- ============================================================

ALTER TABLE inventory.movements
    ADD COLUMN IF NOT EXISTS source_order_item_id BIGINT;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_inventory_movements_source_order_item'
          AND conrelid = 'inventory.movements'::regclass
    ) THEN
        ALTER TABLE inventory.movements
            ADD CONSTRAINT fk_inventory_movements_source_order_item
            FOREIGN KEY (source_order_item_id)
            REFERENCES sales.order_items(id)
            ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_inv_movements_source_order_item
    ON inventory.movements (company_id, branch_id, source_order_item_id)
    WHERE source_order_item_id IS NOT NULL;
