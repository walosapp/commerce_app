# Auditoría de Código — Walos App

> **Fecha**: Abril 2026
> **Alcance**: Backend (.NET 8), Frontend (React 18), Documentación, Seguridad, Clean Code
> **Severidad**: `CRITICO` | `ALTO` | `MEDIO` | `BAJO` | `INFO`

---

## Resumen Ejecutivo

| Categoría | Críticos | Altos | Medios | Bajos | Info |
|-----------|----------|-------|--------|-------|------|
| Seguridad | 1 | 3 | 2 | 1 | 0 |
| Clean Code / Patrones | 0 | 2 | 4 | 3 | 0 |
| Documentación desactualizada | 0 | 1 | 3 | 2 | 0 |
| Testing | 0 | 1 | 1 | 0 | 0 |
| **Total** | **1** | **7** | **10** | **6** | **0** |

---

## 1. SEGURIDAD

### SEC-01 · Frontend `.env` NO está en `.gitignore` `CRITICO`

**Archivo**: `frontend/.gitignore`

El `.gitignore` del frontend solo excluye `.env.local`, `.env.development.local`, etc. — pero **NO excluye `.env`** base. Si existe `frontend/.env` con `VITE_API_URL` u otras variables, se sube al repo.

```
# Actual en .gitignore:
.env.local
.env.development.local
.env.test.local
.env.production.local

# FALTA:
.env
```

**Riesgo**: Variables de entorno expuestas en el repositorio.
**Fix**: Agregar `.env` al `frontend/.gitignore`.

---

### SEC-02 · AuthController retorna respuesta sin `ApiResponse` estándar `ALTO`

**Archivo**: `backend-dotnet/src/Walos.API/Controllers/AuthController.cs:105-129`

El endpoint `Login` retorna un objeto anónimo en vez de usar `ApiResponse<T>`, rompiendo el contrato de API. Si el frontend algún día valida `success` en todas las respuestas, esto puede fallar.

```csharp
// Actual:
return Ok(new { success = true, data = new { token = ..., user = ... } });

// Debería ser:
return Ok(ApiResponse<object>.Ok(new { token = ..., refreshToken, user = ... }, "Login exitoso"));
```

El endpoint `RefreshToken` (línea 151) tiene el mismo problema.

**Riesgo**: Inconsistencia en el contrato de API.
**Fix**: Usar `ApiResponse<T>.Ok()` en ambos endpoints.

---

### SEC-03 · Refresh token no se invalida al usarlo `ALTO`

**Archivo**: `AuthController.cs:132-160`

El endpoint `/auth/refresh` genera un nuevo refresh token pero **no invalida el anterior**. Solo lo sobreescribe con `SaveRefreshTokenAsync`. Si un atacante captura un refresh token antes de que se use, ambos tokens (viejo y nuevo) son válidos hasta que expire el viejo naturalmente.

**Riesgo**: Token replay attack — un refresh token capturado sigue siendo válido.
**Fix**: `SaveRefreshTokenAsync` ya sobreescribe el token en DB (una sola columna), así que el viejo queda invalidado al guardarse el nuevo. Sin embargo, no hay revocación explícita al hacer logout. Agregar `RevokeRefreshTokenAsync` al logout.

---

### SEC-04 · No hay endpoint de logout que revoque tokens `ALTO`

**Archivo**: `AuthController.cs`

No existe un endpoint `POST /auth/logout`. El frontend solo limpia localStorage, pero el JWT y refresh token siguen siendo válidos en el servidor hasta que expiren.

**Riesgo**: Tokens activos después de "logout" del usuario.
**Fix**: Crear endpoint `POST /auth/logout` que:
1. Limpie `refresh_token` y `refresh_token_expires_at` del usuario en DB.
2. (Opcional) Agregue el JWT a una blacklist en caché (Redis) hasta su expiración.

---

### SEC-05 · Upload de archivos sin sanitización de nombre `MEDIO`

**Archivos**: `InventoryController.cs:173`, `CompanyController.cs` (upload logo)

Se usa `Path.GetExtension(file.FileName)` directamente. Aunque se genera un nombre propio, el `file.FileName` original no se sanitiza. Riesgo mínimo porque no se usa el nombre original en el path final, pero `Path.GetExtension` podría retornar extensiones inesperadas.

**Fix**: Validar extensión contra lista blanca explícita:
```csharp
var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
if (!allowedExtensions.Contains(ext))
    return BadRequest(ApiResponse.Fail("Extensión no permitida"));
```

---

### SEC-06 · `X-Tenant-ID` header enviado pero nunca usado en backend `MEDIO`

**Archivos**: `frontend/src/config/api.js:28-31`, `TenantContextMiddleware.cs`

El frontend envía `X-Tenant-ID` en cada request, pero el middleware solo lee `X-Branch-ID` del header. El `CompanyId` se extrae exclusivamente del JWT claim. El header `X-Tenant-ID` es código muerto y podría confundir a futuros desarrolladores.

**Fix**: Eliminar `X-Tenant-ID` del interceptor de axios o documentar que es legacy.

