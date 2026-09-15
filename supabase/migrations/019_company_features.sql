-- ============================================================
-- 019: Features habilitadas por comercio
--
-- Separa el acceso funcional de la facturacion SaaS existente en
-- platform.company_subscriptions. El dashboard es obligatorio.
-- ============================================================

-- La transaccion explicita protege tambien la ejecucion manual desde SQL Editor.
-- Ante cualquier RAISE/ERROR PostgreSQL aborta la transaccion completa: no deben
-- ejecutarse sentencias sueltas de este archivo ni intentar continuar tras error.
BEGIN;

-- Fail closed ANTES de cualquier DDL/DML. IF NOT EXISTS no debe esconder una
-- instalacion parcial o una tabla previa con contrato incompatible.
DO $migration_preflight$
DECLARE
    v_features_exists         BOOLEAN := to_regclass('platform.features') IS NOT NULL;
    v_company_features_exists BOOLEAN := to_regclass('platform.company_features') IS NOT NULL;
    v_incompatible            TEXT;
BEGIN
    IF v_features_exists <> v_company_features_exists THEN
        RAISE EXCEPTION
            'Estado parcial incompatible: platform.features y platform.company_features deben existir ambas o ninguna';
    END IF;

    -- En una instalacion nueva no hay estado anterior que validar.
    IF NOT v_features_exists THEN
        RETURN;
    END IF;

    -- Evita que el catalogo cambie entre el preflight y el seed canonico.
    LOCK TABLE platform.features, platform.company_features
        IN SHARE ROW EXCLUSIVE MODE;

    WITH expected(table_name, column_name, data_type, is_nullable, max_length, column_default) AS (
        VALUES
            ('features'::TEXT, 'code'::TEXT,            'character varying'::TEXT, 'NO'::TEXT, 50::INT,  NULL::TEXT),
            ('features',       'name',                  'character varying',       'NO',       100,      NULL),
            ('features',       'description',           'text',                    'YES',      NULL,     NULL),
            ('features',       'default_enabled',       'boolean',                 'NO',       NULL,     'false'),
            ('features',       'is_mandatory',          'boolean',                 'NO',       NULL,     'false'),
            ('features',       'is_active',             'boolean',                 'NO',       NULL,     'true'),
            ('features',       'display_order',         'integer',                 'NO',       NULL,     '0'),
            ('features',       'created_at',            'timestamp with time zone','NO',       NULL,     'now()'),
            ('features',       'updated_at',            'timestamp with time zone','NO',       NULL,     'now()'),
            ('features',       'updated_by',            'bigint',                  'YES',      NULL,     NULL),
            ('company_features','company_id',           'bigint',                  'NO',       NULL,     NULL),
            ('company_features','feature_code',         'character varying',       'NO',       50,       NULL),
            ('company_features','is_enabled',           'boolean',                 'NO',       NULL,     NULL),
            ('company_features','created_at',           'timestamp with time zone','NO',       NULL,     'now()'),
            ('company_features','updated_at',           'timestamp with time zone','NO',       NULL,     'now()'),
            ('company_features','updated_by',           'bigint',                  'YES',      NULL,     NULL)
    )
    SELECT string_agg(format('platform.%s.%s', e.table_name, e.column_name), ', ' ORDER BY e.table_name, e.column_name)
    INTO v_incompatible
    FROM expected e
    LEFT JOIN information_schema.columns c
      ON c.table_schema = 'platform'
     AND c.table_name = e.table_name
     AND c.column_name = e.column_name
    WHERE c.column_name IS NULL
       OR c.data_type IS DISTINCT FROM e.data_type
       OR c.is_nullable IS DISTINCT FROM e.is_nullable
       OR (e.max_length IS NOT NULL AND c.character_maximum_length IS DISTINCT FROM e.max_length)
       OR (e.column_default IS NULL AND c.column_default IS NOT NULL)
       OR (e.column_default IS NOT NULL AND lower(c.column_default) IS DISTINCT FROM e.column_default);

    IF v_incompatible IS NOT NULL THEN
        RAISE EXCEPTION 'Columnas requeridas ausentes o incompatibles: %', v_incompatible;
    END IF;

    -- Claves primarias y foraneas requeridas, verificadas por columnas y destino
    -- (no solo por nombre), para no aceptar una tabla homonima mal formada.
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint c
        WHERE c.conrelid = 'platform.features'::regclass
          AND c.contype = 'p'
          AND c.convalidated
          AND c.conkey = ARRAY[(SELECT attnum FROM pg_attribute
                               WHERE attrelid = 'platform.features'::regclass AND attname = 'code')]::SMALLINT[]
    ) OR NOT EXISTS (
        SELECT 1
        FROM pg_constraint c
        WHERE c.conrelid = 'platform.company_features'::regclass
          AND c.contype = 'p'
          AND c.convalidated
          AND c.conkey = ARRAY[
              (SELECT attnum FROM pg_attribute WHERE attrelid = 'platform.company_features'::regclass AND attname = 'company_id'),
              (SELECT attnum FROM pg_attribute WHERE attrelid = 'platform.company_features'::regclass AND attname = 'feature_code')
          ]::SMALLINT[]
    ) THEN
        RAISE EXCEPTION 'Claves primarias requeridas ausentes o incompatibles';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint c
        WHERE c.conrelid = 'platform.features'::regclass
          AND c.contype = 'f'
          AND c.convalidated
          AND c.conkey = ARRAY[(SELECT attnum FROM pg_attribute WHERE attrelid = 'platform.features'::regclass AND attname = 'updated_by')]::SMALLINT[]
          AND c.confrelid = 'core.users'::regclass
          AND c.confkey = ARRAY[(SELECT attnum FROM pg_attribute WHERE attrelid = 'core.users'::regclass AND attname = 'id')]::SMALLINT[]
          AND c.confdeltype = 'n'
    ) OR NOT EXISTS (
        SELECT 1 FROM pg_constraint c
        WHERE c.conrelid = 'platform.company_features'::regclass
          AND c.contype = 'f'
          AND c.convalidated
          AND c.conkey = ARRAY[(SELECT attnum FROM pg_attribute WHERE attrelid = 'platform.company_features'::regclass AND attname = 'company_id')]::SMALLINT[]
          AND c.confrelid = 'core.companies'::regclass
          AND c.confkey = ARRAY[(SELECT attnum FROM pg_attribute WHERE attrelid = 'core.companies'::regclass AND attname = 'id')]::SMALLINT[]
          AND c.confdeltype = 'c'
    ) OR NOT EXISTS (
        SELECT 1 FROM pg_constraint c
        WHERE c.conrelid = 'platform.company_features'::regclass
          AND c.contype = 'f'
          AND c.convalidated
          AND c.conkey = ARRAY[(SELECT attnum FROM pg_attribute WHERE attrelid = 'platform.company_features'::regclass AND attname = 'feature_code')]::SMALLINT[]
          AND c.confrelid = 'platform.features'::regclass
          AND c.confkey = ARRAY[(SELECT attnum FROM pg_attribute WHERE attrelid = 'platform.features'::regclass AND attname = 'code')]::SMALLINT[]
          AND c.confdeltype = 'a'
    ) OR NOT EXISTS (
        SELECT 1 FROM pg_constraint c
        WHERE c.conrelid = 'platform.company_features'::regclass
          AND c.contype = 'f'
          AND c.convalidated
          AND c.conkey = ARRAY[(SELECT attnum FROM pg_attribute WHERE attrelid = 'platform.company_features'::regclass AND attname = 'updated_by')]::SMALLINT[]
          AND c.confrelid = 'core.users'::regclass
          AND c.confkey = ARRAY[(SELECT attnum FROM pg_attribute WHERE attrelid = 'core.users'::regclass AND attname = 'id')]::SMALLINT[]
          AND c.confdeltype = 'n'
    ) THEN
        RAISE EXCEPTION 'Claves foraneas requeridas ausentes o incompatibles';
    END IF;

    WITH expected(constraint_name, table_name, required_fragments) AS (
        VALUES
            ('chk_features_code'::TEXT, 'features'::TEXT,
             ARRAY['code', '^[a-z][a-z0-9_]{0,49}$']::TEXT[]),
            ('chk_features_dashboard_mandatory', 'features',
             ARRAY['dashboard', 'default_enabled', 'is_mandatory', 'is_active']),
            ('chk_company_features_dashboard_enabled', 'company_features',
             ARRAY['dashboard', 'feature_code', 'is_enabled'])
    )
    SELECT string_agg(e.constraint_name, ', ' ORDER BY e.constraint_name)
    INTO v_incompatible
    FROM expected e
    LEFT JOIN pg_constraint c
      ON c.conname = e.constraint_name
     AND c.conrelid = format('platform.%I', e.table_name)::regclass
     AND c.contype = 'c'
    WHERE c.oid IS NULL
       OR NOT c.convalidated
       OR EXISTS (
            SELECT 1
            FROM unnest(e.required_fragments) fragment
            WHERE position(lower(fragment) IN lower(pg_get_constraintdef(c.oid))) = 0
       );

    IF v_incompatible IS NOT NULL THEN
        RAISE EXCEPTION 'Restricciones CHECK requeridas ausentes o incompatibles: %', v_incompatible;
    END IF;

    WITH expected(code, name, description, default_enabled, is_mandatory, is_active, display_order) AS (
        VALUES
            ('dashboard'::TEXT,  'Dashboard'::TEXT,    'Resumen operativo del comercio'::TEXT,                TRUE,  TRUE,  TRUE, 1),
            ('inventory',        'Inventario',         'Productos, existencias, movimientos y alertas',       TRUE,  FALSE, TRUE, 2),
            ('restaurant',       'Restaurante',        'Mesas, ordenes y cierre de venta restaurante',        TRUE,  FALSE, TRUE, 3),
            ('pos',              'POS-Deli',           'Venta rapida de mostrador',                           TRUE,  FALSE, TRUE, 4),
            ('cash',             'Caja',               'Apertura, movimientos y cierre de caja',              TRUE,  FALSE, TRUE, 5),
            ('purchases',        'Compras',            'Ordenes de compra y recepcion',                       TRUE,  FALSE, TRUE, 6),
            ('suppliers',        'Proveedores',        'Catalogo y gestion de proveedores',                   TRUE,  FALSE, TRUE, 7),
            ('delivery',         'Domicilios',         'Pedidos y seguimiento de domicilios',                 TRUE,  FALSE, TRUE, 8),
            ('finance',          'Finanzas',           'Ingresos, egresos y reportes financieros',            TRUE,  FALSE, TRUE, 9),
            ('ai',               'Asistente IA',       'Asistente inteligente de Walos',                      FALSE, FALSE, TRUE, 10)
    )
    SELECT string_agg(f.code, ', ' ORDER BY f.code)
    INTO v_incompatible
    FROM platform.features f
    JOIN expected e USING (code)
    WHERE f.name IS DISTINCT FROM e.name
       OR f.description IS DISTINCT FROM e.description
       OR f.default_enabled IS DISTINCT FROM e.default_enabled
       OR f.is_mandatory IS DISTINCT FROM e.is_mandatory
       OR f.is_active IS DISTINCT FROM e.is_active
       OR f.display_order IS DISTINCT FROM e.display_order;

    IF v_incompatible IS NOT NULL THEN
        RAISE EXCEPTION 'Definicion canonica incompatible para features existentes: %', v_incompatible;
    END IF;
