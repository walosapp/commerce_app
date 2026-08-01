# Brief de Deuda Técnica — Walos

> **Fecha**: Agosto 2026
> **Fuente**: Auditoría de código verificada contra el repositorio actual.
> **Propósito**: Documento operativo para ejecutar remediación de deuda técnica.
> **Regla**: Si algo de este archivo contradice al código, manda el código.

---

## Contexto

Walos es un sistema de gestión comercial (POS/inventario/finanzas) con asistente IA, construido con ASP.NET Core 8 + React 18 + Supabase (PostgreSQL). La auditoría integral de Mayo 2026 detectó hallazgos en 7 fases. Los **P0 de seguridad ya fueron corregidos**. Este brief consolida la deuda P1/P2 vigente, verificada contra el código actual.

### Lo que YA se corrigió

| Hallazgo | Corrección verificada |
|---|---|
| `PlatformAdminController` sin restricción de rol | Ahora tiene `[Authorize(Roles = "dev")]` |
| `X-Branch-ID` override inseguro desde header | Branch ahora solo viene del JWT claim |
| `first_name`/`last_name` inconsistente en frontend | Eliminado; todo usa `firstName`/`lastName` |
| Sin tests de seguridad críticos | Agregados `PlatformAdminControllerSecurityTests`, `TenantContextMiddlewareTests`, `CatalogControllerSecurityTests` |
| Tests de integración limitados | 14 archivos de integración cubriendo repos principales |

---

## Inventario completo de deuda técnica

### DT-01 — DI con ownership duplicado

- **Prioridad**: P1
- **Capa**: Backend / Arquitectura
- **Esfuerzo**: Bajo (< 1 hora)
- **Riesgo si no se corrige**: El último registro en DI gana silenciosamente; comportamiento inesperado si las implementaciones divergen.

**Problema**: `AddInfrastructure()` registra 6 application services que ya están registrados en `AddApplication()`:

```
// backend-dotnet/src/Walos.Infrastructure/DependencyInjection.cs:36-41
services.AddScoped<IAuthService, AuthService>();
services.AddScoped<ISalesService, SalesService>();
services.AddScoped<ICreditService, CreditService>();
services.AddScoped<ICashRegisterService, CashRegisterService>();
services.AddScoped<IRefundService, RefundService>();
services.AddScoped<IFinanceService, FinanceService>();
```

Esos mismos servicios ya están en:

```
// backend-dotnet/src/Walos.Application/DependencyInjection.cs:12-18
services.AddScoped<IAuthService, AuthService>();
services.AddScoped<ISalesService, SalesService>();
services.AddScoped<ICreditService, CreditService>();
services.AddScoped<IFinanceService, FinanceService>();
// etc.
```

**Corrección**: Eliminar los registros de application services de `AddInfrastructure()`. Dejar ahí solo repos, infra services y hosted services. Principio: `AddApplication()` registra services de negocio; `AddInfrastructure()` registra implementaciones de infra.

**Archivos a tocar**:
- `backend-dotnet/src/Walos.Infrastructure/DependencyInjection.cs`
- `backend-dotnet/src/Walos.Application/DependencyInjection.cs` (verificar que tenga todos los que se eliminan del otro)

**Validación**: `dotnet build` + `dotnet test` deben pasar.

---

### DT-02 — InventoryController sobrecargado (561 líneas)

- **Prioridad**: P1
- **Capa**: Backend / Arquitectura
- **Esfuerzo**: Alto (4-6 horas)
- **Riesgo si no se corrige**: SRP violado, difícil testear en aislamiento, cualquier cambio en inventario tiene alto riesgo de regresión.

**Problema**: Un solo controller mezcla 14 responsabilidades:

