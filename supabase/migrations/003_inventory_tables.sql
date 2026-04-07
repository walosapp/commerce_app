-- =============================================
-- Script: 003_inventory_tables.sql
-- Descripcion: Tablas de inventario (categories, units, products, stock, movements, ai_interactions, alerts)
-- Target: Supabase (PostgreSQL)
-- Multi-tenant: company_id en TODAS las tablas
-- =============================================

-- -------------------------------------------
-- 1. CATEGORIES
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS inventory.categories (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),

    -- Informacion
    name                VARCHAR(100) NOT NULL,
    code                VARCHAR(50) NOT NULL,
    description         VARCHAR(500),

    -- Jerarquia
    parent_category_id  BIGINT REFERENCES inventory.categories(id),

    -- Configuracion visual
    icon                VARCHAR(50),
    color               VARCHAR(7),
    display_order       INT DEFAULT 0,

    -- Estado
    is_active           BOOLEAN DEFAULT TRUE,

    -- Auditoria
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at          TIMESTAMPTZ,
    created_by          BIGINT,
    updated_by          BIGINT,

    -- Constraints
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS idx_inv_categories_company ON inventory.categories (company_id);
CREATE INDEX IF NOT EXISTS idx_inv_categories_parent ON inventory.categories (parent_category_id);
CREATE INDEX IF NOT EXISTS idx_inv_categories_active ON inventory.categories (company_id, is_active) WHERE deleted_at IS NULL;

-- -------------------------------------------
-- 2. UNITS
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS inventory.units (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),

    -- Informacion
    name                VARCHAR(100) NOT NULL,
    abbreviation        VARCHAR(10) NOT NULL,
    unit_type           VARCHAR(50) NOT NULL,

    -- Conversion
    base_unit_id        BIGINT REFERENCES inventory.units(id),
    conversion_factor   DECIMAL(18, 6) DEFAULT 1.0,

    -- Estado
    is_active           BOOLEAN DEFAULT TRUE,

    -- Auditoria
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at          TIMESTAMPTZ,
    created_by          BIGINT,
    updated_by          BIGINT,

    -- Constraints
    UNIQUE (company_id, abbreviation)
);

CREATE INDEX IF NOT EXISTS idx_inv_units_company ON inventory.units (company_id);
CREATE INDEX IF NOT EXISTS idx_inv_units_type ON inventory.units (company_id, unit_type);

-- -------------------------------------------
-- 3. PRODUCTS
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS inventory.products (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),

    -- Informacion basica
    name                VARCHAR(200) NOT NULL,
    sku                 VARCHAR(50) NOT NULL,
    barcode             VARCHAR(100),
    description         VARCHAR(1000),

    -- Clasificacion
    category_id         BIGINT NOT NULL REFERENCES inventory.categories(id),
    unit_id             BIGINT NOT NULL REFERENCES inventory.units(id),

    -- Imagen
    image_url           VARCHAR(500),
    thumbnail_url       VARCHAR(500),

    -- Precios
    cost_price          DECIMAL(18, 2) NOT NULL DEFAULT 0,
    sale_price          DECIMAL(18, 2) NOT NULL DEFAULT 0,
    margin_percentage   DECIMAL(10, 4) GENERATED ALWAYS AS (
        CASE WHEN cost_price > 0 THEN ((sale_price - cost_price) / cost_price * 100) ELSE 0 END
    ) STORED,

    -- Control de stock
    min_stock           DECIMAL(18, 3) DEFAULT 0,
    max_stock           DECIMAL(18, 3) DEFAULT 0,
    reorder_point       DECIMAL(18, 3) DEFAULT 0,

    -- Configuracion
    is_perishable       BOOLEAN DEFAULT FALSE,
    shelf_life_days     INT,
    product_type        VARCHAR(50) DEFAULT 'simple',

    -- Estado
    is_active           BOOLEAN DEFAULT TRUE,
    is_for_sale         BOOLEAN DEFAULT TRUE,

    -- Auditoria
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at          TIMESTAMPTZ,
    created_by          BIGINT,
    updated_by          BIGINT,

    -- Constraints
    UNIQUE (company_id, sku),
    CHECK (cost_price >= 0),
    CHECK (sale_price >= 0)
);

CREATE INDEX IF NOT EXISTS idx_inv_products_company ON inventory.products (company_id);
CREATE INDEX IF NOT EXISTS idx_inv_products_category ON inventory.products (company_id, category_id);
CREATE INDEX IF NOT EXISTS idx_inv_products_sku ON inventory.products (company_id, sku);
CREATE INDEX IF NOT EXISTS idx_inv_products_barcode ON inventory.products (barcode) WHERE barcode IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_inv_products_active ON inventory.products (company_id, is_active) WHERE deleted_at IS NULL;

-- -------------------------------------------
-- 4. STOCK (por sucursal)
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS inventory.stock (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id           BIGINT NOT NULL REFERENCES core.branches(id),
    product_id          BIGINT NOT NULL REFERENCES inventory.products(id),

    -- Cantidades
    quantity            DECIMAL(18, 3) NOT NULL DEFAULT 0,
    reserved_quantity   DECIMAL(18, 3) DEFAULT 0,

    -- Ubicacion fisica
    location            VARCHAR(100),

    -- Ultimo conteo
    last_stock_count_at TIMESTAMPTZ,
    last_stock_count_by BIGINT,

    -- Auditoria
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Constraints
    UNIQUE (branch_id, product_id),
    CHECK (quantity >= 0),
    CHECK (reserved_quantity >= 0)
);

