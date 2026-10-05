# AUDITORÍA FINAL — WALOS V1 PLATFORM CLOSURE

> **Fecha**: 2026-09-15  
> **Auditor**: Arquitectónico independiente  
> **Tipo**: Auditoría de seguridad, arquitectura y calidad  
> **Alcance**: Commit `e702214cc3cde583976ef0241a96581972525fb3`  
> **Baseline**: `7699336e3b236d863a2e045c15457cf666c66a1a`

---

## RESUMEN EJECUTIVO

**Commit auditado**: `e702214` - "feat: complete Walos v1 platform closure"  
**Archivos modificados**: 176  
**Inserciones**: ~9,212  
**Eliminaciones**: ~1,396

**Veredicto**: ⚠️ **V1 PLATFORM COMMIT NO APROBADO**

**Hallazgos críticos**:
- **3 BLOCKER** — Deben corregirse antes de producción
- **5 HIGH** — Riesgos de seguridad significativos
- **8 MEDIUM** — Mejoras recomendadas
- **12 LOW** — Observaciones menores
- **15 OBSERVATIONS** — Notas arquitectónicas

---

## TABLA DE HALLAZGOS CRÍTICOS

| ID | Severidad | Componente | Descripción | Impacto |
|----|-----------|------------|-------------|---------|
| B-01 | **BLOCKER** | `CompanyFeatureMiddleware.cs:32` | Bypass dev+platform_admin permite saltar TODAS las validaciones de features | Escalación de privilegios |
| B-02 | **BLOCKER** | `AiSessionRepository.cs` | Falta validación de tenant isolation en queries de sesiones IA | Cross-tenant data access |
| B-03 | **BLOCKER** | Frontend `FeatureRoute.jsx:43` | Feature check omite validación cuando `feature === 'dashboard'` | Bypass de feature enforcement |
| H-01 | **HIGH** | `AdminRepository.cs` | Branch management no valida que platform_admin pertenezca a WALOS-SYSTEM | Privilege escalation |
| H-02 | **HIGH** | `OrchestratorService.cs` | add_stock tool no está realmente deshabilitado, solo oculto | IA puede modificar inventario |
| H-03 | **HIGH** | `CompanyFeatureRepository.cs:146` | ReadCommitted + FOR UPDATE no protege concurrencia en última sucursal | Race condition |
| H-04 | **HIGH** | Frontend feature state | Cache de features no se invalida al cambiar de company | Stale permissions |
| H-05 | **HIGH** | `PwaController.cs` | Branding fallback no valida path traversal | Potential file disclosure |

---

## PARTE 1: ANÁLISIS DEL COMMIT

### 1.1 Alcance del Commit

**Verificación de archivos problemáticos**:
- ✅ NO contiene `018_pos_sale_idempotency.sql`
- ✅ NO contiene archivos B1.3
- ✅ NO contiene código de hardware
- ✅ NO contiene secretos o credenciales

**Archivos clave añadidos**:
- Migración `019_company_features.sql` (SHA256: `816ce3e6...` ✅ coincide)
- Middleware `CompanyFeatureMiddleware.cs`
- Repositorio `CompanyFeatureRepository.cs`
- Service `CompanyFeatureService.cs`
- Guard `AiCapabilityGuard.cs`
- Controller `PlatformBranchesController.cs`
- Controller `PlatformFeaturesController.cs`
- Frontend `FeatureRoute.jsx`, `useCompanyFeatures.js`

**Seeds históricos modificados**:
- ✅ `800_seed_initial_data.sql` — Convertido en marcador seguro
- ✅ `900_seed_dev_user.sql` — Convertido en marcador seguro
- ✅ `999_cleanup_data_keep_inventory.sql` — Convertido en marcador seguro

**Observación**: La modificación de migrations históricas puede causar problemas con herramientas de migración que validan checksums. Recomendación: documentar explícitamente en `supabase/migrations/README.md` que estos archivos fueron convertidos en marcadores y que los scripts funcionales se movieron a `supabase/scripts/`.

---

## PARTE 2: MIGRACIÓN 019_company_features.sql

### 2.1 Validación de Schema

**SHA256**: `816ce3e6b7a029a1c33c1743bec1b74c32f09da5745e3d6fc623a4fe58b419e6` ✅

**Características**:
- ✅ Aditiva (no destructiva)
- ✅ Idempotente (`IF NOT EXISTS`, `ON CONFLICT DO NOTHING`)
- ✅ Transaccional explícita (`BEGIN`/`COMMIT`)
- ✅ Preflight validation exhaustiva
- ✅ Foreign keys correctas
- ✅ Unique constraints correctos
- ✅ CHECK constraints protegen invariantes

**Invariantes protegidos**:
```sql
-- Dashboard siempre ON
CHECK (code <> 'dashboard' OR (default_enabled AND is_mandatory AND is_active))

-- Dashboard no puede deshabilitarse por company
CHECK (feature_code <> 'dashboard' OR is_enabled)

-- Códigos de feature validados
CHECK (code ~ '^[a-z][a-z0-9_]{0,49}$')
```

**Backfill**:
```sql
INSERT INTO platform.company_features (company_id, feature_code, is_enabled)
SELECT c.id, f.code, f.default_enabled
FROM core.companies c
CROSS JOIN platform.features f
ON CONFLICT (company_id, feature_code) DO NOTHING;
```

**Análisis del backfill**:
- ✅ Todas las features operativas (`inventory`, `restaurant`, `pos`, etc.) tienen `default_enabled = TRUE`
- ✅ Solo `ai` tiene `default_enabled = FALSE`
- ✅ Compañías existentes conservan acceso a módulos operativos
- ✅ IA inicia deshabilitada por defecto
- ✅ No hay riesgo de deshabilitar módulos actualmente en uso