| Acción | Líneas aprox | Complejidad |
|---|---|---|
| CRUD Productos (Get, GetById, Create, Update, Delete) | ~150 | Media — validación inline, mapeo manual de entidad |
| Upload de imagen | ~35 | Media — filesystem + content type validation |
| Import/Export Excel | ~100 | Alta — parsing, mapeo categorías/unidades, stock inicial |
| Stock (Get, GetLow, AddStock) | ~90 | Alta — costo promedio ponderado, movimientos |
| AI (Process, Confirm) | ~30 | Baja — delega a service |
| Alertas | ~10 | Baja |
| Reportes de ganancias | ~25 | Media — agregaciones |
| Categorías y Unidades | ~20 | Baja |

Además inyecta 5 dependencias: `IInventoryRepository`, `IInventoryService`, `ITenantContext`, `ILogger`, `ProductExcelService`.

**Corrección sugerida** (dividir en sub-controllers o extraer a services):

1. **Opción A — Sub-controllers**: `ProductsController`, `StockController`, `InventoryReportsController`; mantener AI en `AiController` existente
2. **Opción B — Service layer**: Mover lógica de Create/Update/AddStock a `IInventoryService`, que el controller solo delegue

La opción B es preferible porque ya existe `IInventoryService` con algo de lógica; se trata de migrar más casos de uso ahí.

**Archivos a tocar**:
- `backend-dotnet/src/Walos.API/Controllers/InventoryController.cs`
- `backend-dotnet/src/Walos.Application/Services/IInventoryService.cs`
- `backend-dotnet/src/Walos.Application/Services/InventoryService.cs`
- Posiblemente crear sub-controllers en `Controllers/`

**Validación**: `dotnet build` + `dotnet test` + verificar manualmente CRUD producto, add stock, import Excel.

---

### DT-03 — Interfaces de repositorio en capa equivocada

- **Prioridad**: P1
- **Capa**: Backend / Arquitectura
- **Esfuerzo**: Medio (1-2 horas)
- **Riesgo si no se corrige**: Boundary inconsistente; al buscar un contrato, no sabés si está en Domain o Application.

**Problema**: 7 interfaces de repositorio viven en `Walos.Application.Services` en lugar de `Walos.Domain.Interfaces`:

| Interface | Ubicación actual (incorrecta) |
|---|---|
| `IAdminRepository` | `Walos.Application/Services/IAdminRepository.cs` |
| `ICatalogRepository` | `Walos.Application/Services/ICatalogRepository.cs` |
| `IDeliveryRepository` | `Walos.Application/Services/IDeliveryRepository.cs` |
| `IPurchaseOrderRepository` | `Walos.Application/Services/IPurchaseOrderRepository.cs` |
| `IRecipeRepository` | `Walos.Application/Services/IRecipeRepository.cs` |
| `ISuppliersRepository` | `Walos.Application/Services/ISuppliersRepository.cs` |
| `IUsersRepository` | `Walos.Application/Services/IUsersRepository.cs` |

Las otras 14 interfaces de repositorio ya están correctamente en `Walos.Domain.Interfaces`.

**Corrección**: Mover los 7 archivos a `Walos.Domain/Interfaces/`, actualizar `namespace` a `Walos.Domain.Interfaces` y ajustar `using` en consumers.

**Archivos a tocar**:
- 7 archivos de interface (mover + cambiar namespace)
- Controllers y services que los consumen (actualizar `using`)
- `DependencyInjection.cs` de Infrastructure (puede necesitar using)

**Validación**: `dotnet build` + `dotnet test`

---

### DT-04 — Controllers repository-driven (sin service layer)

- **Prioridad**: P1
- **Capa**: Backend / Arquitectura
- **Esfuerzo**: Alto (6-8 horas total, divisible por controller)
- **Riesgo si no se corrige**: Dos filosofías arquitectónicas conviven; cada feature nueva puede seguir cualquier dirección.

**Problema**: 5 controllers inyectan repositorios directamente y operan sin service layer:

| Controller | Inyecta | Líneas |
|---|---|---|
| `SuppliersController` | `ISuppliersRepository` | 141 |
| `CatalogController` | `ICatalogRepository` | 149 |
| `UsersController` | `IUsersRepository` | 129 |
| `RecipesController` | `IRecipeRepository` + `IInventoryRepository` | 112 |
| `PurchaseOrdersController` | `IPurchaseOrderRepository` | 77 |

