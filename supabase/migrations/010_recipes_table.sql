-- ============================================================
-- 010: Tabla de recetas (BOM - Bill of Materials)
-- Permite definir que insumos consume un producto preparado
-- ============================================================

CREATE TABLE IF NOT EXISTS inventory.recipes (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    product_id          BIGINT NOT NULL REFERENCES inventory.products(id),   -- producto preparado
    ingredient_id       BIGINT NOT NULL REFERENCES inventory.products(id),   -- insumo
    quantity            DECIMAL(18, 4) NOT NULL CHECK (quantity > 0),
    unit_id             BIGINT REFERENCES inventory.units(id),
    notes               VARCHAR(300),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    UNIQUE (product_id, ingredient_id)
);

CREATE INDEX IF NOT EXISTS idx_recipes_product    ON inventory.recipes (product_id);
CREATE INDEX IF NOT EXISTS idx_recipes_ingredient ON inventory.recipes (ingredient_id);
CREATE INDEX IF NOT EXISTS idx_recipes_company    ON inventory.recipes (company_id);

-- product_type values: 'simple' | 'supply' | 'prepared' | 'service'
-- supply   = insumo (se descuenta del stock cuando se usa en receta)
-- prepared = producto con receta (al venderse descuenta sus ingredientes)
-- simple   = producto normal (se descuenta directamente)
-- service  = servicio sin stock