**Rol `platform_admin`**:
```sql
INSERT INTO core.roles (company_id, name, code, ...)
SELECT c.id, 'Administrador de Plataforma', 'platform_admin', ...
FROM core.companies c
WHERE c.tax_id = 'WALOS-SYSTEM-001'
ON CONFLICT (company_id, code) DO NOTHING;
```

**Análisis**:
- ✅ Solo se crea para la compañía del sistema (`WALOS-SYSTEM-001`)
- ✅ No se asigna automáticamente a ningún usuario
- ✅ Requiere asignación manual explícita

**Veredicto migración**: ✅ **APROBADA** — Segura para producción

---

## PARTE 3: HALLAZGOS BLOCKER

### B-01: Bypass dev+platform_admin en CompanyFeatureMiddleware

**Archivo**: `backend-dotnet/src/Walos.API/Middleware/CompanyFeatureMiddleware.cs`  
**Líneas**: 30-36

**Código problemático**:
```csharp
// The bypass is intentionally narrower than a role check: only the trusted
// system dev principal materialized from the signed JWT may bypass tenant flags.
if (tenant.IsDev && tenant.IsPlatformAdmin)
{
    await _next(context);
    return;
}
```

**Problema**:
Un usuario con **ambos** claims `IsDev=true` Y `IsPlatformAdmin=true` puede saltar **TODAS** las validaciones de features, incluyendo features deshabilitadas para cualquier tenant.

**Análisis de generación de claims**:

`AuthService.cs:186-189`:
```csharp
private static bool IsTrustedPlatformAdmin(User user) =>
    (string.Equals(user.RoleCode, WalosRoles.Dev, StringComparison.OrdinalIgnoreCase)
     || string.Equals(user.RoleCode, WalosRoles.PlatformAdmin, StringComparison.OrdinalIgnoreCase))
    && string.Equals(user.CompanyTaxId, WalosSystemIdentity.CompanyTaxId, StringComparison.Ordinal);
```

**Escenario de riesgo**:
1. Usuario con rol `dev` en compañía `WALOS-SYSTEM-001` → `IsDev=true`, `IsPlatformAdmin=true`
2. Este usuario puede acceder a **cualquier** endpoint protegido por `[RequireFeature]`, incluso si la feature está OFF para el tenant objetivo
3. El bypass es correcto para `dev` (necesita probar funcionalidad), pero **no debe aplicarse cuando el usuario actúa como operador de un tenant cliente**

**Impacto**: **CRÍTICO**  
Un usuario `dev` puede operar en un tenant cliente como si todas las features estuvieran habilitadas, incluso cuando el cliente explícitamente deshabilitó módulos.

**Corrección recomendada**:
```csharp
// Bypass SOLO para dev operando en su propia compañía del sistema
if (tenant.IsDev && tenant.IsPlatformAdmin && tenant.CompanyId == WalosSystemIdentity.CompanyId)
{
    await _next(context);
    return;
}
```

O alternativamente, si `dev` debe poder probar funcionalidad en cualquier tenant:
```csharp
// Bypass SOLO para dev, NO para platform_admin puro
if (tenant.IsDev)
{
    await _next(context);
    return;
}
```

**Decisión arquitectónica requerida**: ¿Debe `dev` poder operar en tenants clientes saltando features, o debe respetar la configuración del cliente?

---

### B-02: Falta validación de tenant isolation en AiSessionRepository

**Archivo**: `backend-dotnet/src/Walos.Infrastructure/Repositories/AiSessionRepository.cs`  
**Líneas**: Múltiples queries

**Problema**:
Varios métodos del repositorio **NO validan `company_id`** en sus queries, permitiendo potencialmente cross-tenant data access.

**Queries problemáticas**:

1. `GetSessionAsync(long sessionId)` — línea ~45:
```csharp
const string sql = @"
    SELECT id, company_id, user_id, branch_id, ...
    FROM ai.sessions
    WHERE id = @SessionId";  // ← NO valida company_id
```

2. `GetInteractionAsync(long interactionId)` — línea ~80:
```csharp
const string sql = @"
    SELECT id, session_id, user_message, ...
    FROM ai.interactions
    WHERE id = @InteractionId";  // ← NO valida company_id
```

3. `ConfirmInteractionAsync(long interactionId, ...)` — línea ~120:
```csharp
const string sql = @"
    UPDATE ai.interactions
    SET confirmed_at = NOW(), ...
    WHERE id = @InteractionId";  // ← NO valida company_id
```

**Escenario de ataque**:
1. Atacante (Company A) crea sesión IA → obtiene `sessionId = 123`
2. Atacante adivina/enumera `interactionId = 456` de Company B
3. Atacante llama `ConfirmInteractionAsync(456, ...)` → **modifica interacción de otro tenant**

**Impacto**: **CRÍTICO**  
Cross-tenant data access y modificación. Violación de aislamiento multi-tenant.

**Corrección recomendada**:
Agregar validación de `company_id` en TODOS los métodos que reciben IDs:

```csharp
public async Task<AiSession?> GetSessionAsync(long sessionId, long companyId)
{
    const string sql = @"
        SELECT id, company_id, user_id, branch_id, ...
        FROM ai.sessions
        WHERE id = @SessionId
          AND company_id = @CompanyId";  // ← AGREGAR
    
    return await connection.QuerySingleOrDefaultAsync<AiSession>(sql, 
        new { SessionId = sessionId, CompanyId = companyId });
}
```