Mientras que controllers maduros (`Finance`, `Sales`, `Delivery`, `Credit`, `CashRegister`, `Refund`) sí delegan a application services.

**Corrección**: Crear application services para cada módulo y migrar lógica de controller. Priorizar por complejidad:
1. `RecipesController` (usa 2 repos, tiene lógica de recálculo de costo)
2. `UsersController` (tiene hash de password y validaciones)
3. `SuppliersController` (CRUD directo)
4. `CatalogController` (CRUD directo)
5. `PurchaseOrdersController` (CRUD directo)

**Archivos a crear**:
- `Walos.Application/Services/IRecipeService.cs` + `RecipeService.cs`
- `Walos.Application/Services/IUsersService.cs` + `UsersService.cs`
- `Walos.Application/Services/ISuppliersService.cs` + `SuppliersService.cs`
- (Catalog y PurchaseOrders pueden esperar por ser CRUD simple)

**Validación**: `dotnet build` + `dotnet test` + verificar funcionalidad de cada módulo.

---

### DT-05 — `userService` admin usa endpoints incorrectos

- **Prioridad**: P1
- **Capa**: Frontend / Bug funcional
- **Esfuerzo**: Bajo (15 minutos)
- **Riesgo si no se corrige**: Bug funcional real — admin update/delete opera sobre el tenant del JWT en vez del tenant objetivo.

**Problema**:

```javascript
// frontend/src/services/userService.js:16
adminUpdate: (id, companyId, data)  => api.put(`/users/${id}`, data).then(r => r.data),
// frontend/src/services/userService.js:19
adminDelete: (id, companyId)        => api.delete(`/users/${id}`).then(r => r.data),
```

Ambos reciben `companyId` como parámetro pero lo ignoran. Deberían usar los endpoints admin:

```javascript
adminUpdate: (id, companyId, data)  => api.put(`/admin/users/${id}`, data, { params: { companyId } }).then(r => r.data),
adminDelete: (id, companyId)        => api.delete(`/admin/users/${id}`, { params: { companyId } }).then(r => r.data),
```

**Archivo a tocar**: `frontend/src/services/userService.js`

**Validación**: Verificar manualmente en modo admin que update/delete apunte al endpoint correcto.

---

### DT-06 — Componentes que llaman `api` directo (saltando services)

- **Prioridad**: P1
- **Capa**: Frontend / Consistencia
- **Esfuerzo**: Medio (1-2 horas)
- **Riesgo si no se corrige**: Contratos dispersos; si cambia un endpoint, hay que buscar en componentes además de services.

**Problema**: 4 componentes importan `api` directamente y definen mini-servicios locales:

| Componente | Qué hace directo |
|---|---|
| `modules/inventory/components/ImportProductsModal.jsx` | Descarga plantilla Excel con `api.get(... blob)` |
| `modules/sales/components/CreditsPanel.jsx` | Define `creditService` local con 5 métodos |
| `modules/sales/components/OrderItemsList.jsx` | Define `getOrderItems()` local |
| `modules/sales/components/SalesSummaryTab.jsx` | Define `salesSummaryService` local con 3 métodos |

**Corrección**:
1. Mover métodos de `CreditsPanel` a `services/creditService.js` (ya existe `services/refundService.js` como referencia)
2. Mover `getOrderItems` a `services/salesService.js`
3. Mover `salesSummaryService` a `services/salesService.js`
4. Mover descarga de template a `services/inventoryService.js`
5. Actualizar imports en los 4 componentes

**Archivos a tocar**:
- `frontend/src/services/salesService.js` (agregar métodos)
- `frontend/src/services/inventoryService.js` (agregar descarga template)
- Crear `frontend/src/services/creditService.js` si no existe (verificar — actualmente no hay archivo dedicado de créditos)
- Los 4 componentes listados (cambiar imports)