END
$migration_preflight$;

CREATE TABLE IF NOT EXISTS platform.features (
    code                VARCHAR(50) PRIMARY KEY,
    name                VARCHAR(100) NOT NULL,
    description         TEXT,
    default_enabled     BOOLEAN NOT NULL DEFAULT FALSE,
    is_mandatory        BOOLEAN NOT NULL DEFAULT FALSE,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    display_order       INT NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by          BIGINT REFERENCES core.users(id) ON DELETE SET NULL,

    CONSTRAINT chk_features_code
        CHECK (code ~ '^[a-z][a-z0-9_]{0,49}$'),
    CONSTRAINT chk_features_dashboard_mandatory
        CHECK (code <> 'dashboard' OR (default_enabled AND is_mandatory AND is_active))
);

CREATE TABLE IF NOT EXISTS platform.company_features (
    company_id          BIGINT NOT NULL
                        REFERENCES core.companies(id) ON DELETE CASCADE,
    feature_code        VARCHAR(50) NOT NULL
                        REFERENCES platform.features(code),
    is_enabled          BOOLEAN NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by          BIGINT REFERENCES core.users(id) ON DELETE SET NULL,

    PRIMARY KEY (company_id, feature_code),
    CONSTRAINT chk_company_features_dashboard_enabled
        CHECK (feature_code <> 'dashboard' OR is_enabled)
);