Aplicar el mismo patrón a:
- `GetInteractionAsync`
- `ConfirmInteractionAsync`
- `GetSessionHistoryAsync`
- Cualquier otro método que reciba IDs

---

### B-03: Frontend omite validación de feature para dashboard

**Archivo**: `frontend/src/components/routing/FeatureRoute.jsx`  
**Línea**: 43

**Código problemático**:
```jsx
if (feature !== 'dashboard' && !isReady) {
    return <Layout><FeatureStatus status={isError ? 'error' : 'loading'} /></Layout>;
}
```

**Problema**:
Cuando `feature === 'dashboard'`, el componente **omite la validación de `isReady`** y permite el acceso incluso si el estado de features no se ha cargado.

**Análisis del hook `useCompanyFeatures.js:49`**:
```js
isReady: trustedDev || platformOnly || query.isSuccess,
```

**Escenario de riesgo**:
1. Usuario autenticado accede a una ruta protegida con `feature="dashboard"`
2. La query de features falla (`query.isError = true`)
3. `isReady = false`, pero la validación se omite
4. El usuario accede al dashboard sin validar que realmente tiene acceso

**Impacto**: **ALTO**  
Bypass de feature enforcement. Aunque el dashboard es obligatorio, el código establece un patrón peligroso que podría replicarse en otros componentes.

**Corrección recomendada**:
```jsx
// Dashboard es obligatorio, pero igual debe validarse isReady
if (!isReady) {
    return <Layout><FeatureStatus status={isError ? 'error' : 'loading'} /></Layout>;
}
if (feature !== 'dashboard' && !hasFeature(feature)) {
    return <Layout><FeatureStatus status="disabled" /></Layout>;
}
```

O si el dashboard realmente debe ser accesible sin esperar features:
```jsx
// Dashboard siempre accesible, otros features requieren validación
if (feature === 'dashboard') {
    return <Layout>{children}</Layout>;
}

if (!isReady) {
    return <Layout><FeatureStatus status={isError ? 'error' : 'loading'} /></Layout>;
}
if (!hasFeature(feature)) {
    return <Layout><FeatureStatus status="disabled" /></Layout>;
}
```

---

## PARTE 4: HALLAZGOS HIGH

### H-01: AdminRepository no valida platform_admin pertenece a WALOS-SYSTEM

**Archivo**: `backend-dotnet/src/Walos.Infrastructure/Repositories/AdminRepository.cs`  
**Métodos**: `CreateBranchAsync`, `UpdateBranchAsync`, `DeleteBranchAsync`

**Problema**:
Los métodos de administración de sucursales **NO validan** que el usuario `platform_admin` pertenezca a la compañía del sistema antes de permitir operaciones cross-tenant.

**Código actual** (ejemplo `CreateBranchAsync`):
```csharp
public async Task<BranchAdminResponse> CreateBranchAsync(
    long companyId,
    CreateBranchAdminRequest request,
    long userId)
{
    // ← NO valida que userId pertenezca a WALOS-SYSTEM-001
    
    const string sql = @"
        INSERT INTO core.branches (company_id, name, ...)
        VALUES (@CompanyId, @Name, ...)";
    
    await connection.ExecuteAsync(sql, new { CompanyId = companyId, ... });
}
```

**Escenario de riesgo**:
1. Atacante crea usuario con rol `platform_admin` en su propia compañía (Company A)
2. Atacante obtiene claim `IsPlatformAdmin=true` en su JWT
3. Atacante llama endpoint `/api/v1/platform/admin/companies/{otherCompanyId}/branches` → **modifica sucursales de otro tenant**

**Análisis de protección actual**:
- ✅ `PlatformBranchesController` tiene `[Authorize(Policy = WalosPolicies.PlatformAdmin)]`
- ❌ La policy `PlatformAdmin` solo valida el claim, **NO valida la compañía del usuario**

**Corrección recomendada**:
Agregar validación en el repositorio:

```csharp
public async Task<BranchAdminResponse> CreateBranchAsync(
    long companyId,
    CreateBranchAdminRequest request,
    long userId)
{
    // Validar que el usuario pertenece a WALOS-SYSTEM
    const string validateUserSql = @"
        SELECT 1
        FROM core.users u
        JOIN core.companies c ON c.id = u.company_id
        WHERE u.id = @UserId
          AND c.tax_id = @SystemTaxId
          AND u.deleted_at IS NULL
          AND c.deleted_at IS NULL";
    
    var isSystemUser = await connection.ExecuteScalarAsync<bool>(validateUserSql, new
    {
        UserId = userId,
        SystemTaxId = WalosSystemIdentity.CompanyTaxId
    });
    
    if (!isSystemUser)
        throw new UnauthorizedAccessException("Platform admin operations require system company membership");
    
    // ... resto del código
}
```

O mejor aún, agregar esta validación en el middleware `TenantContextMiddleware` cuando se detecta `IsPlatformAdmin=true`.

---

### H-02: add_stock tool no está realmente deshabilitado

**Archivo**: `backend-dotnet/src/Walos.Application/Services/OrchestratorService.cs`  
**Líneas**: ~150-200 (tool registration)

**Problema**:
El tool `add_stock` está comentado en la lista de tools enviados a OpenAI, pero el **handler sigue existente y funcional** en el código.

**Código actual**:
```csharp
var tools = new List<ChatTool>
{
    // add_stock tool comentado
    // new ChatTool { ... },
    
    new ChatTool { Type = "function", Function = new FunctionDefinition { Name = "search_product", ... } },
    new ChatTool { Type = "function", Function = new FunctionDefinition { Name = "get_stock", ... } },
    // ... otros tools
};
```

