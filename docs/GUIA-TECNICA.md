# Guía técnica de Walos

> Revisión estática del árbol local, 2026-10-04; HEAD `6d22cfa` más cambios previos. Sin builds, tests, despliegues, migraciones ni conexión a DB. No se leyeron archivos `.env`.

## Fuentes de verdad

| Contrato | Fuente |
|---|---|
| Rutas UI / permisos de navegación | `frontend/src/App.jsx`, `config/companyFeatures.js` |
| API / requests | `backend-dotnet/src/Walos.API/Controllers`, DTOs de Application |
| Persistencia | Repositorios Infrastructure y `supabase/migrations` |
| Configuración API | `Walos.API/Program.cs` |
| Scripts frontend | `frontend/package.json` |
| PWA / E2E | `frontend/vite.config.js`, `frontend/playwright.config.js` |
| Pruebas backend | `tests/Walos.Tests/Walos.Tests.csproj`, `Integration/IntegrationTestBase.cs` |

## Configuración sin secretos

`Program.cs` carga `.env` desde el directorio del binario o el directorio actual. Mapea `DB_CONNECTION_STRING`, `JWT_SECRET`, opciones de expiración/refresh JWT, `OPENAI_API_KEY`, `OPENAI_MODEL`, `OPENAI_MAX_TOKENS`, `OPENAI_TEMPERATURE`, `CORS_ORIGINS`, `RATE_LIMIT_WINDOW_MS`, `RATE_LIMIT_MAX_REQUESTS`, `PORT`, `AI_KEY_ENCRYPTION_SECRET` y opciones de almacenamiento Supabase. Revisá código y ejemplo antes de configurar; no copies valores de documentos históricos.

`SqlConnectionFactory` usa Npgsql/PostgreSQL. No apuntes a una conexión SQL Server mencionada en documentación antigua. El frontend usa `VITE_API_URL` + `/api/` + `VITE_API_VERSION`; no incluyas dos veces el prefijo.

## Autorización

El tenant y sucursal vienen del JWT validado. `X-Branch-ID` no reemplaza el claim de sucursal. `CompanyFeatureMiddleware` y políticas del servidor son independientes de ocultar botones. No se afirma seguridad global por haber encontrado estos controles: los repositorios y escenarios negativos requieren validación propia.

## Base de datos

Seguí el [inventario de migraciones](../supabase/migrations/README.md), no una lista antigua reducida a 001–007. Los scripts 800, 900 y 999 se documentan allí como marcadores de compatibilidad; el bootstrap de demostración/sistema es manual opt-in. No ejecutar reset, seed ni migraciones sin probar que el destino está aislado/autorizado.

## Pruebas y estado de validación

Los comandos están en [backend](../backend-dotnet/README.md) y [frontend](../frontend/README.md). No hay conteos de tests vigentes certificados en esta revisión.

**Riesgo de integración:** `IntegrationTestBase` toma primero `ConnectionStrings:TestConnection` y después `WALOS_TEST_CONNECTION`; configura repositorios reales y su limpieza elimina datos de prueba por rangos de IDs. Revisá ambas fuentes antes de ejecutar. Sin conexión, los tests de integración se omiten; un resultado sin fallos con omisiones no prueba integración.

Playwright requiere servidor ya disponible, usando `E2E_BASE_URL` o localhost:5173. El workflow versionado encontrado corresponde al release del agente de impresión; no se lo presenta como un gate completo backend/frontend.

## Alcance no certificado

La UI y los controladores de caja, pagos, devoluciones, compras, delivery, usuarios y administración existen. No equivalen a despliegue/UAT aprobado. QR/Wompi y DIAN conservan documentos de propuesta: no se verificó un flujo real de cobro externo ni emisión fiscal. Ver [clasificación documental](README.md).
