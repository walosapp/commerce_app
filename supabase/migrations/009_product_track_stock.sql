-- ============================================================
-- 009: Agregar track_stock a inventory.products
-- Permite distinguir productos que requieren control de stock
-- (insumos, botellas) de preparaciones/servicios que siempre
-- estan disponibles.
-- ============================================================

-- 1. Nuevo campo track_stock (default TRUE = comportamiento actual)
ALTER TABLE inventory.products
    ADD COLUMN IF NOT EXISTS track_stock BOOLEAN DEFAULT TRUE;

-- 2. Productos tipo prepared/service no trackean stock
UPDATE inventory.products
SET track_stock = FALSE
WHERE product_type IN ('prepared', 'service');

-- 3. Indice para filtrar rapido por tipo
CREATE INDEX IF NOT EXISTS idx_inv_products_type
    ON inventory.products (company_id, product_type)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_inv_products_track_stock
    ON inventory.products (company_id, track_stock)
    WHERE deleted_at IS NULL;