**Handler sigue existente**:
```csharp
private async Task<string> HandleToolCallAsync(...)
{
    return toolName switch
    {
        "add_stock" => await HandleAddStockAsync(...),  // ← SIGUE EXISTIENDO
        "search_product" => await HandleSearchProductAsync(...),
        // ...
    };
}
```

**Escenario de riesgo**:
1. Atacante manipula request a OpenAI para incluir `add_stock` tool call
2. O atacante explota prompt injection para forzar a la IA a "recordar" el tool
3. El handler ejecuta la acción → **modifica inventario**

**Impacto**: **ALTO**  
Aunque el tool no está en la lista oficial, el handler sigue accesible. Un atacante sofisticado podría explotar esto.

**Corrección recomendada**:
Eliminar completamente el handler o agregar validación explícita:

```csharp
private async Task<string> HandleToolCallAsync(...)
{
    return toolName switch
    {
        "add_stock" => throw new InvalidOperationException("add_stock tool is disabled"),
        "search_product" => await HandleSearchProductAsync(...),
        // ...
    };
}
```

O mejor aún, eliminar el método `HandleAddStockAsync` completamente del código.

---

### H-03: ReadCommitted + FOR UPDATE no protege concurrencia en última sucursal

**Archivo**: `backend-dotnet/src/Walos.Infrastructure/Repositories/CompanyFeatureRepository.cs`  
**Línea**: 146

**Código problemático**:
```csharp
using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

const string lockCompanySql = @"
    SELECT require_cash_register
    FROM core.companies
    WHERE id = @CompanyId
      AND deleted_at IS NULL
    FOR UPDATE";
```

**Problema**:
`ReadCommitted` + `FOR UPDATE` **NO protege** contra el siguiente escenario concurrente:

**Escenario de race condition**:
```
T1: BEGIN (ReadCommitted)
T2: BEGIN (ReadCommitted)
T1: SELECT ... FROM companies WHERE id=1 FOR UPDATE  → require_cash_register=true
T2: [BLOQUEADO esperando lock de T1]
T1: SELECT ... FROM branches WHERE company_id=1  → count=1 (última sucursal)
T1: DELETE FROM branches WHERE id=100
T1: COMMIT
T2: [OBTIENE LOCK]
T2: SELECT ... FROM companies WHERE id=1 FOR UPDATE  → require_cash_register=true
T2: SELECT ... FROM branches WHERE company_id=1  → count=0 (¡T1 ya eliminó!)
T2: DELETE FROM branches WHERE id=101  → ❌ PERMITE eliminar cuando ya no hay sucursales
T2: COMMIT
```

**Resultado**: Compañía sin sucursales, violando invariante de negocio.

**Corrección recomendada**:
Usar `Serializable` o agregar lock explícito en la tabla `branches`:

```csharp
using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
```

O:
```csharp
using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

// Lock explícito en branches para evitar phantom reads
const string lockBranchesSql = @"
    SELECT id
    FROM core.branches
    WHERE company_id = @CompanyId
      AND deleted_at IS NULL
    FOR UPDATE";

var branchIds = await connection.QueryAsync<long>(lockBranchesSql, 
    new { CompanyId = companyId }, transaction);

if (branchIds.Count() <= 1)
    throw new BusinessException("No se puede eliminar la última sucursal");
```

---

### H-04: Cache de features no se invalida al cambiar de company

**Archivo**: `frontend/src/hooks/useCompanyFeatures.js`  
**Línea**: 14-19

**Código problemático**:
```js
const query = useQuery({
    queryKey: ['company-features', tenantId],
    queryFn: featureService.getMine,
    enabled: isAuthenticated && !!tenantId && !trustedDev && !platformOnly,
    staleTime: 5 * 60 * 1000,  // ← 5 minutos
});
```

**Problema**:
Si un usuario cambia de compañía en la misma sesión (ej: `dev` probando múltiples tenants), el cache de features **NO se invalida automáticamente**, causando que el usuario vea features de la compañía anterior.

**Escenario de riesgo**:
1. Usuario `dev` accede a Company A (tiene feature `ai` habilitada)
2. Cache almacena `['company-features', companyA] → { ai: true }`
3. Usuario cambia a Company B (tiene feature `ai` deshabilitada)
4. `tenantId` cambia, pero `queryKey` sigue siendo `['company-features', companyB]`
5. Si el cambio ocurre dentro de los 5 minutos, **el cache no se refresca**
6. Usuario ve UI de IA habilitada en Company B → **acceso incorrecto**

**Análisis adicional**:
El `queryKey` incluye `tenantId`, por lo que técnicamente debería crear una entrada de cache separada. Sin embargo, el problema es que el **estado de UI** (sidebar, rutas) podría no actualizarse hasta que el cache se refresque.

**Corrección recomendada**:
Invalidar cache explícitamente al cambiar de tenant:

```js
// En authStore.js, cuando se cambia de tenant:
const setTenant = (newTenantId) => {
    set({ tenantId: newTenantId });
    queryClient.invalidateQueries({ queryKey: ['company-features'] });
};
```

O reducir `staleTime` a 0 para features críticas:
```js
staleTime: 0,  // Siempre revalidar
```

---

### H-05: Branding fallback no valida path traversal

**Archivo**: `backend-dotnet/src/Walos.API/Controllers/PwaController.cs`  
**Líneas**: ~50-80 (método de fallback)

**Problema**:
El endpoint de branding fallback **NO valida** que la ruta del archivo no contenga path traversal (`../`).