**Validación**: Verificar que ventas, créditos e importación de productos sigan funcionando.

---

### DT-07 — Header `X-Company-ID` muerto

- **Prioridad**: P2
- **Capa**: Frontend / Limpieza contractual
- **Esfuerzo**: Bajo (5 minutos)
- **Riesgo si no se corrige**: Confusión contractual; alguien puede asumir que el backend consume ese header.

**Problema**:

```javascript
// frontend/src/config/api.js:33-35
if (state?.tenantId) {
  config.headers['X-Company-ID'] = state.tenantId;
}
```

El backend (`TenantContextMiddleware`) no lee `X-Company-ID`. El `companyId` siempre viene del JWT claim.

**Corrección**: Eliminar las líneas 33-35 de `api.js`.

**Archivo a tocar**: `frontend/src/config/api.js`

**Validación**: Login + navegar por módulos principales. El header no afecta funcionalidad.

---

### DT-08 — Routing de settings duplicado

- **Prioridad**: P2
- **Capa**: Frontend / Mantenibilidad
- **Esfuerzo**: Bajo (30 minutos)
- **Riesgo si no se corrige**: Ruido en `App.jsx`; cada nueva sección de settings requiere agregar otra ruta manual.

**Problema**: 7 rutas en `App.jsx` renderizan el mismo `SettingsPage`:

```jsx
// frontend/src/App.jsx:82-89
<Route path="/settings" element={...}><SettingsPage /></...>
<Route path="/settings/branding" element={...}><SettingsPage /></...>
<Route path="/settings/themes" element={...}><SettingsPage /></...>
<Route path="/settings/discounts" element={...}><SettingsPage /></...>
<Route path="/settings/catalog" element={...}><SettingsPage /></...>
<Route path="/settings/plan" element={...}><SettingsPage /></...>
<Route path="/settings/ai" element={...}><SettingsPage /></...>
<Route path="/settings/payments" element={...}><SettingsPage /></...>
```

**Corrección**: Usar un wildcard route:

```jsx
<Route path="/settings/*" element={<ProtectedRoute><SettingsPage /></ProtectedRoute>} />
```

Si `/settings/catalog` necesita roles especiales, manejar eso dentro de `SettingsPage` o con un guard interno.

**Archivos a tocar**: `frontend/src/App.jsx`

**Validación**: Navegar por todas las secciones de settings.

---

### DT-09 — `AdminUsersPage` huérfano

- **Prioridad**: P2
- **Capa**: Frontend / Limpieza
- **Esfuerzo**: Bajo (10 minutos)
- **Riesgo si no se corrige**: 228 líneas de código muerto; confusión sobre si es funcionalidad activa.

**Problema**: `frontend/src/modules/admin/AdminUsersPage.jsx` existe (228 líneas) con un componente funcional completo, pero **no está importado ni referenciado en `App.jsx`**. No tiene ruta.

**Corrección**: Dos opciones:
1. **Eliminar** si la funcionalidad ya está cubierta por `UsersPage` con modo admin
2. **Conectar** si es funcionalidad necesaria para superadmin cross-tenant

**Decisión requerida**: Verificar si `UsersPage` ya cubre el caso admin cross-tenant antes de eliminar.

**Archivo a tocar**: `frontend/src/modules/admin/AdminUsersPage.jsx` (eliminar) o `App.jsx` (agregar ruta)

---

### DT-10 — Credenciales dev hardcodeadas en UI de login

- **Prioridad**: P2
- **Capa**: Frontend / Seguridad
- **Esfuerzo**: Bajo (5 minutos)
- **Riesgo si no se corrige**: Credenciales visibles en producción.

**Problema**:

```jsx
// frontend/src/modules/auth/LoginPage.jsx:129-132
<div className="mt-6 rounded-lg bg-gray-50 p-3 text-center text-xs text-gray-400">
  <p className="font-medium text-gray-500">Credenciales de desarrollo</p>
  <p className="mt-1">Usuario: <span>admin@mibar.com</span> | Contraseña: <span>admin123</span></p>
</div>
```

