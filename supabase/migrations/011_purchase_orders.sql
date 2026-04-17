-- ============================================================
-- 011: Pedidos a proveedor (purchase orders)
-- Al recibir un pedido se actualiza stock y se registra egreso
-- ============================================================

CREATE TABLE IF NOT EXISTS suppliers.purchase_orders (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id       BIGINT NOT NULL REFERENCES core.branches(id),
    supplier_id     BIGINT NOT NULL REFERENCES suppliers.suppliers(id),

    order_number    VARCHAR(50) NOT NULL,
    status          VARCHAR(20) NOT NULL DEFAULT 'pending',
    -- pending | ordered | received | cancelled

    notes           TEXT,
    expected_date   DATE,
    received_at     TIMESTAMPTZ,

    -- Totales
    subtotal        DECIMAL(18,2) NOT NULL DEFAULT 0,
    tax             DECIMAL(18,2) NOT NULL DEFAULT 0,
    total           DECIMAL(18,2) NOT NULL DEFAULT 0,

    -- Auditoria
    created_by      BIGINT REFERENCES core.users(id),
    received_by     BIGINT REFERENCES core.users(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    UNIQUE (company_id, order_number)
);

CREATE TABLE IF NOT EXISTS suppliers.purchase_order_items (
    id              BIGSERIAL PRIMARY KEY,
    order_id        BIGINT NOT NULL REFERENCES suppliers.purchase_orders(id) ON DELETE CASCADE,
    product_id      BIGINT NOT NULL REFERENCES inventory.products(id),

    product_name    VARCHAR(200) NOT NULL,
    quantity        DECIMAL(18,3) NOT NULL CHECK (quantity > 0),
    unit_cost       DECIMAL(18,2) NOT NULL DEFAULT 0,
    subtotal        DECIMAL(18,2) NOT NULL DEFAULT 0,

    -- Lo que realmente llegó (puede diferir del pedido)
    received_qty    DECIMAL(18,3),

    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_po_company    ON suppliers.purchase_orders (company_id, status);
CREATE INDEX IF NOT EXISTS idx_po_supplier   ON suppliers.purchase_orders (supplier_id);
CREATE INDEX IF NOT EXISTS idx_po_items_order ON suppliers.purchase_order_items (order_id);