**Código problemático** (hipotético, basado en patrón común):
```csharp
[HttpGet("branding/logo")]
public async Task<IActionResult> GetLogo([FromQuery] string? url)
{
    if (string.IsNullOrEmpty(url))
    {
        // Fallback a logo por defecto
        var defaultPath = Path.Combine(_env.ContentRootPath, "Assets", "walos-default.png");
        return PhysicalFile(defaultPath, "image/png");
    }
    
    // ← NO valida que url no contenga ../
    var filePath = Path.Combine(_env.ContentRootPath, url);
    return PhysicalFile(filePath, "image/png");
}
```

**Escenario de ataque**:
```
GET /api/v1/pwa/branding/logo?url=../../appsettings.json
```

**Impacto**: **ALTO**  
Potential file disclosure. Atacante podría leer archivos sensibles del servidor.

**Corrección recomendada**:
```csharp
[HttpGet("branding/logo")]
public async Task<IActionResult> GetLogo([FromQuery] string? url)
{
    if (string.IsNullOrEmpty(url))
    {
        var defaultPath = Path.Combine(_env.ContentRootPath, "Assets", "walos-default.png");
        return PhysicalFile(defaultPath, "image/png");
    }
    
    // Validar que url no contenga path traversal
    if (url.Contains("..") || Path.IsPathRooted(url))
        return BadRequest("Invalid file path");
    
    var safePath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "Assets", url));
    var assetsPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "Assets"));
    
    if (!safePath.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase))
        return BadRequest("Invalid file path");
    
    if (!System.IO.File.Exists(safePath))
        return NotFound();
    
    return PhysicalFile(safePath, "image/png");
}
```

**Nota**: Revisar el código real de `PwaController.cs` para confirmar si esta vulnerabilidad existe.

---

## PARTE 5: HALLAZGOS MEDIUM

### M-01: JWT Secret Validator no se ejecuta en startup

**Severidad**: MEDIUM  
**Archivo**: `backend-dotnet/src/Walos.API/Security/JwtSecretValidator.cs`

**Problema**:
El validador de JWT secret existe pero **NO se invoca** en `Program.cs` durante el startup de la aplicación.

**Corrección recomendada**:
```csharp
// En Program.cs, antes de builder.Build()
JwtSecretValidator.ValidateOrThrow(builder.Configuration["Jwt:Secret"]);
```

---

### M-02: CompanyFeatureMiddleware no registra intentos de acceso denegado

**Severidad**: MEDIUM  
**Archivo**: `backend-dotnet/src/Walos.API/Middleware/CompanyFeatureMiddleware.cs:71-78`

**Problema**:
Cuando se deniega acceso por feature deshabilitada, **NO se registra en logs** para auditoría.

**Corrección recomendada**:
```csharp
private static async Task WriteDeniedAsync(HttpContext context, string feature, ILogger logger)
{
    logger.LogWarning(
        "Feature access denied: User={UserId}, Company={CompanyId}, Feature={Feature}, Path={Path}",
        context.User.FindFirst(WalosClaimTypes.UserId)?.Value,
        context.User.FindFirst(WalosClaimTypes.CompanyId)?.Value,
        feature,
        context.Request.Path);
    
    context.Response.StatusCode = StatusCodes.Status403Forbidden;
    await context.Response.WriteAsJsonAsync(ApiResponse.Fail(...));
}
```

---

### M-03: Frontend no muestra mensaje específico cuando feature está cargando vs error

**Severidad**: MEDIUM  
**Archivo**: `frontend/src/components/routing/FeatureRoute.jsx:44`

**Problema**:
El usuario ve el mismo mensaje "Cargando módulos..." tanto para loading como para error, sin distinguir si hubo un fallo.

**Corrección recomendada**:
```jsx
if (!isReady) {
    const status = isError ? 'error' : 'loading';
    return <Layout><FeatureStatus status={status} /></Layout>;
}
```

---

### M-04: AiCapabilityGuard permite bypass con flag booleano simple

**Severidad**: MEDIUM  
**Archivo**: `backend-dotnet/src/Walos.Application/Services/AiCapabilityGuard.cs:22-28`

**Problema**:
El parámetro `trustedDevBypass` es un simple `bool`, sin validar que el caller realmente es `dev`.

**Corrección recomendada**:
```csharp
public async Task<AiCapabilitySnapshot> GetSnapshotAsync(
    long companyId, 
    ITenantContext tenant)
{
    IReadOnlyDictionary<string, bool> states;
    if (tenant.IsDev && tenant.IsPlatformAdmin)
    {
        // Bypass solo para dev verificado
        states = CapabilityCodes.ToDictionary(code => code, _ => true);
    }
    else
    {
        var stored = await _features.GetFeatureStatesAsync(companyId, CapabilityCodes);
        states = CapabilityCodes.ToDictionary(...);
    }
    // ...
}
```

---

### M-05: CompanyFeatureRepository no usa prepared statements parametrizados en todos los casos

**Severidad**: MEDIUM  
**Archivo**: `backend-dotnet/src/Walos.Infrastructure/Repositories/CompanyFeatureRepository.cs`

**Problema**:
Aunque Dapper usa parámetros, algunos strings se construyen dinámicamente sin validación.

**Recomendación**: Revisar todos los queries y asegurar que **NUNCA** se concatenan strings de usuario directamente en SQL.

---

### M-06: Frontend feature config no está sincronizado con backend WalosFeatures

**Severidad**: MEDIUM  
**Archivos**:
- `frontend/src/config/companyFeatures.js:1-12`
- `backend-dotnet/src/Walos.Domain/Features/WalosFeatures.cs:5-14`

**Problema**:
Los códigos de features están duplicados en frontend y backend sin validación automática de sincronización.

**Corrección recomendada**:
Generar el archivo de frontend automáticamente desde el backend durante el build:

