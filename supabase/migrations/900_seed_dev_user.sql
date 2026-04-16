-- =============================================
-- Script: 900_seed_dev_user.sql
-- Descripcion: Crea la empresa sistema Walos y el usuario dev (super admin global)
-- Ejecutar UNA SOLA VEZ en Supabase SQL Editor
-- =============================================

DO $$
DECLARE
    v_company_id BIGINT;
    v_branch_id  BIGINT;
    v_role_id    BIGINT;
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

    -- 3. Rol dev (unico en esta empresa)
    INSERT INTO core.roles (
        company_id, name, code, description,
        permissions, access_level, is_system_role
    ) VALUES (
        v_company_id,
        'Desarrollador',
        'dev',
        'Super administrador del sistema SaaS. Acceso total.',
        '{"all": {"read": true, "write": true, "delete": true, "admin": true}}'::jsonb,
        100,
        TRUE
    )
    ON CONFLICT (company_id, code) DO NOTHING;

    SELECT id INTO v_role_id FROM core.roles WHERE company_id = v_company_id AND code = 'dev';

    -- 4. Usuario dev
    -- Contrasena: walos2024
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
        '$2a$11$8YxKYDqiRWQLMY.L44IYxe5oGyWGlAx01SU6UF69UO5Y1o9urLBia',
        'es', TRUE, TRUE
    )
    ON CONFLICT (email) DO NOTHING;

    RAISE NOTICE 'Usuario dev creado: dev@walos.app / walos2024';
    RAISE NOTICE 'company_id = %', v_company_id;

END $$;
