-- ============================================================================
-- BOOTSTRAP MANUAL OPT-IN: tenant sistema y cuenta tecnica dev
--
-- Antes de ejecutar este archivo, el operador debe suministrar en LA MISMA
-- sesion un hash BCrypt propio mediante:
--   SELECT set_config('walos.bootstrap_dev_password_hash', '<bcrypt>', false);
-- Nunca use este script como migracion automatica ni guarde el hash en Git.
-- ============================================================================

BEGIN;

DO $$
DECLARE
    v_password_hash TEXT := current_setting('walos.bootstrap_dev_password_hash', TRUE);
BEGIN
    IF v_password_hash IS NULL OR v_password_hash !~ '^\$2[aby]\$[0-9]{2}\$.{53}$' THEN
        RAISE EXCEPTION 'walos.bootstrap_dev_password_hash debe contener un hash BCrypt suministrado por el operador';
    END IF;
END $$;

DO $$
DECLARE
    v_company_id BIGINT;
    v_branch_id  BIGINT;
    v_role_id    BIGINT;
    v_password_hash TEXT := current_setting('walos.bootstrap_dev_password_hash');
BEGIN

    -- 1. Empresa sistema (la tuya como dueno del SaaS)
    INSERT INTO core.companies (
        name, legal_name, tax_id, email,
        display_name, currency, timezone, language,
        is_active, subscription_plan
    ) VALUES (
        'Walos System',
        'Walos Technologies S.A.S.',
        'WALOS-SYSTEM-001',
        'dev@walos.app',
        'Walos',
        'COP',
        'America/Bogota',
        'es',
        TRUE,
        'enterprise'
    )
    ON CONFLICT (tax_id) DO NOTHING;

    SELECT id INTO v_company_id FROM core.companies WHERE tax_id = 'WALOS-SYSTEM-001';

    -- 2. Sucursal principal
    INSERT INTO core.branches (
        company_id, name, code, branch_type,
        address, city, state, country,
        is_active, is_main
    ) VALUES (
        v_company_id, 'Sede Principal', 'HQ-01', 'office',
        'Virtual', 'Bogota', 'Cundinamarca', 'CO',
        TRUE, TRUE
    )
    ON CONFLICT (company_id, code) DO NOTHING;

    SELECT id INTO v_branch_id FROM core.branches WHERE company_id = v_company_id AND code = 'HQ-01';

    -- 3. Roles reservados de plataforma. Es idempotente aun cuando este
    -- seed se ejecute despues de 019_company_features.sql.
    INSERT INTO core.roles (
        company_id, name, code, description,
        permissions, access_level, is_system_role, is_active
    ) VALUES
    (
        v_company_id, 'Desarrollador', 'dev',
        'Super administrador del sistema SaaS. Acceso total.',
        '{"all": {"read": true, "write": true, "delete": true, "admin": true}}'::jsonb,
        100, TRUE, TRUE
    ),
    (
        v_company_id, 'Administrador de Plataforma', 'platform_admin',
        'Administracion global de comercios y funcionalidades de Walos.',
        '{"platform": {"admin": true}}'::jsonb,
        100, TRUE, TRUE
    )
    ON CONFLICT (company_id, code) DO NOTHING;

    -- Si la migracion 019 ya existe, Walos System recibe el mismo catalogo
    -- canonico que cualquier tenant. El SQL dinamico evita depender de 019 en
    -- instalaciones antiguas; platform_admin sigue creado solo en este tenant.
    IF to_regclass('platform.features') IS NOT NULL
       AND to_regclass('platform.company_features') IS NOT NULL THEN
        EXECUTE $feature_defaults$
            INSERT INTO platform.company_features
                (company_id, feature_code, is_enabled)
            SELECT c.id, f.code, f.default_enabled
            FROM core.companies c
            CROSS JOIN platform.features f
            ON CONFLICT (company_id, feature_code) DO NOTHING
        $feature_defaults$;
    END IF;

    SELECT id INTO v_role_id FROM core.roles WHERE company_id = v_company_id AND code = 'dev';

    -- 4. Usuario dev. El rol platform_admin queda disponible pero este script
    -- no crea ni asigna automaticamente un usuario para ese rol.
    INSERT INTO core.users (
        company_id, branch_id, role_id,
        first_name, last_name,
        email, phone,
        password_hash,
        language, is_active, email_verified
    ) VALUES (
        v_company_id, v_branch_id, v_role_id,
        'Super', 'Admin',
        'dev@walos.app',
        NULL,
        v_password_hash,
        'es', TRUE, TRUE
    )
    ON CONFLICT (email) DO NOTHING;

    RAISE NOTICE 'Usuario tecnico dev creado';
    RAISE NOTICE 'company_id = %', v_company_id;

END $$;

SELECT set_config('walos.bootstrap_dev_password_hash', '', FALSE);
COMMIT;