**Corrección**: Condicionar a entorno dev:

```jsx
{import.meta.env.DEV && (
  <div className="mt-6 ...">
    ...
  </div>
)}
```

**Archivo a tocar**: `frontend/src/modules/auth/LoginPage.jsx`

**Validación**: Verificar que aparezca en `npm run dev` y no en `npm run build` + preview.

---

### DT-11 — Cobertura de tests frontend muy angosta

- **Prioridad**: P1
- **Capa**: Frontend / Testing
- **Esfuerzo**: Alto (8-12 horas)
- **Riesgo si no se corrige**: Regresiones silenciosas en módulos core.

**Problema**: Solo 3 unit tests y 3 E2E specs:

**Unit/Integration** (en `frontend/src/test/`):
- `modules/auth/LoginPage.test.jsx`
- `stores/authStore.test.js`
- `utils/formatters.test.js`

**E2E** (en `frontend/e2e/`):
- `auth.spec.js`
- `navigation.spec.js`
- `sales.spec.js`

**Sin cobertura**: inventory, suppliers, users, settings, finance, delivery, dashboard, credits, cash register.

**Componentes con >300 líneas sin tests**:

| Componente | Líneas |
|---|---|
| `ProductFormModal.jsx` | 623 |
| `InvoicePanel.jsx` | 550 |
| `SalesPage.jsx` | 477 |
| `SuppliersPage.jsx` | 425 |
| `CatalogSettings.jsx` | 377 |
| `MonthInitPanelModal.jsx` | 376 |
| `CreateDeliveryOrderPanel.jsx` | 374 |
| `UsersPage.jsx` | 328 |

**Corrección sugerida por prioridad**:

1. **Crítico**: Tests para `InventoryPage` (CRUD productos, add stock)
2. **Crítico**: Tests para `SalesPage` (mesas, facturación)
3. **Alto**: Tests para `UsersPage` (CRUD, roles, permisos)
4. **Alto**: Tests para `FinancePage` (entradas, resumen)
5. **Medio**: Tests para `SuppliersPage`, `SettingsPage`, `DeliveryOrdersPage`

---

### DT-12 — Inconsistencia de estilo en services

- **Prioridad**: P2
- **Capa**: Frontend / Consistencia
- **Esfuerzo**: Medio (1 hora)
- **Riesgo si no se corrige**: Dos convenciones conviven; cada nuevo service puede seguir cualquiera.

**Problema**: `inventoryService` usa `async/await`:

```javascript
getProducts: async (filters = {}) => {
  const response = await api.get('/inventory/products', { params: filters });
  return response.data;
},
```

Los demás usan `.then()`:

```javascript
getOrders: (params = {}) => api.get('/delivery/orders', { params }).then(r => r.data),
```

**Corrección**: Unificar a un solo estilo. Recomendación: `.then(r => r.data)` es más conciso para wrappers simples; `async/await` para lógica más compleja. Elegir uno y aplicar a todos los 17 archivos de `services/`.

**Archivos a tocar**: Todos los archivos en `frontend/src/services/`

---

### DT-13 — Sin estrategia de validación unificada en backend

- **Prioridad**: P2
- **Capa**: Backend / Consistencia
- **Esfuerzo**: Alto (6+ horas)
- **Riesgo si no se corrige**: Reglas de validación duplicadas/dispersas en controllers.

**Problema**: Solo existen 2 validators con FluentValidation:
- `CreateProductValidator`
- `AiInputValidator`

El resto de validaciones son inline en controllers (`if string.IsNullOrWhiteSpace... return BadRequest`). Ejemplo en `InventoryController.CreateProduct()`:

```csharp
if (string.IsNullOrWhiteSpace(request.Name))
    return BadRequest(ApiResponse.Fail("El nombre del producto es requerido"));
if (request.CategoryId <= 0)
    return BadRequest(ApiResponse.Fail("Selecciona una categoria valida..."));
```