CREATE INDEX IF NOT EXISTS idx_inv_stock_company ON inventory.stock (company_id);
CREATE INDEX IF NOT EXISTS idx_inv_stock_branch ON inventory.stock (company_id, branch_id);
CREATE INDEX IF NOT EXISTS idx_inv_stock_product ON inventory.stock (company_id, product_id);
CREATE INDEX IF NOT EXISTS idx_inv_stock_quantity ON inventory.stock (company_id, branch_id, quantity);

-- -------------------------------------------
-- 5. MOVEMENTS (historial de entradas/salidas)
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS inventory.movements (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id           BIGINT NOT NULL REFERENCES core.branches(id),
    product_id          BIGINT NOT NULL REFERENCES inventory.products(id),

    -- Tipo
    movement_type       VARCHAR(50) NOT NULL,

    -- Cantidades
    quantity            DECIMAL(18, 3) NOT NULL,
    unit_cost           DECIMAL(18, 2),
    total_cost          DECIMAL(18, 2) GENERATED ALWAYS AS (ABS(quantity) * COALESCE(unit_cost, 0)) STORED,

    -- Referencia al documento origen
    reference_type      VARCHAR(50),
    reference_id        BIGINT,

    -- Info adicional
    notes               VARCHAR(1000),

    -- Transferencias entre sucursales
    from_branch_id      BIGINT REFERENCES core.branches(id),
    to_branch_id        BIGINT REFERENCES core.branches(id),

    -- Stock despues del movimiento
    stock_after         DECIMAL(18, 3),

    -- IA
    created_by_ai       BOOLEAN DEFAULT FALSE,
    ai_confidence       DECIMAL(5, 2),
    ai_metadata         JSONB,

    -- Auditoria
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by          BIGINT
);

CREATE INDEX IF NOT EXISTS idx_inv_movements_company ON inventory.movements (company_id);
CREATE INDEX IF NOT EXISTS idx_inv_movements_branch ON inventory.movements (company_id, branch_id);
CREATE INDEX IF NOT EXISTS idx_inv_movements_product ON inventory.movements (company_id, product_id);
CREATE INDEX IF NOT EXISTS idx_inv_movements_type ON inventory.movements (company_id, movement_type);
CREATE INDEX IF NOT EXISTS idx_inv_movements_created ON inventory.movements (company_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_inv_movements_ref ON inventory.movements (reference_type, reference_id);

-- -------------------------------------------
-- 6. AI_INTERACTIONS
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS inventory.ai_interactions (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id           BIGINT NOT NULL REFERENCES core.branches(id),
    user_id             BIGINT NOT NULL REFERENCES core.users(id),

    -- Sesion
    session_id          VARCHAR(100) NOT NULL,
    interaction_type    VARCHAR(50) NOT NULL,

    -- Entrada del usuario
    user_input          TEXT,
    user_input_language VARCHAR(5) DEFAULT 'es',

    -- Respuesta IA
    ai_response         TEXT,
    ai_action           VARCHAR(50),

    -- Datos procesados (JSON)
    processed_data      JSONB,

    -- Resultado
    action_status       VARCHAR(20) DEFAULT 'pending',
    action_result       JSONB,

    -- Confianza
    confidence_score        DECIMAL(5, 2),
    requires_confirmation   BOOLEAN DEFAULT TRUE,
    confirmed_by_user       BOOLEAN DEFAULT FALSE,
    confirmed_at            TIMESTAMPTZ,

    -- Metadata IA
    ai_model            VARCHAR(50),
    tokens_used         INT,
    processing_time_ms  INT,

    -- Auditoria
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_inv_ai_company ON inventory.ai_interactions (company_id);
CREATE INDEX IF NOT EXISTS idx_inv_ai_branch ON inventory.ai_interactions (company_id, branch_id);
CREATE INDEX IF NOT EXISTS idx_inv_ai_user ON inventory.ai_interactions (user_id);
CREATE INDEX IF NOT EXISTS idx_inv_ai_session ON inventory.ai_interactions (session_id);
CREATE INDEX IF NOT EXISTS idx_inv_ai_created ON inventory.ai_interactions (company_id, created_at DESC);

-- -------------------------------------------
-- 7. ALERTS
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS inventory.alerts (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id           BIGINT NOT NULL REFERENCES core.branches(id),
    product_id          BIGINT REFERENCES inventory.products(id),

    -- Tipo
    alert_type          VARCHAR(50) NOT NULL,
    severity            VARCHAR(20) DEFAULT 'medium',

    -- Mensaje
    title               VARCHAR(200) NOT NULL,
    message             VARCHAR(1000) NOT NULL,

    -- Datos
    current_value       DECIMAL(18, 3),
    threshold_value     DECIMAL(18, 3),

    -- Sugerencia IA
    ai_suggestion       VARCHAR(1000),
    suggested_action    VARCHAR(50),

    -- Estado
    status              VARCHAR(20) DEFAULT 'active',
    acknowledged_by     BIGINT REFERENCES core.users(id),
    acknowledged_at     TIMESTAMPTZ,
    resolved_at         TIMESTAMPTZ,

    -- Notificacion
    notification_sent       BOOLEAN DEFAULT FALSE,
    notification_sent_at    TIMESTAMPTZ,

    -- Auditoria
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_inv_alerts_company ON inventory.alerts (company_id);
CREATE INDEX IF NOT EXISTS idx_inv_alerts_branch ON inventory.alerts (company_id, branch_id);
CREATE INDEX IF NOT EXISTS idx_inv_alerts_product ON inventory.alerts (product_id);
CREATE INDEX IF NOT EXISTS idx_inv_alerts_type ON inventory.alerts (company_id, alert_type);
CREATE INDEX IF NOT EXISTS idx_inv_alerts_status ON inventory.alerts (company_id, status);
CREATE INDEX IF NOT EXISTS idx_inv_alerts_created ON inventory.alerts (company_id, created_at DESC);