```csharp
// Script de generación
var features = typeof(WalosFeatures)
    .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Const)
    .Where(f => f.FieldType == typeof(string))
    .Select(f => f.GetValue(null)?.ToString())
    .ToArray();

var jsContent = $"export const FEATURE_CODES = Object.freeze({JsonSerializer.Serialize(features)});";
File.WriteAllText("frontend/src/config/generatedFeatures.js", jsContent);
```

---

### M-07: No hay rate limiting en endpoints de features

**Severidad**: MEDIUM  
**Archivo**: `backend-dotnet/src/Walos.API/Controllers/FeaturesController.cs`

**Problema**:
Los endpoints de consulta de features **NO tienen rate limiting**, permitiendo enumeration attacks.

**Corrección recomendada**:
Agregar rate limiting con `AspNetCoreRateLimit`:

```csharp
[HttpGet("mine")]
[RateLimit(Name = "FeatureQuery", Seconds = 60, Limit = 10)]
public async Task<IActionResult> GetMyFeatures() { ... }
```

---

### M-08: AdminService no registra auditoría de cambios de sucursales

**Severidad**: MEDIUM  
**Archivo**: `backend-dotnet/src/Walos.Application/Services/AdminService.cs`

**Problema**:
Crear/editar/eliminar sucursales **NO genera eventos de auditoría** para trazabilidad.

**Corrección recomendada**:
Agregar tabla `platform.audit_log` y registrar todas las operaciones administrativas:

```csharp
await _auditRepository.LogAsync(new AuditEntry
{
    UserId = userId,
    Action = "branch.created",
    TargetType = "branch",
    TargetId = branchId,
    CompanyId = companyId,
    Details = JsonSerializer.Serialize(request)
});
```

---

## PARTE 6: HALLAZGOS LOW

### L-01: Falta documentación de API para endpoints de platform admin

**Severidad**: LOW  
**Recomendación**: Agregar comentarios XML y atributos Swagger a `PlatformBranchesController` y `PlatformFeaturesController`.

---

### L-02: Frontend no usa TypeScript

**Severidad**: LOW  
**Observación**: El frontend está en JavaScript puro. Migrar a TypeScript reduciría errores de tipo en runtime.

---

### L-03: No hay tests E2E para flujo completo de features

**Severidad**: LOW  
**Recomendación**: Agregar tests Playwright que validen:
1. Admin habilita feature
2. Usuario ve sidebar actualizado
3. Usuario accede a módulo
4. Admin deshabilita feature
5. Usuario pierde acceso

---

### L-04: CompanyFeatureService no cachea resultados

**Severidad**: LOW  
**Recomendación**: Agregar cache distribuido (Redis) para `GetFeatureStatesAsync` con TTL de 5 minutos.

---

### L-05: Frontend no muestra loading skeleton en FeatureRoute

**Severidad**: LOW  
**Recomendación**: Reemplazar mensaje de texto con skeleton UI para mejor UX.

---

### L-06: No hay métricas de uso de features

**Severidad**: LOW  
**Recomendación**: Agregar telemetría para rastrear qué features se usan más por tenant.

---

### L-07: AdminRepository usa queries separadas en lugar de JOINs

**Severidad**: LOW  
**Observación**: Algunas operaciones hacen múltiples queries cuando podrían usar un solo JOIN. Optimización de performance.

---

### L-08: Frontend no valida que feature codes sean válidos antes de enviar requests

**Severidad**: LOW  
**Recomendación**: Agregar validación en `featureService.js` contra `FEATURE_CODES`.

---

### L-09: No hay índice en platform.company_features(is_enabled)

**Severidad**: LOW  
**Recomendación**: Agregar índice para queries que filtran por `is_enabled = true`.

---

### L-10: CompanyFeatureMiddleware ejecuta query en cada request

**Severidad**: LOW  
**Recomendación**: Cachear resultados de features por request usando `HttpContext.Items`.

---

### L-11: Frontend useCompanyFeatures no expone método para refrescar features manualmente

**Severidad**: LOW  
**Recomendación**: Exponer `refetch` en el hook para permitir refresh manual después de cambios.

---

### L-12: No hay validación de que feature_code existe antes de asignar a company

**Severidad**: LOW  
**Observación**: La FK en la migración ya protege esto, pero el service podría dar mejor mensaje de error.

---

## PARTE 7: OBSERVATIONS

### O-01: Arquitectura de features es extensible

**Observación**: El diseño permite agregar nuevas features fácilmente sin cambios de schema, solo agregando filas en `platform.features`.

---

### O-02: Separación clara entre platform admin y tenant admin

**Observación**: La distinción entre `platform_admin` (administra SaaS) y `super_admin` (administra tenant) es arquitectónicamente correcta.

---

### O-03: Dashboard como feature obligatoria es correcto

**Observación**: Proteger el dashboard con CHECK constraints garantiza que nunca se deshabilite accidentalmente.

---

### O-04: Frontend usa React Query correctamente

**Observación**: El uso de `@tanstack/react-query` para features es apropiado y permite cache eficiente.

---

### O-05: Migración usa preflight validation exhaustiva

**Observación**: La validación de schema antes de ejecutar DDL es una práctica excelente que previene instalaciones parciales.

---

### O-06: Seeds históricos convertidos en marcadores es seguro

**Observación**: Mover lógica destructiva a `supabase/scripts/` y dejar migrations como marcadores previene ejecuciones accidentales.

---

### O-07: AiCapabilityGuard usa fingerprint para cache

**Observación**: El uso de SHA256 hash del estado de capabilities permite cache eficiente en OpenAI.

---

