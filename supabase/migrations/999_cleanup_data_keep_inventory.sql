-- ============================================================
-- Compatibilidad de historial: 999_cleanup_data_keep_inventory.sql
--
-- NO ejecuta limpieza. La version 999 se conserva como marcador no
-- destructivo para no romper instalaciones que ya registraron esta version.
-- El mantenimiento destructivo es exclusivamente manual y vive en:
--   supabase/scripts/cleanup_data_keep_inventory.sql
-- Nunca debe copiarse de nuevo al directorio de migraciones.
-- ============================================================

DO $$
BEGIN
    RAISE NOTICE '999 es un marcador no destructivo; el cleanup requiere ejecucion manual explicita';
END $$;
