-- =============================================
-- Script: 002_core_tables.sql
-- Descripcion: Tablas core (companies, branches, roles, users)
-- Target: Supabase (PostgreSQL)
-- Multi-tenant: company_id en todas las tablas de negocio
-- =============================================

-- -------------------------------------------
-- 1. COMPANIES (tabla raiz del tenant)
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS core.companies (
    id              BIGSERIAL PRIMARY KEY,

    -- Informacion basica
    name            VARCHAR(200) NOT NULL,
    legal_name      VARCHAR(300) NOT NULL,
    tax_id          VARCHAR(50) NOT NULL UNIQUE,

    -- Contacto
    email           VARCHAR(100),
    phone           VARCHAR(20),
    website         VARCHAR(200),

    -- Direccion
    address         VARCHAR(500),
    city            VARCHAR(100),
    state           VARCHAR(100),
    country         VARCHAR(2) DEFAULT 'CO',
    postal_code     VARCHAR(10),

    -- Configuracion
    currency        VARCHAR(3) DEFAULT 'COP',
    timezone        VARCHAR(50) DEFAULT 'America/Bogota',
    language        VARCHAR(5) DEFAULT 'es',

    -- Branding
    display_name    VARCHAR(200),
    logo_url        VARCHAR(500),
    primary_color   VARCHAR(7) DEFAULT '#1a73e8',
    theme_preference VARCHAR(30) NOT NULL DEFAULT 'light',

    -- Reglas de descuento
    manual_discount_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    max_discount_percent    DECIMAL(5,2) NOT NULL DEFAULT 15,
    max_discount_amount     DECIMAL(18,2) NOT NULL DEFAULT 50000,
    discount_requires_override BOOLEAN NOT NULL DEFAULT FALSE,
    discount_override_threshold_percent DECIMAL(5,2) NOT NULL DEFAULT 10,

    -- Suscripcion
    is_active               BOOLEAN DEFAULT TRUE,
    subscription_plan       VARCHAR(50) DEFAULT 'basic',
    subscription_expires_at TIMESTAMPTZ,

    -- Auditoria
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    created_by      BIGINT,
    updated_by      BIGINT
);

CREATE INDEX IF NOT EXISTS idx_companies_tax_id ON core.companies (tax_id);
CREATE INDEX IF NOT EXISTS idx_companies_is_active ON core.companies (is_active) WHERE deleted_at IS NULL;

-- -------------------------------------------
-- 2. BRANCHES (sucursales)
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS core.branches (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),

    -- Informacion basica
    name            VARCHAR(200) NOT NULL,
    code            VARCHAR(20) NOT NULL,
    branch_type     VARCHAR(50) NOT NULL,

    -- Contacto
    email           VARCHAR(100),
    phone           VARCHAR(20),

    -- Direccion
    address         VARCHAR(500) NOT NULL,
    city            VARCHAR(100) NOT NULL,
    state           VARCHAR(100),
    country         VARCHAR(2) DEFAULT 'CO',
    postal_code     VARCHAR(10),

    -- Geolocalizacion
    latitude        DECIMAL(10, 8),
    longitude       DECIMAL(11, 8),

    -- Horarios (JSON)
    business_hours  JSONB,

    -- Configuracion
    max_tables      INT,
    max_capacity    INT,

    -- Estado
    is_active       BOOLEAN DEFAULT TRUE,
    is_main         BOOLEAN DEFAULT FALSE,

    -- Auditoria
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    created_by      BIGINT,
    updated_by      BIGINT,

    -- Constraints
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS idx_branches_company_id ON core.branches (company_id);
CREATE INDEX IF NOT EXISTS idx_branches_active ON core.branches (company_id, is_active) WHERE deleted_at IS NULL;

-- -------------------------------------------
-- 3. ROLES
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS core.roles (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),

    -- Informacion del rol
    name            VARCHAR(100) NOT NULL,
    code            VARCHAR(50) NOT NULL,
    description     VARCHAR(500),

    -- Permisos (JSON)
    permissions     JSONB NOT NULL DEFAULT '{}',

    -- Nivel de acceso
    access_level    INT DEFAULT 1,

    -- Tipo de rol
    is_system_role  BOOLEAN DEFAULT FALSE,
    is_active       BOOLEAN DEFAULT TRUE,

    -- Auditoria
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    created_by      BIGINT,
    updated_by      BIGINT,

    -- Constraints
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS idx_roles_company_id ON core.roles (company_id);
CREATE INDEX IF NOT EXISTS idx_roles_code ON core.roles (company_id, code);

-- -------------------------------------------
-- 4. USERS
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS core.users (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id       BIGINT REFERENCES core.branches(id),
    role_id         BIGINT NOT NULL REFERENCES core.roles(id),

    -- Informacion personal
    first_name      VARCHAR(100) NOT NULL,
    last_name       VARCHAR(100) NOT NULL,
    email           VARCHAR(100) NOT NULL UNIQUE,
    phone           VARCHAR(20),

    -- Autenticacion
    password_hash   VARCHAR(255) NOT NULL,
    password_salt   VARCHAR(100),

    -- Tokens
    refresh_token               VARCHAR(500),
    refresh_token_expires_at    TIMESTAMPTZ,
    reset_password_token        VARCHAR(100),
    reset_password_expires_at   TIMESTAMPTZ,

    -- Configuracion de usuario
    language        VARCHAR(5) DEFAULT 'es',
    timezone        VARCHAR(50),
    avatar_url      VARCHAR(500),

    -- Seguridad
    failed_login_attempts   INT DEFAULT 0,
    locked_until            TIMESTAMPTZ,
    last_login_at           TIMESTAMPTZ,
    last_login_ip           VARCHAR(45),

    -- Estado
    is_active           BOOLEAN DEFAULT TRUE,
    email_verified      BOOLEAN DEFAULT FALSE,
    email_verified_at   TIMESTAMPTZ,

    -- Auditoria
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMPTZ,
    created_by      BIGINT,
    updated_by      BIGINT
);

CREATE INDEX IF NOT EXISTS idx_users_company_id ON core.users (company_id);
CREATE INDEX IF NOT EXISTS idx_users_branch_id ON core.users (branch_id);
CREATE INDEX IF NOT EXISTS idx_users_role_id ON core.users (role_id);
CREATE INDEX IF NOT EXISTS idx_users_email ON core.users (email);
CREATE INDEX IF NOT EXISTS idx_users_active ON core.users (company_id, is_active) WHERE deleted_at IS NULL;