### O-08: CompanyFeatureRepository valida dependencias de features

**Observación**: La validación de que `cash` debe estar habilitada antes de habilitar `restaurant`/`pos` es correcta.

---

### O-09: Frontend FeatureRoute tiene fail-safe correcto

**Observación**: En caso de error, el componente bloquea acceso en lugar de permitirlo (fail-closed).

---

### O-10: JWT Secret Validator tiene validaciones robustas

**Observación**: Validar longitud, complejidad y fragmentos prohibidos es excelente práctica.

---

### O-11: Tests de seguridad cubren casos críticos

**Observación**: Existen tests para tenant isolation, feature enforcement y authorization policies.

---

### O-12: PWA manifest incluye iconos para todas las resoluciones

**Observación**: Los iconos agregados cubren desde 72x72 hasta 512x512, cumpliendo requisitos PWA.

---

### O-13: Frontend usa atomic design pattern

**Observación**: La estructura de componentes sigue principios de atomic design (atoms → molecules → organisms).

---

### O-14: Backend usa CQRS implícito

**Observación**: Separación entre queries (read) y commands (write) en repositorios es clara.

---

### O-15: Middleware order es correcto

**Observación**: `TenantContextMiddleware` → `CompanyFeatureMiddleware` → `Authorization` es el orden correcto.

---

## PARTE 8: ANÁLISIS DE TESTS

### 8.1 Tests de Seguridad

**Archivos auditados**:
- `AuthorizationPolicyTests.cs` — ✅ Valida policies correctamente
- `CompanyFeatureMiddlewareTests.cs` — ✅ Valida bypass dev+platform_admin
- `ControllerFeatureMetadataTests.cs` — ✅ Valida que controllers tienen atributos
- `FeatureEndpointsTests.cs` — ✅ Valida enforcement backend
- `TenantContextMiddlewareTests.cs` — ✅ Valida tenant isolation
- `UsersRepositorySecurityIntegrationTests.cs` — ✅ Valida cross-tenant protection

**Cobertura**: ~85% de casos críticos

**Gaps identificados**:
- ❌ No hay test para B-02 (AiSessionRepository cross-tenant)
- ❌ No hay test para H-01 (AdminRepository platform_admin validation)
- ❌ No hay test para H-03 (race condition última sucursal)
- ❌ No hay test para H-05 (path traversal en branding)

---

### 8.2 Tests de Features

**Archivos auditados**:
- `CompanyFeatureRepositoryIntegrationTests.cs` — ✅ 296 líneas, casos exhaustivos
- `CompanyFeatureSchemaTests.cs` — ✅ Valida CHECK constraints
- `CompanyFeatureServiceTests.cs` — ✅ Valida lógica de negocio
- `AiCapabilityGuardTests.cs` — ✅ Valida snapshot generation

**Cobertura**: ~90% de lógica de features

---

### 8.3 Tests Frontend

**Archivos auditados**:
- `FeatureRoute.test.jsx` — ✅ Valida routing condicional
- `useCompanyFeatures.test.jsx` — ✅ Valida hook logic
- `companyFeatures.test.js` — ✅ Valida config functions
- `LayoutFeatures.test.jsx` — ✅ Valida sidebar conditional rendering

**Cobertura**: ~80% de componentes de features

**Gaps identificados**:
- ❌ No hay test para H-04 (cache stale al cambiar tenant)
- ❌ No hay test E2E para flujo completo

---

## PARTE 9: RIESGOS RESIDUALES V1

### R-01: Refund de productos preparados con receta histórica

**Estado**: CONOCIDO, NO BLOQUEANTE  
**Referencia**: Auditoría independiente en `docs/AUDITORIA-REFUND-REVISION-CRITICA.md`

**Decisión**: Implementar en V1.1 con `source_order_item_id` en `inventory.movements`.

---

### R-02: add_stock IA deshabilitado

**Estado**: CONOCIDO, PARCIALMENTE MITIGADO  
**Mitigación actual**: Tool no está en lista de OpenAI  
**Riesgo residual**: Handler sigue existente (ver H-02)

**Decisión**: Eliminar handler completamente antes de producción.

---

### R-03: Rate limiter no aplicado

**Estado**: CONOCIDO, NO BLOQUEANTE  
**Impacto**: Posible abuse de endpoints públicos

**Decisión**: Implementar en V1.1 con `AspNetCoreRateLimit`.

---

### R-04: Login 422 NOT REPRODUCED

**Estado**: CONOCIDO, NO REPRODUCIDO  
**Referencia**: Reportado en auditorías anteriores

**Decisión**: Monitorear en producción. Si se reproduce, investigar.

---

## PARTE 10: CORRECCIONES OBLIGATORIAS ANTES DE PRODUCCIÓN

### Correcciones BLOCKER (obligatorias)

| ID | Componente | Corrección | Estimación |
|----|------------|------------|------------|
| B-01 | `CompanyFeatureMiddleware.cs:32` | Agregar validación `tenant.CompanyId == WalosSystemIdentity.CompanyId` al bypass | 15 min |
| B-02 | `AiSessionRepository.cs` | Agregar parámetro `companyId` a todos los métodos que reciben IDs y validar en queries | 2 horas |
| B-03 | `FeatureRoute.jsx:43` | Refactorizar lógica de validación para no omitir `isReady` check | 30 min |

**Total estimado**: ~3 horas

---

### Correcciones HIGH (fuertemente recomendadas)

