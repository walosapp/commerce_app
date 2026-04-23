-- =============================================
-- Script: 014_business_hours.sql
-- Descripcion: Agrega horario de apertura/cierre a core.companies
--              Permite calcular el "dia comercial" correctamente para
--              negocios que cierran despues de medianoche
-- =============================================

ALTER TABLE core.companies
    ADD COLUMN IF NOT EXISTS business_open_time  TIME NOT NULL DEFAULT '00:00:00',
    ADD COLUMN IF NOT EXISTS business_close_time TIME NOT NULL DEFAULT '23:59:59';

COMMENT ON COLUMN core.companies.business_open_time  IS 'Hora de apertura del negocio (ej: 17:00 = 5pm)';
COMMENT ON COLUMN core.companies.business_close_time IS 'Hora de cierre del negocio (ej: 03:00 = 3am del dia siguiente). Si es menor que open_time, el turno cruza medianoche.';