**Corrección**: No se necesita migrar todo de golpe. Estrategia incremental:
1. Cada nuevo endpoint usa FluentValidation
2. Al tocar un controller existente, migrar sus validaciones a validators
3. Crear validators para los DTOs más usados: `CreateFinancialEntryRequest`, `CreateUserRequest`, `CreateTableRequest`, `InvoiceTableRequest`

---

### DT-14 — Documentación desactualizada

- **Prioridad**: P1 (READMEs) / P2 (docs internos)
- **Capa**: Documentación
- **Esfuerzo**: Medio (3-4 horas total)
- **Riesgo si no se corrige**: Onboarding incorrecto; decisiones basadas en información falsa.

| Documento | Problema | Prioridad |
|---|---|---|
| `backend-dotnet/README.md` | Menciona SQL Server, `X-Tenant-ID`, solo auth+inventory | P1 |
| `frontend/README.md` | Marca auth/ventas/proveedores como "próximamente" | P1 |
| `docs/architecture.md` | Afirma todos los controllers son thin; marca frontend caja como pendiente | P1 |
| `plan.md` | Marca Fase 1 como activa (ya completada) | P2 |
| `docs/pending-credit-module.md` | Nombre dice "pending" pero contenido es implementación completada | P2 |
| `docs/pending-billing-ai-keys.md` | Mismo problema de nomenclatura | P2 |
| `docs/CODE_AUDIT_REPORT.md` | Artefacto histórico sin framing claro | P2 |

---

## Orden de ejecución recomendado

### Bloque 1 — Quick wins (< 2 horas total)

| # | Item | Esfuerzo |
|---|---|---|
| 1 | DT-05: Corregir `userService` admin endpoints | 15 min |
| 2 | DT-07: Eliminar header `X-Company-ID` muerto | 5 min |
| 3 | DT-10: Condicionar dev credentials a `import.meta.env.DEV` | 5 min |
| 4 | DT-01: Limpiar DI duplicado | 30 min |
| 5 | DT-09: Decidir destino de `AdminUsersPage` | 10 min |
| 6 | DT-08: Simplificar routing settings | 30 min |

### Bloque 2 — Consistencia frontend (2-3 horas)

| # | Item | Esfuerzo |
|---|---|---|
| 7 | DT-06: Mover api calls directos a services | 1-2 horas |
| 8 | DT-12: Unificar estilo de services | 1 hora |

### Bloque 3 — Arquitectura backend (8-16 horas, divisible)

| # | Item | Esfuerzo |
|---|---|---|
| 9 | DT-03: Mover interfaces a Domain | 1-2 horas |
| 10 | DT-02: Reducir InventoryController | 4-6 horas |
| 11 | DT-04: Crear services para controllers repo-driven | 6-8 horas |
| 12 | DT-13: Ampliar validators (incremental) | Ongoing |

### Bloque 4 — Testing y docs (12-16 horas, divisible)

| # | Item | Esfuerzo |
|---|---|---|
| 13 | DT-11: Ampliar test coverage frontend | 8-12 horas |
| 14 | DT-14: Actualizar READMEs y docs | 3-4 horas |

---

## Criterio de cierre

Este brief se considera resuelto cuando:

1. Todos los items de Bloque 1 estén completos
2. La capa `services/` del frontend sea la única que toque `api`
3. El DI no tenga registros duplicados
4. Los controllers más usados tengan service layer
5. Los módulos core del frontend tengan al menos un test
6. Los READMEs principales reflejen el estado real del sistema

---

## Regla de uso

- Si un item se completa, marcarlo con `[x]` y fecha
- Si se descarta, documentar el motivo
- Si aparece deuda nueva durante la corrección, agregarla al final con su código `DT-XX`
- Verificación mínima por item: `dotnet build` + `dotnet test` (backend) o `npm run build` + `npm test` (frontend)