---

### SEC-07 · `ExceptionHandlingMiddleware` expone mensajes de excepción en producción `BAJO`

**Archivo**: `ExceptionHandlingMiddleware.cs:49`

La respuesta siempre incluye `exception.Message`, incluso en producción. Para errores 500, esto podría filtrar detalles internos (nombres de tablas, connection strings en errores de DB, etc.).

**Fix**: Para status 500, usar un mensaje genérico:
```csharp
response["message"] = statusCode == 500 && !_env.IsDevelopment()
    ? "Error interno del servidor"
    : exception.Message;
```

---

## 2. CLEAN CODE Y PATRONES

### CC-01 · Controllers con lógica de negocio (Fat Controllers) `ALTO`

**Archivos**: `SalesController.cs`, `FinanceController.cs`, `CompanyController.cs`

Los controllers contienen lógica de negocio directamente (validaciones de descuento, cálculos de totales, validación de items). Solo `InventoryController` usa un servicio (`IInventoryService`).

**Patrón definido**: Clean Architecture requiere que la lógica esté en la capa Application.

**Ejemplo** — `SalesController.InvoiceTable` (líneas 156-278) hace:
- Validación de disponibilidad
- Cálculo de descuentos
- Actualización de stock
- Registro de movimientos
- Actualización de orden y mesa

Todo debería estar en un `ISalesService.InvoiceTableAsync()`.

**Fix**: Crear `ISalesService`, `IFinanceService`, `ICompanyService` en Application layer y mover la lógica de negocio ahí. Los controllers solo deben orquestar: validar request → llamar service → retornar response.

---

### CC-02 · `FinanceRepository` excesivamente grande (30KB, ~800 líneas) `ALTO`

**Archivo**: `Infrastructure/Repositories/FinanceRepository.cs` — 30,048 bytes

Viola el principio de Single Responsibility. Comparar con el patrón del proyecto donde cada repositorio debería ser manejable.

**Fix**: Considerar dividir en `FinanceEntryRepository`, `FinanceCategoryRepository`, `FinanceTemplateRepository`, o al menos agrupar métodos con `#region`.

---

### CC-03 · Login response usa `new { }` anónimo en vez de DTO `MEDIO`

**Archivo**: `AuthController.cs:105-129`

El login retorna un objeto anónimo con la estructura del usuario. Debería ser un DTO explícito (`LoginResponse`) para type safety y documentación Swagger.

---

### CC-04 · `SalesController` inyecta `IInventoryRepository` directamente `MEDIO`

**Archivo**: `SalesController.cs:17`

Un controller no debería depender de repositories de otros módulos. Debería usar un servicio que orqueste la interacción entre módulos.

**Fix**: Crear `ISalesService` que internamente use `ISalesRepository` + `IInventoryRepository`.

---

### CC-05 · Archivo vacío en módulos `MEDIO`

**Archivos**: `frontend/src/modules/login.jsx` (0 bytes), `modules/company/`, `modules/users/`, `modules/suppliers/`

Archivos vacíos o placeholder que ensucian el workspace.

**Fix**: Eliminar archivos vacíos. Los placeholders en `App.jsx` ya documentan qué está pendiente.

---

### CC-06 · `authStore.js` duplica token en Zustand persist Y localStorage manual `MEDIO`

**Archivo**: `frontend/src/stores/authStore.js:20-23`

`setAuth` guarda manualmente en `localStorage.setItem('token', ...)` Y además Zustand persist guarda el mismo token en `auth-storage`. Esto crea dos fuentes de verdad.

**Fix**: Usar solo Zustand persist como fuente de verdad. El interceptor de axios debería leer de `useAuthStore.getState().token`.

---

### CC-07 · `queryClient` instanciado fuera de componente `BAJO`

**Archivo**: `frontend/src/App.jsx:22-30`

`new QueryClient()` está fuera del tree de React. Funciona, pero en Strict Mode puede causar problemas. Es una práctica válida pero vale documentar que es intencional.

---

### CC-08 · Inline placeholder components en App.jsx `BAJO`

**Archivo**: `frontend/src/App.jsx:63-64`

```jsx
<Route path="/suppliers" element={<ProtectedRoute><div className="text-center">...</div></ProtectedRoute>} />
```

Debería ser un componente `PlaceholderPage` reutilizable o directamente una redirección.

---

### CC-09 · `FinancialRecurringTemplate` service falta en frontend `BAJO`

**Archivo**: `frontend/src/services/financeService.js`

El service no incluye endpoints para recurring templates (`GET /finance/templates`, `POST /finance/templates`, etc.) aunque existen en el backend. Las llamadas se hacen directamente desde componentes.

---

## 3. DOCUMENTACIÓN DESACTUALIZADA

### DOC-01 · `architecture.md` severamente desactualizado `ALTO`

**Archivo**: `docs/architecture.md`

