-- =============================================
-- Script: 015_ai_sessions.sql
-- Descripcion: Tablas de sesiones y mensajes del agente orquestador de IA
-- =============================================

-- -------------------------------------------
-- 1. SESIONES DE CHAT
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS core.ai_sessions (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id) ON DELETE CASCADE,
    user_id             BIGINT NOT NULL REFERENCES core.users(id) ON DELETE CASCADE,
    agent_type          VARCHAR(30) NOT NULL DEFAULT 'orchestrator',
    context             JSONB NOT NULL DEFAULT '{}',
    last_activity_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_ai_sessions_company     ON core.ai_sessions (company_id);
CREATE INDEX IF NOT EXISTS idx_ai_sessions_user        ON core.ai_sessions (user_id);
CREATE INDEX IF NOT EXISTS idx_ai_sessions_activity    ON core.ai_sessions (last_activity_at);

-- -------------------------------------------
-- 2. MENSAJES DE LA SESION
-- -------------------------------------------
CREATE TABLE IF NOT EXISTS core.ai_messages (
    id              BIGSERIAL PRIMARY KEY,
    session_id      BIGINT NOT NULL REFERENCES core.ai_sessions(id) ON DELETE CASCADE,
    role            VARCHAR(15) NOT NULL CHECK (role IN ('user', 'assistant', 'system')),
    content         TEXT NOT NULL,
    metadata        JSONB NOT NULL DEFAULT '{}',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_ai_messages_session     ON core.ai_messages (session_id);
CREATE INDEX IF NOT EXISTS idx_ai_messages_created     ON core.ai_messages (session_id, created_at ASC);