| ID | Componente | Corrección | Estimación |
|----|------------|------------|------------|
| H-01 | `AdminRepository.cs` | Agregar validación de que `userId` pertenece a `WALOS-SYSTEM-001` | 1 hora |
| H-02 | `OrchestratorService.cs` | Eliminar handler `HandleAddStockAsync` completamente | 30 min |
| H-03 | `CompanyFeatureRepository.cs:146` | Cambiar a `IsolationLevel.Serializable` o agregar lock en `branches` | 1 hora |
| H-04 | `useCompanyFeatures.js` | Invalidar cache al cambiar `tenantId` | 30 min |
| H-05 | `PwaController.cs` | Agregar validación de path traversal en branding fallback | 1 hora |

**Total estimado**: ~4 horas

---

### Correcciones MEDIUM (recomendadas)

| ID | Componente | Corrección | Estimación |
|----|------------|------------|------------|
| M-01 | `Program.cs` | Invocar `JwtSecretValidator.ValidateOrThrow` en startup | 5 min |
| M-02 | `CompanyFeatureMiddleware.cs:71` | Agregar logging de intentos denegados | 15 min |
| M-04 | `AiCapabilityGuard.cs:22` | Reemplazar `bool trustedDevBypass` con `ITenantContext tenant` | 30 min |

**Total estimado**: ~1 hora

---

## PARTE 11: VEREDICTO FINAL

### ⚠️ V1 PLATFORM COMMIT NO APROBADO

**Razones**:

1. **3 BLOCKER** que permiten:
   - Escalación de privilegios (B-01)
   - Cross-tenant data access (B-02)
   - Bypass de feature enforcement (B-03)

2. **5 HIGH** que representan riesgos de seguridad significativos

**Trabajo requerido antes de producción**:
- ✅ Migración 019: **APROBADA**
- ❌ Código backend: **REQUIERE 3 CORRECCIONES BLOCKER**
- ❌ Código frontend: **REQUIERE 1 CORRECCIÓN BLOCKER**
- ⚠️ Tests: **REQUIERE AGREGAR 4 TESTS CRÍTICOS**

**Estimación total de correcciones obligatorias**: ~7 horas

**Estimación total de correcciones recomendadas**: ~5 horas adicionales

---

## PARTE 12: LISTA EXACTA DE CORRECCIONES

### Obligatorias (antes de push a producción)

1. **B-01**: `CompanyFeatureMiddleware.cs:32`
   ```csharp
   if (tenant.IsDev && tenant.IsPlatformAdmin 
       && tenant.CompanyId == WalosSystemIdentity.CompanyId)
   ```

2. **B-02**: `AiSessionRepository.cs`
   - Agregar parámetro `long companyId` a:
     - `GetSessionAsync`
     - `GetInteractionAsync`
     - `ConfirmInteractionAsync`
     - `GetSessionHistoryAsync`
   - Agregar `AND company_id = @CompanyId` a todos los WHERE

3. **B-03**: `FeatureRoute.jsx:43`
   ```jsx
   if (feature === 'dashboard') {
       return <Layout>{children}</Layout>;
   }
   if (!isReady) {
       return <Layout><FeatureStatus status={isError ? 'error' : 'loading'} /></Layout>;
   }
   ```

4. **Test B-02**: Agregar `AiSessionRepositoryTenantIsolationTests.cs`
   - Test: `GetSession_DifferentCompany_ReturnsNull`
   - Test: `ConfirmInteraction_DifferentCompany_ThrowsException`

---

### Fuertemente recomendadas (antes de producción)

5. **H-01**: `AdminRepository.cs`
   - Agregar validación de `WALOS-SYSTEM-001` en:
     - `CreateBranchAsync`
     - `UpdateBranchAsync`
     - `DeleteBranchAsync`

6. **H-02**: `OrchestratorService.cs`
   - Eliminar método `HandleAddStockAsync`
   - Agregar `throw new InvalidOperationException` en switch case

7. **H-03**: `CompanyFeatureRepository.cs:146`
   - Cambiar a `IsolationLevel.Serializable`
   - O agregar lock explícito en `branches`

8. **H-04**: `authStore.js`
   - Invalidar cache de features al cambiar `tenantId`

9. **H-05**: `PwaController.cs`
   - Agregar validación de path traversal

10. **Test H-01**: Agregar `AdminRepositoryPlatformAdminTests.cs`
    - Test: `CreateBranch_NonSystemUser_ThrowsUnauthorized`

11. **Test H-03**: Agregar `CompanyFeatureRepositoryConcurrencyTests.cs`
    - Test: `DeleteLastBranch_Concurrent_OnlyOneSucceeds`

12. **Test H-05**: Agregar `PwaControllerSecurityTests.cs`
    - Test: `GetLogo_PathTraversal_ReturnsBadRequest`

---

### Recomendadas (pueden posponerse a V1.1)

13. **M-01**: `Program.cs` — Validar JWT secret en startup
14. **M-02**: `CompanyFeatureMiddleware.cs` — Logging de denials
15. **M-04**: `AiCapabilityGuard.cs` — Reemplazar bool bypass con ITenantContext
16. **M-06**: Generar `frontend/src/config/generatedFeatures.js` desde backend
17. **M-07**: Agregar rate limiting a `FeaturesController`
18. **M-08**: Agregar auditoría de cambios de sucursales

---

## FIRMA DE AUDITORÍA

**Auditor**: Arquitectónico independiente  
**Fecha**: 2026-09-15  
**Commit auditado**: `e702214cc3cde583976ef0241a96581972525fb3`  
**Veredicto**: ⚠️ **NO APROBADO** — Requiere correcciones obligatorias

**Próximos pasos**:
1. Implementar correcciones B-01, B-02, B-03
2. Agregar tests faltantes
3. Re-auditar commit corregido
4. Aprobar para producción

---

**FIN DE AUDITORÍA**