| Sección | Dice | Realidad |
|---------|------|----------|
| Base de datos | "SQL Server" | PostgreSQL (Supabase) |
| IA Model | "gpt-3.5-turbo" | "gpt-4" (configurable) |
| Estado Pendiente | "Login/Register UI" | Login está implementado |
| Estado Pendiente | "Módulo de Ventas: pendiente" | Ventas completado |
| Estado Pendiente | "PWA offline: service worker pendiente" | PWA configurada con Workbox |
| Estructura archivos | Solo muestra Inventory controller | Hay 6 controllers |
| Flujo de datos | "TenantContextMiddleware extrae via HttpContext.Items" | Usa ITenantContext scoped |
| Módulos | "Ventas (Pendiente)" | Ventas, Finanzas, Settings completados |
| Stack | No menciona Zustand, React Query, Tailwind | Son parte core del proyecto |

---

### DOC-02 · `database-schema.md` no incluye esquemas sales, finance, suppliers, delivery `MEDIO`

**Archivo**: `docs/database-schema.md`

Solo documenta `core` e `inventory`. Faltan: `sales` (tables, orders, order_items), `finance` (categories, entries, recurring_templates), `suppliers`, `delivery`.

---

### DOC-03 · `conexiones.md` tiene información contradictoria `MEDIO`

**Archivo**: `docs/conexiones.md`

- Línea 8 dice "SQL Server" pero línea 83 dice "PostgreSQL (Supabase)" — ambas en el mismo doc.
- Línea 9 muestra connection string de SQL Server.
- Sección "Autenticación para Desarrollo" (línea 74-79) dice "configurar manualmente en consola" pero ya existe login UI funcional.

---

### DOC-04 · `STYLE_GUIDE.md` dice "DB: SQL Server" `MEDIO`

**Archivo**: `docs/STYLE_GUIDE.md:30`

```
| DB | SQL Server (`SCM_App_Track_Me`) |
```

La DB es PostgreSQL en Supabase.

---

### DOC-05 · `.env.example` tiene connection string de SQL Server `BAJO`

**Archivo**: `backend-dotnet/src/Walos.API/.env.example:7`

```
DB_CONNECTION_STRING=Server=tu-servidor.database.windows.net;Database=...
```

Debería ser el formato PostgreSQL/Supabase.

---

### DOC-06 · `appsettings.json` tiene plantilla de connection string desactualizada `BAJO`

**Archivo**: `backend-dotnet/src/Walos.API/appsettings.json:6`

El template dice `Host=db.<project-ref>.supabase.co` pero no coincide con el formato de `.env.example`.

---

## 4. TESTING

### TEST-01 · Cobertura muy baja — solo 3 archivos de test `ALTO`

**Directorio**: `backend-dotnet/tests/Walos.Tests/`

Solo existen:
- `Services/InventoryServiceTests.cs` (~103 líneas)
- `Validators/CreateProductValidatorTests.cs`
- `Validators/AiInputValidatorTests.cs`
- `Repositories/TenantIsolationSqlTests.cs`

**Faltan tests para**:
- `AuthController` (login, lockout, refresh)
- `SalesController` (crear mesa, facturar, descuentos)
- `FinanceController` (entries, templates, month init)
- `CompanyController` (settings, theme, logo)
- Todos los repositories
- Frontend: 0 tests

**Regla del proyecto**: "Cobertura mínima 80% en pruebas unitarias".

---

### TEST-02 · No hay tests de integración ni E2E `MEDIO`

No hay configuración de Cypress, Playwright ni Selenium para tests E2E. No hay tests de integración que prueben la cadena completa controller → service → repository.

**Regla del proyecto**: "Pruebas E2E: flujos de usuario críticos".

---

## 5. PLAN DE ACCIÓN PRIORIZADO

| # | Hallazgo | Severidad | Esfuerzo | Acción |
|---|----------|-----------|----------|--------|
| 1 | SEC-01 | CRITICO | 1 min | Agregar `.env` a `frontend/.gitignore` |
| 2 | SEC-04 | ALTO | 30 min | Crear endpoint `POST /auth/logout` |
| 3 | SEC-02 | ALTO | 15 min | Usar `ApiResponse<T>` en auth endpoints |
| 4 | DOC-01 | ALTO | 1 hora | Reescribir `architecture.md` |
| 5 | CC-01 | ALTO | 4 horas | Crear service layer para Sales, Finance, Company |
| 6 | TEST-01 | ALTO | 8 horas | Agregar tests unitarios por módulo |
| 7 | SEC-03 | ALTO | 15 min | Verificar revocación de refresh token |
| 8 | SEC-07 | BAJO | 10 min | Mensajes genéricos en errores 500 producción |
| 9 | DOC-02-06 | MEDIO | 2 horas | Actualizar docs restantes |
| 10 | CC-02 | ALTO | 2 horas | Refactorizar FinanceRepository |
| 11 | SEC-05 | MEDIO | 10 min | Validar extensiones de archivo |
| 12 | SEC-06 | MEDIO | 5 min | Eliminar X-Tenant-ID del frontend |
| 13 | CC-05 | MEDIO | 5 min | Eliminar archivos vacíos |
| 14 | CC-06 | MEDIO | 30 min | Unificar fuente de verdad del token |