CREATE INDEX IF NOT EXISTS idx_company_features_enabled
    ON platform.company_features (company_id, is_enabled);

INSERT INTO platform.features
    (code, name, description, default_enabled, is_mandatory, display_order)
VALUES
    ('dashboard',   'Dashboard',     'Resumen operativo del comercio',                 TRUE,  TRUE,  1),
    ('inventory',   'Inventario',    'Productos, existencias, movimientos y alertas',  TRUE,  FALSE, 2),
    ('restaurant',  'Restaurante',   'Mesas, ordenes y cierre de venta restaurante',   TRUE,  FALSE, 3),
    ('pos',         'POS-Deli',      'Venta rapida de mostrador',                      TRUE,  FALSE, 4),
    ('cash',        'Caja',          'Apertura, movimientos y cierre de caja',         TRUE,  FALSE, 5),
    ('purchases',   'Compras',       'Ordenes de compra y recepcion',                  TRUE,  FALSE, 6),
    ('suppliers',   'Proveedores',   'Catalogo y gestion de proveedores',              TRUE,  FALSE, 7),
    ('delivery',    'Domicilios',    'Pedidos y seguimiento de domicilios',            TRUE,  FALSE, 8),
    ('finance',     'Finanzas',      'Ingresos, egresos y reportes financieros',       TRUE,  FALSE, 9),
    ('ai',          'Asistente IA',  'Asistente inteligente de Walos',                 FALSE, FALSE, 10)
ON CONFLICT (code) DO NOTHING;

-- Todo comercio existente conserva los modulos operativos. IA inicia apagada.
INSERT INTO platform.company_features
    (company_id, feature_code, is_enabled)
SELECT c.id, f.code, f.default_enabled
FROM core.companies c
CROSS JOIN platform.features f
ON CONFLICT (company_id, feature_code) DO NOTHING;

-- Rol reservado para administracion global. No se crea ni asigna ningun usuario.
INSERT INTO core.roles
    (company_id, name, code, description, permissions, access_level, is_system_role, is_active)
SELECT
    c.id,
    'Administrador de Plataforma',
    'platform_admin',
    'Administracion global de comercios y funcionalidades de Walos.',
    '{"platform": {"admin": true}}'::jsonb,
    100,
    TRUE,
    TRUE
FROM core.companies c
WHERE c.tax_id = 'WALOS-SYSTEM-001'
ON CONFLICT (company_id, code) DO NOTHING;

COMMIT;
