# Diagnóstico de Auditoría — Walos

> Documento vivo de auditoría.  
> Objetivo: registrar hallazgos con evidencia, priorizarlos y mapear futuras correcciones.

---

## Estado del documento

- **Proyecto**: Walos
- **Inicio de auditoría**: 2026-05-06
- **Estado actual**: Fase 1 completada
- **Fuente de planificación**: `plan.md`

---

## Cómo usar este documento

Cada hallazgo debe tener:

- código identificador
- fase
- severidad
- evidencia concreta
- impacto técnico
- acción correctiva
- estado

Estados posibles:

- `pendiente`
- `validado`
- `corrigiendo`
- `corregido`
- `descartado`

---

## Resumen ejecutivo

Walos ya muestra una base arquitectónica SERIA: frontend modular por dominio, backend en capas, multi-tenant real, testing disponible y superficie funcional amplia.  
El problema principal en esta etapa NO es “falta de sistema”, sino **riesgo de desalineación entre arquitectura declarada, crecimiento del producto y documentación operativa**.

En Fase 1, los hallazgos más relevantes son:

1. La arquitectura general existe y es coherente.
2. El punto más sensible transversalmente es el aislamiento multi-tenant.
3. La documentación actual NO puede asumirse como fuente de verdad única.
4. El frontend ya tiene suficiente tamaño como para exigir vigilancia de modularidad.

---

# Fase 1 — Mapa arquitectónico

## Objetivo

Entender cómo está construido Walos hoy, cuáles son sus boundaries reales y qué zonas merecen auditoría profunda en fases posteriores.

## Resultado

**Fase 1 completada** con evidencia de código y estructura del repositorio.

---

## Arquitectura actual verificada

### Backend

Estructura principal:

- `backend-dotnet/src/Walos.API`
- `backend-dotnet/src/Walos.Application`
- `backend-dotnet/src/Walos.Domain`
- `backend-dotnet/src/Walos.Infrastructure`

Esto confirma una separación compatible con Clean Architecture:

- **API**: controllers, middleware, bootstrap
- **Application**: services, DTOs, validaciones
- **Domain**: entidades, interfaces, contratos de negocio
- **Infrastructure**: repositorios, conexión DB, servicios externos

### Frontend

Estructura principal:

- `frontend/src/modules/*` por dominio
- `frontend/src/services/*` como capa de acceso HTTP
- `frontend/src/stores/*` para estado global
- `frontend/src/config/api.js` como punto central de auth/headers

Los módulos detectados incluyen:

- `inventory`
- `sales`
- `finance`
- `delivery`
- `suppliers`
- `settings`
- `alerts`
- `dashboard`
- `auth`
- `users`
- `admin`

### Infraestructura transversal

- Auth JWT
- `TenantContextMiddleware`
- React Query para server state
- Zustand persist para sesión/UI
- PWA con Workbox
- PostgreSQL/Supabase

---

## Mapa de flujo de alto nivel

### Backend

```text
HTTP Request
→ Controller
→ Application Service
→ Repository / External Service
→ PostgreSQL / OpenAI / otros servicios
```

### Frontend

```text
Route/Page
→ módulo de negocio
→ service
→ API client central (`config/api.js`)
→ backend
```

### Multi-tenant

```text
JWT claims (companyId, userId, branchId)
→ TenantContextMiddleware
→ ITenantContext scoped
→ controllers / services / repositories
```

---

## Zonas críticas detectadas

### 1. Multi-tenant y aislamiento

Es la zona MÁS delicada del sistema.

Motivo:

- cruza auth, middleware, controllers, repositorios y queries
- cualquier inconsistencia acá impacta seguridad y datos entre tenants
- el frontend además participa enviando headers de contexto

### 2. Escalabilidad del frontend

El frontend ya dejó de ser chico.

Motivo:

- muchos módulos activos
- páginas de negocio con bastante responsabilidad
- riesgo de crecimiento desordenado en rutas, servicios y estado compartido

### 3. Coherencia entre arquitectura declarada y arquitectura real

La arquitectura “se ve bien” en macro, pero eso NO prueba que todas las capas respeten sus límites internamente.

Motivo:

- hay que validar en Fase 2 si controllers siguen siendo thin
- hay que verificar si los repositorios y services mantienen responsabilidades limpias

### 4. Documentación y backlog

Hay evidencia de desalineación documental.

Motivo:

- `PENDING.md` contiene secciones viejas que ya no reflejan el código
- documentos de arquitectura/auditoría previos pueden estar parcialmente stale

---

## Hallazgos de Fase 1

| ID | Severidad | Hallazgo | Evidencia | Impacto | Acción propuesta | Estado |
|----|-----------|----------|-----------|---------|------------------|--------|
| F1-01 | P1 | La documentación operativa no refleja completamente el estado real del código | `PENDING.md` contradice módulos ya presentes en `frontend/src/modules/*` y `backend-dotnet/src/Walos.API/Controllers/*` | Riesgo de planificar mal, repetir trabajo o tomar backlog incorrecto como fuente de verdad | Auditar y sanear documentación en Fase 7 | validado |
| F1-02 | P0 | El aislamiento multi-tenant es el eje técnico más sensible del sistema | `TenantContextMiddleware.cs`, `authStore.js`, `config/api.js`, controllers autenticados | Un error acá puede romper aislamiento entre empresas/sucursales | Prioridad alta para Fase 4 | validado |
| F1-03 | P1 | El frontend ya tiene complejidad suficiente para exigir control arquitectónico | `App.jsx`, cantidad de módulos, tamaño del módulo `sales`, organización de `services/` y `stores/` | Riesgo de acoplamiento, páginas gordas y contratos frágiles | Auditar modularidad en Fase 3 | validado |
| F1-04 | P1 | Existe arquitectura por capas declarada, pero todavía hay que verificar su cumplimiento real | Estructura `API -> Application -> Domain <- Infrastructure` observada en el repo | El diagrama puede ser correcto mientras las responsabilidades reales estén mezcladas | Profundizar en Fase 2 | validado |
| F1-05 | P2 | Hay documentación técnica previa que puede contener diagnóstico viejo | `docs/architecture.md`, `docs/CODE_AUDIT_REPORT.md` deben releerse contra código actual antes de usarse | Riesgo de repetir errores ya corregidos o perseguir problemas inexistentes | Revalidar esos docs antes de tomarlos como referencia | pendiente |

---

## Evidencia consolidada de Fase 1

### Estructura backend verificada

- `backend-dotnet/src/Walos.API/Program.cs`
- `backend-dotnet/src/Walos.API/Middleware/TenantContextMiddleware.cs`
- `backend-dotnet/src/Walos.API/Controllers/*.cs`
- `backend-dotnet/src/Walos.Application/Services/*.cs`
- `backend-dotnet/src/Walos.Infrastructure/Repositories/*.cs`

### Estructura frontend verificada

- `frontend/src/App.jsx`
- `frontend/src/config/api.js`
- `frontend/src/stores/authStore.js`
- `frontend/src/modules/inventory/InventoryPage.jsx`
- `frontend/src/modules/sales/SalesPage.jsx`
- `frontend/src/modules/dashboard/DashboardPage.jsx`

### Tooling verificado

- `frontend/package.json` → Vitest, Playwright, ESLint
- `backend-dotnet/tests/Walos.Tests/Walos.Tests.csproj` → xUnit, Moq, coverlet
- `frontend/vite.config.js` → PWA + proxy API

---

## Mapa de correcciones futuras

> Esta tabla es para convertir diagnóstico en ejecución sin perder trazabilidad.

| Ref | Nace de | Corrección futura | Fase sugerida | Prioridad | Estado |
|-----|---------|-------------------|---------------|-----------|--------|
| C-01 | F1-01 | Sanear `PENDING.md` y contrastarlo con código real | Fase 7 | P1 | pendiente |
| C-02 | F1-02 | Auditar claims, headers, middleware y filtros tenant en endpoints/repositorios | Fase 4 | P0 | pendiente |
| C-03 | F1-03 | Revisar páginas grandes, boundaries de módulos y uso de estado compartido | Fase 3 | P1 | pendiente |
| C-04 | F1-04 | Verificar si controllers y services respetan límites de capa | Fase 2 | P1 | pendiente |
| C-05 | F1-05 | Revalidar docs históricos antes de reutilizarlos como evidencia | Fase 7 | P2 | pendiente |

---

## Decisiones tomadas durante la auditoría

| Fecha | Decisión | Motivo |
|------|----------|--------|
| 2026-05-06 | Usar `plan.md` como guía oficial del recorrido | Evitar una revisión difusa y sin cierre |
| 2026-05-06 | Centralizar el diagnóstico en este archivo | Poder mapear errores, correcciones y prioridades desde un único lugar |

---

## Siguiente paso

Pasar a **Fase 2 — Auditoría backend**, con foco en:

1. controllers
2. services
3. repositories
4. composición de dependencias
5. cumplimiento real de la separación por capas

---

## Key Learnings

1. Walos ya tiene tamaño suficiente como para auditarse como sistema, no como feature aislada.
2. El valor de Fase 1 fue ubicar las zonas sensibles antes de empezar a corregir cosas a ciegas.
3. La documentación previa del repo necesita revalidación constante contra código real.

---

# Fase 2 — Auditoría backend

## Objetivo

Validar si la arquitectura backend declarada se cumple en el código REAL: controllers, services, repositories, validaciones y composición de dependencias.

## Resultado

**Fase 2 completada** con revisión directa de controllers, services, middleware y DI.

---

## Diagnóstico general

El backend de Walos NO está mal armado. De hecho, se nota evolución real:

- hay servicios de aplicación importantes (`SalesService`, `FinanceService`, `CompanyService`, `DeliveryService`, `AuthService`)
- hay middleware transversal claro
- hay DTOs y respuestas estandarizadas
- hay separación física por capas

PERO eso no significa que la arquitectura esté completamente consistente.

La conclusión honesta es esta:

> **el backend tiene una base buena, pero conviven dos estilos arquitectónicos al mismo tiempo**:
>
> 1. módulos más maduros, orientados a service layer  
> 2. módulos todavía repository-driven desde controller

Eso genera una arquitectura mixta. Funciona, sí. Pero a medida que el sistema crece, te empieza a cobrar mantenimiento.

---

## Lo que está BIEN

### 1. Hay service layer real en módulos críticos

Se verificó uso claro de servicios en:

- `AuthController` → `IAuthService`
- `FinanceController` → `IFinanceService`
- `CompanyController` → `ICompanyService`
- `DeliveryController` → `IDeliveryService`
- `CreditController` → `ICreditService`
- `CashRegisterController` → `ICashRegisterService`
- `RefundController` → `IRefundService`

Eso es una MEJORA importante respecto a arquitecturas donde el controller hace todo.

### 2. El middleware transversal está bien ubicado

Archivos verificados:

- `backend-dotnet/src/Walos.API/Program.cs`
- `backend-dotnet/src/Walos.API/Middleware/TenantContextMiddleware.cs`
- `backend-dotnet/src/Walos.API/Middleware/ExceptionHandlingMiddleware.cs`

La responsabilidad transversal está donde debe estar: fuera de controllers.

### 3. Parte de la deuda vieja ya fue corregida

Ejemplo concreto:

- la auditoría vieja hablaba de un `FinanceRepository` gigante
- hoy el repo está dividido en parciales como:
  - `FinanceRepository.Categories.cs`
  - `FinanceRepository.Entries.cs`

O sea: IMPORTANTE NO repetir diagnósticos viejos sin verificar. El código YA cambió.

---

## Hallazgos de Fase 2

| ID | Severidad | Hallazgo | Evidencia | Impacto | Acción propuesta | Estado |
|----|-----------|----------|-----------|---------|------------------|--------|
| F2-01 | P1 | La capa API no es consistente: varios controllers siguen accediendo repositorios directo | `InventoryController.cs`, `SuppliersController.cs`, `CatalogController.cs`, `PurchaseOrdersController.cs`, `RecipesController.cs`, `UsersController.cs`, `PlatformController.cs`, partes de `AdminController.cs` | Rompe el criterio de controller thin y reparte reglas de negocio/validación en demasiados lugares | Migrar gradualmente esos módulos a application services dedicados | validado |
| F2-02 | P1 | `InventoryController` está sobrecargado y mezcla HTTP, validación, filesystem, persistencia y reglas de negocio | `InventoryController.cs` (~470 líneas), uso masivo de `_repository` y validaciones inline | Alto costo de mantenimiento, más riesgo de bugs y difícil testeo aislado | Extraer casos de uso a `IInventoryService` o servicios específicos de productos/importación/stock | validado |
| F2-03 | P1 | Existen contratos de repositorio ubicados en `Walos.Application.Services` en lugar de `Walos.Domain.Interfaces` | `ISuppliersRepository.cs`, `IUsersRepository.cs`, `IRecipeRepository.cs`, `IPurchaseOrderRepository.cs`, `IDeliveryRepository.cs`, `ICatalogRepository.cs` en `Walos.Application/Services/` | Boundary inconsistente: la definición de puertos de infraestructura queda repartida entre capas | Consolidar contratos de repositorio en una sola capa de interfaces | validado |
| F2-04 | P1 | La composición de dependencias tiene ownership mezclado: `AddApplication()` y `AddInfrastructure()` registran servicios de aplicación | `Walos.Application/DependencyInjection.cs` y `Walos.Infrastructure/DependencyInjection.cs` registran `IAuthService`, `ISalesService`, `IFinanceService`, etc. | La infraestructura está asumiendo responsabilidades de la capa de aplicación; complica el modelo mental y duplica wiring | Dejar services de aplicación SOLO en `AddApplication()` y repos/infra SOLO en `AddInfrastructure()` | validado |
| F2-05 | P2 | La estrategia de validación no está unificada | Solo se detectaron `CreateProductValidator` y `AiInputValidator`, mientras muchos endpoints validan manualmente strings/campos | Inconsistencia de errores, reglas duplicadas y menos reaprovechamiento | Definir criterio: FluentValidation para requests de entrada y validación de negocio en services | validado |
| F2-06 | P2 | Algunas interfaces de servicio están declaradas en el mismo archivo que su implementación | `CashRegisterService.cs`, `CreditService.cs`, `RefundService.cs` | No rompe nada, pero vuelve más difusa la separación entre contrato e implementación | Separar interface y clase cuando se toque el módulo | pendiente |
| F2-07 | P2 | `AiController` depende de una clase concreta (`OrchestratorService`) en lugar de una abstracción | `AiController.cs` | Reduce flexibilidad para testeo/sustitución y deja más pegado el controller a una implementación puntual | Introducir interfaz si el orquestador sigue creciendo o si se necesita testearlo aislado | pendiente |

---

## Evidencia principal

### Controllers thin y service-driven

- `FinanceController.cs`
- `CompanyController.cs`
- `DeliveryController.cs`
- `CreditController.cs`
- `CashRegisterController.cs`
- `RefundController.cs`

Estos controllers muestran una dirección correcta: reciben request, delegan, responden.

### Controllers repository-driven

- `InventoryController.cs`
- `SuppliersController.cs`
- `CatalogController.cs`
- `PurchaseOrdersController.cs`
- `RecipesController.cs`
- `UsersController.cs`
- `PlatformController.cs`
- parte de `AdminController.cs`

Acá aparece la inconsistencia arquitectónica más clara de la fase.

### Composition root inconsistente

- `backend-dotnet/src/Walos.Application/DependencyInjection.cs`
- `backend-dotnet/src/Walos.Infrastructure/DependencyInjection.cs`

Ambos registran servicios de aplicación. Eso es una señal clara de boundary borroso.

---

## Evaluación por capa

### API

**Estado:** mixto

- bien en varios módulos nuevos/maduros
- inconsistente en módulos todavía controller+repo

### Application

**Estado:** bueno, pero desigual

- hay servicios potentes y reales
- algunos casos de uso todavía no viven donde deberían

### Domain

**Estado:** aceptable

- existe como capa separada
- pero no todos los contratos que actúan como “puertos” están centralizados ahí

### Infrastructure

**Estado:** fuerte pero muy protagónico

- hace lo que debe hacer
- pero en algunos flujos está demasiado expuesto directamente a controllers

---

## Conclusión de Fase 2

La arquitectura backend de Walos tiene una base suficientemente buena como para escalar, **PERO necesita unificar criterio**.

El problema principal NO es “el backend está roto”.

El problema real es este:

> **hay dos filosofías conviviendo**:
>
> - módulos guiados por application services
> - módulos guiados por repository desde controller

Si eso no se corrige, cada feature nueva puede reforzar una dirección distinta y el backend se vuelve cada vez más heterogéneo.

---

## Mapa de correcciones futuras

| Ref | Nace de | Corrección futura | Fase sugerida | Prioridad | Estado |
|-----|---------|-------------------|---------------|-----------|--------|
| C-06 | F2-01 | Crear services para módulos todavía repository-driven | ejecución posterior | P1 | pendiente |
| C-07 | F2-02 | Dividir `InventoryController` por responsabilidades y mover lógica a Application | ejecución posterior | P1 | pendiente |
| C-08 | F2-03 | Reubicar contratos de repositorio a una capa única y coherente | ejecución posterior | P1 | pendiente |
| C-09 | F2-04 | Limpiar DI duplicado entre Application e Infrastructure | ejecución posterior | P1 | pendiente |
| C-10 | F2-05 | Definir estrategia única de validación | ejecución posterior | P2 | pendiente |
| C-11 | F2-07 | Evaluar interfaz para `OrchestratorService` si sigue creciendo | ejecución posterior | P2 | pendiente |

---

## Siguiente paso

Pasar a **Fase 3 — auditoría frontend**, con foco en:

1. rutas
2. módulos
3. estado global
4. servicios HTTP
5. tamaño y responsabilidad de páginas críticas

---

## Key Learnings

1. El backend de Walos ya tiene una base seria, pero arquitectónicamente todavía está mezclando estilos.
2. El mayor problema de Fase 2 no es la ausencia de capas, sino la INCONSISTENCIA en cómo se respetan.
3. `InventoryController` es hoy el mejor ejemplo de deuda arquitectónica por concentración de responsabilidades.

---

# Fase 3 — Auditoría frontend

## Objetivo

Revisar modularidad, crecimiento de páginas, uso de estado global, servicios HTTP y consistencia general de la arquitectura frontend.

## Resultado

**Fase 3 completada** con revisión de rutas, stores, services y módulos principales.

---

## Diagnóstico general

El frontend de Walos tiene una dirección correcta:

- módulos por dominio
- capa `services/` separada
- `React Query` para server state
- `Zustand` para estado global
- layout y auth centralizados

PERO otra vez aparece el mismo patrón que vimos en backend:

> **la base es buena, pero la consistencia empieza a aflojar cuando el sistema crece**.

No es un frontend desordenado desde cero.  
Es un frontend que YA necesita reglas más estrictas para no degradarse.

---

## Lo que está BIEN

### 1. La organización por módulos existe de verdad

Se verificó estructura real por dominio en:

- `inventory`
- `sales`
- `finance`
- `delivery`
- `suppliers`
- `settings`
- `users`
- `alerts`
- `dashboard`
- `auth`
- `admin`

Eso es MUCHO mejor que un `components/` gigante sin boundaries.

### 2. La capa de acceso HTTP está centralizada

Punto central verificado:

- `frontend/src/config/api.js`

Y además los módulos consumen servicios específicos (`inventoryService`, `salesService`, etc.) en lugar de pegarle al cliente HTTP desde cualquier componente.

### 3. La sesión y preferencias visuales están acotadas

Stores verificados:

- `authStore.js`
- `uiStore.js`

No se ve un abuso masivo de Zustand para meter cualquier cosa ahí. Eso es sano.

---

## Hallazgos de Fase 3

| ID | Severidad | Hallazgo | Evidencia | Impacto | Acción propuesta | Estado |
|----|-----------|----------|-----------|---------|------------------|--------|
| F3-01 | P1 | `App.jsx` concentra demasiada responsabilidad de composición global | `App.jsx` maneja routing, auth gating, theme sync, query client setup, route duplication de settings y bootstrap de auth getter | Aumenta el acoplamiento del entrypoint y vuelve más costoso evolucionar navegación/global concerns | Separar router config, auth guard y providers en piezas dedicadas | validado |
| F3-02 | P1 | Hay páginas y componentes demasiado grandes para su rol | `ProductFormModal.jsx` (556), `InvoicePanel.jsx` (538), `SalesPage.jsx` (475), `SuppliersPage.jsx` (425), `UsersPage.jsx` (328) | Riesgo de componentes “god component”, difícil testeo y regresiones UI | Dividir por subfeatures / hooks / presentational components | validado |
| F3-03 | P1 | Hay inconsistencias funcionales y riesgo real en `userService` para flujos admin cross-tenant | `adminUpdate()` usa `PUT /users/{id}` y `adminDelete()` usa `DELETE /users/{id}` ignorando `companyId`, mientras el modo dev de `UsersPage.jsx` pretende operar sobre múltiples comercios | Puede ejecutar operaciones sobre el endpoint equivocado y depender del tenant actual en lugar del target real | Corregir servicios admin o ajustar backend/flujo explícitamente | validado |
| F3-04 | P2 | Existe código admin aparentemente huérfano/no enrutable | `modules/admin/AdminUsersPage.jsx` existe pero no está referenciado en `App.jsx` | Código muerto o flujo abandonado aumenta ruido y confusión | Eliminarlo o conectarlo formalmente a rutas | validado |
| F3-05 | P2 | La capa `services/` no sigue un estilo uniforme | Ej.: `inventoryService` usa `async/await`, `supplierService` y `userService` usan `.then(r => r.data)`, nombres y estilo varían | No rompe funcionalidad, pero degrada legibilidad y hace más difícil mantener convenciones | Unificar estilo de servicios y contrato de retorno | validado |
| F3-06 | P2 | `uiStore` duplica parte de branding que también vive en server state | `Layout.jsx` trae company settings vía query y luego persiste `companyName`/`companyLogoUrl` en `uiStore` | Duplica fuentes de verdad y puede dejar estado visual stale | Evaluar si branding debe vivir solo en query cache + hydrate local controlado | validado |
| F3-07 | P2 | El routing de settings está expandido manualmente a múltiples rutas que renderizan la misma página | `/settings`, `/settings/branding`, `/settings/themes`, `/settings/discounts`, etc. → todos renderizan `SettingsPage` | El árbol de rutas crece por repetición y concentra lógica de navegación dentro de una sola página | Definir subroutes reales o un mapa declarativo de secciones | validado |

---

## Evidencia principal

### Entry point cargado

- `frontend/src/App.jsx`

Contiene:

- providers globales
- guards de auth
- sync de tema
- seteo del getter de auth para API
- definición manual de rutas

Funciona, sí. Pero está empezando a pedir separación.

### Componentes/páginas sobredimensionadas

Mediciones verificadas:

- `ProductFormModal.jsx` → 556 líneas
- `InvoicePanel.jsx` → 538 líneas
- `SalesPage.jsx` → 475 líneas
- `SuppliersPage.jsx` → 425 líneas
- `UsersPage.jsx` → 328 líneas

Acá NO hay que obsesionarse con el número.  
El problema es la **concentración de responsabilidades**, no el largo por sí solo.

### Bug/fragilidad concreta en servicios admin de usuarios

Archivo verificado:

- `frontend/src/services/userService.js`

Problema:

- `adminUpdate()` llama `PUT /users/{id}`
- `adminDelete()` llama `DELETE /users/{id}`

Mientras que el modo admin/dev en `UsersPage.jsx` trabaja como si pudiera operar usuarios de otros comercios.

Eso es un hallazgo REAL, no una opinión estilística.

### Duplicación de estado visual

Archivos:

- `frontend/src/components/layout/Layout.jsx`
- `frontend/src/stores/uiStore.js`

`Layout` consulta settings del backend y luego replica branding en Zustand persist.  
Eso crea una frontera borrosa entre:

- server state
- UI preferences
- branding efectivo

---

## Evaluación por área

### Routing

**Estado:** aceptable, pero centralizado en exceso

### Estado global

**Estado:** razonable

- `authStore` y `uiStore` son acotados
- no se detectó un abuso grave de Zustand

### Server state

**Estado:** bueno

- React Query está bien presente
- varias páginas trabajan con `queryKey` e invalidaciones explícitas

### Modularidad de UI

**Estado:** mixto

- buena separación por carpetas
- mala concentración interna en algunas páginas/componentes

### Servicios

**Estado:** útil pero inconsistente

- existe la capa
- faltan convenciones unificadas
- al menos un flujo admin tiene riesgo funcional real

---

## Conclusión de Fase 3

El frontend de Walos está mejor de lo que suele estar una SPA de este tamaño.  
Eso hay que decirlo COMO ES.

Pero también es verdad esto:

> **ya entró en la etapa donde la arquitectura necesita disciplina, no solo buenas intenciones**.

Los dos focos más importantes de esta fase son:

1. **componentes/páginas demasiado cargados**
2. **inconsistencias en la capa de servicios y en algunos flujos admin**

Si no se ordena ahora, cada módulo nuevo va a seguir creciendo con su propio criterio.

---

## Mapa de correcciones futuras

| Ref | Nace de | Corrección futura | Fase sugerida | Prioridad | Estado |
|-----|---------|-------------------|---------------|-----------|--------|
| C-12 | F3-01 | Separar providers, router config y guards de `App.jsx` | ejecución posterior | P1 | pendiente |
| C-13 | F3-02 | Partir páginas/componentes grandes por subfeatures u hooks dedicados | ejecución posterior | P1 | pendiente |
| C-14 | F3-03 | Corregir `userService` admin para no usar endpoints tenant-scoped incorrectos | ejecución posterior | P1 | pendiente |
| C-15 | F3-04 | Eliminar o enrutar `AdminUsersPage.jsx` | ejecución posterior | P2 | pendiente |
| C-16 | F3-05 | Estandarizar estilo y contrato de `services/` | ejecución posterior | P2 | pendiente |
| C-17 | F3-06 | Revisar si branding debe persistirse en `uiStore` o derivarse del server state | ejecución posterior | P2 | pendiente |

---

## Siguiente paso

Pasar a **Fase 4 — multi-tenant y seguridad**, con foco en:

1. claims JWT
2. `TenantContextMiddleware`
3. headers de contexto
4. endpoints sensibles
5. consistencia de filtros por tenant/branch

---

## Key Learnings

1. El frontend de Walos tiene buena estructura macro, pero ya necesita reglas más estrictas para sostenerse.
2. El mayor riesgo de Fase 3 no es Zustand ni React Query: es la concentración de responsabilidades y la inconsistencia entre módulos.
3. `userService` en modo admin muestra un hallazgo funcional real, no solo deuda estética.

---

# Fase 4 — Multi-tenant y seguridad

## Objetivo

Verificar el aislamiento entre tenants/sucursales, la solidez de auth/JWT, el uso de headers de contexto y la protección real de endpoints sensibles.

## Resultado

**Fase 4 completada** con revisión de middleware, auth, cliente API, controllers sensibles y repositorios clave.

---

## Diagnóstico general

Acá hay que ser MUY precisos:

> **el aislamiento por company está bastante bien encaminado**.

La mayoría de repositorios y controllers importantes usan `companyId` del JWT/tenant context y filtran consultas por empresa.  
Eso es BUENO.

PERO encontré dos cosas serias:

1. un endpoint administrativo de plataforma está subprotegido
2. el override de sucursal (`X-Branch-ID`) es demasiado confiado y no valida pertenencia/autorización

O sea: el aislamiento entre **companies** se ve razonablemente defendido en muchos flujos, pero el aislamiento/autoridad por **branch** es más débil y hay un hueco claro en administración de plataforma.

---

## Lo que está BIEN

### 1. El `companyId` principal sale del JWT, no del cliente

Middleware verificado:

- `backend-dotnet/src/Walos.API/Middleware/TenantContextMiddleware.cs`

Ahí:

- `companyId` se toma del claim JWT
- `userId` se toma del claim JWT
- el backend NO confía en `X-Company-ID` para construir el tenant principal

Eso evita una clase muy común de vulnerabilidad donde el cliente elige el tenant por header.

### 2. Muchos repositorios sí filtran por empresa

Ejemplos verificados:

- `InventoryRepository.GetAllProductsAsync(...)` → `WHERE p.company_id = @CompanyId`
- `InventoryRepository.GetProductByIdAsync(...)` → `WHERE p.id = @ProductId AND p.company_id = @CompanyId`
- `InventoryRepository.GetStockByBranchAsync(...)` → `WHERE p.company_id = @CompanyId`
- `UsersRepository.GetByIdAsync(...)` → `WHERE u.id = @UserId AND u.company_id = @CompanyId`
- `PlatformRepository.GetPaymentMethodsAsync(...)` → `WHERE company_id = @CompanyId`
- `CompanyRepository.GetCompanySettingsAsync(...)` → `WHERE id = @CompanyId`

Eso muestra intención real de aislamiento tenant-scoped.

### 3. JWT tiene validación mínima correcta

En `Program.cs` se verificó:

- firma (`ValidateIssuerSigningKey = true`)
- expiración (`ValidateLifetime = true`)
- `ClockSkew = TimeSpan.Zero`

No es perfecto en todo, pero no está “abierto”.

---

## Hallazgos de Fase 4

| ID | Severidad | Hallazgo | Evidencia | Impacto | Acción propuesta | Estado |
|----|-----------|----------|-----------|---------|------------------|--------|
| F4-01 | P0 | `PlatformAdminController` expone operaciones administrativas de plataforma con solo `[Authorize]` y SIN restricción de rol | `backend-dotnet/src/Walos.API/Controllers/PlatformAdminController.cs` tiene `[Authorize]`, pero no `[Authorize(Roles = ...)]` en controller ni endpoints | Cualquier usuario autenticado podría acceder a catálogo admin, empresas, planes, asignación de servicios e invoices de otras compañías si llega al endpoint | Restringir inmediatamente a `dev`/superadmin de plataforma y revisar logs de acceso | validado |
| F4-02 | P1 | `X-Branch-ID` puede sobreescribir la sucursal del claim sin validación adicional | `TenantContextMiddleware.cs`: comentario y lógica `header > JWT claim`; no se encontró chequeo de pertenencia/autorización de esa branch para el usuario | Un usuario autenticado podría pivotear a otra sucursal del mismo company si conoce o adivina el ID | Validar que la branch exista dentro del company y que el usuario tenga permiso sobre esa sucursal antes de aceptar override | validado |
| F4-03 | P1 | Varios endpoints aceptan `branchId` por query/body además del tenant context, sin una capa visible de autorización por sucursal | `SalesController.GetTables([FromQuery] long? branchId)`, `FinanceController` varios métodos, `InventoryController` stock/alerts/reportes, etc. | Amplía la superficie para consultar/operar otra sucursal del mismo tenant si no hay control de acceso por branch en otra capa | Unificar política de branch access y centralizar validación | validado |
| F4-04 | P2 | El frontend envía `X-Company-ID`, pero el backend lo ignora | `frontend/src/config/api.js` envía `X-Company-ID`; `TenantContextMiddleware.cs` no lo consume | No parece vulnerabilidad directa, pero sí contrato muerto/confuso que puede inducir a error futuro | Eliminar header o documentarlo explícitamente como legacy/no usado | validado |
| F4-05 | P2 | La protección JWT no valida issuer/audience | `Program.cs`: `ValidateIssuer = false`, `ValidateAudience = false` | No rompe por sí solo en un sistema interno con clave fuerte, pero reduce endurecimiento defensivo | Evaluar issuer/audience si el sistema va a crecer o integrarse externamente | pendiente |

---

## Evidencia principal

### Hallazgo crítico: admin de plataforma subprotegido

Archivo:

- `backend-dotnet/src/Walos.API/Controllers/PlatformAdminController.cs`

Observación verificada:

- tiene `[Authorize]`
- NO tiene roles de plataforma en controller ni endpoints

Y además expone operaciones sensibles como:

- ver compañías
- ver plan de cualquier company
- asignar servicios
- generar facturas
- cambiar estado de facturas

Esto NO es una observación estética.  
Es el hallazgo más serio de la fase.

### Override de branch demasiado permisivo

Archivo:

- `backend-dotnet/src/Walos.API/Middleware/TenantContextMiddleware.cs`

Lógica actual:

```text
BranchId: header > JWT claim
```

Eso significa:

- si llega `X-Branch-ID` válido numéricamente
- el backend lo toma por encima del claim

Y no encontré evidencia de:

- verificación de pertenencia de esa branch al usuario
- verificación de que el usuario pueda operar esa sucursal

### Header muerto de company

Archivos:

- `frontend/src/config/api.js`
- `backend-dotnet/src/Walos.API/Middleware/TenantContextMiddleware.cs`

El frontend manda:

- `X-Company-ID`
- `X-Branch-ID`

Pero el backend solo usa:

- JWT claims para company
- header opcional para branch

Eso deja un contrato confuso.

---

## Evaluación por área

### Aislamiento por company

**Estado:** razonablemente bueno

- el backend suele apoyarse en `companyId` del JWT
- muchos repositorios filtran correctamente por empresa

### Aislamiento por branch

**Estado:** débil / incompleto

- hay override cliente → backend
- no apareció validación centralizada de autorización por sucursal

### Endpoints administrativos

**Estado:** INCONSISTENTE

- `AdminController` sí está restringido a `dev`
- `PlatformAdminController` NO

Eso es exactamente el tipo de inconsistencia que después rompe seguridad real.

### Cliente API

**Estado:** aceptable, pero con ruido contractual

- centralizado
- pero manda un header de company que el backend no usa

---

## Conclusión de Fase 4

Walos NO está ciego en multi-tenant.  
Eso sería injusto decirlo.

La realidad es más interesante:

> **el aislamiento por empresa está bastante encaminado, pero la autorización por sucursal y algunos endpoints administrativos todavía tienen huecos importantes**.

El punto MÁS urgente de esta fase es:

1. **cerrar `PlatformAdminController` por rol**

Y el segundo foco fuerte es:

2. **definir una política real de acceso por branch**

porque hoy el sistema acepta demasiado del cliente en esa parte.

---

## Mapa de correcciones futuras

| Ref | Nace de | Corrección futura | Fase sugerida | Prioridad | Estado |
|-----|---------|-------------------|---------------|-----------|--------|
| C-18 | F4-01 | Restringir `PlatformAdminController` a rol de plataforma (`dev` o equivalente) | inmediata | P0 | pendiente |
| C-19 | F4-02 | Validar `X-Branch-ID` contra permisos reales del usuario y contra el company actual | inmediata | P1 | pendiente |
| C-20 | F4-03 | Crear política/servicio central para autorización por sucursal | ejecución posterior | P1 | pendiente |
| C-21 | F4-04 | Eliminar `X-Company-ID` del frontend o documentar que no participa del tenant resolution | ejecución posterior | P2 | pendiente |
| C-22 | F4-05 | Evaluar issuer/audience en JWT si el despliegue se endurece | ejecución posterior | P2 | pendiente |

---

## Siguiente paso

Pasar a **Fase 5 — contratos, datos y consistencia**, con foco en:

1. DTOs
2. respuestas API
3. naming de campos
4. supuestos frontend ↔ backend
5. coherencia entre dominios

---

## Key Learnings

1. El aislamiento por `companyId` en Walos está bastante mejor de lo que parecía al principio.
2. El verdadero punto frágil no es `company`, sino `branch` y la confianza excesiva en el override desde cliente.
3. `PlatformAdminController` es el hallazgo más crítico de toda la Fase 4.

---

# Fase 5 — Contratos, datos y consistencia

## Objetivo

Revisar si frontend y backend hablan el mismo idioma de forma consistente: envelopes API, DTOs, naming de campos, supuestos entre capas y puntos donde el contrato se rompe o se vuelve frágil.

## Resultado

**Fase 5 completada** con revisión de `ApiResponse`, DTOs relevantes, servicios frontend y componentes que consumen datos directamente.

---

## Diagnóstico general

La buena noticia primero:

> **Walos sí tiene un contrato base reconocible**.

Ese contrato es:

- envelope `ApiResponse<T>`
- `success`
- `message`
- `data`
- `count` opcional

Y eso aparece de forma bastante extendida tanto en backend como en frontend.

PERO el problema de esta fase no es “no hay contrato”.

El problema real es este:

> **hay un contrato principal, pero alrededor de él se acumularon accesos directos, convenciones mezcladas y nombres inconsistentes**.

Eso vuelve frágil la evolución del sistema.

---

## Lo que está BIEN

### 1. Existe un envelope API claro

Archivo verificado:

- `backend-dotnet/src/Walos.Application/DTOs/Common/ApiResponse.cs`

El patrón principal está bien definido y la mayoría del frontend ya trabaja esperando:

- `result.success`
- `result.data`
- `result.message`

### 2. El backend usa camelCase en JSON

Verificado en:

- `Program.cs` con `JsonNamingPolicy.CamelCase`

Eso permite que DTOs en C# con `PascalCase` se serialicen a un naming razonable para frontend JS.

### 3. Los DTOs core existen y no todo se hace con objetos anónimos

Ejemplos revisados:

- `CreateProductRequest`
- `CreateFinancialEntryRequest`
- `CreateUserRequest`
- `CreateTableRequest`
- `InvoiceTableRequest`
- `LoginResult`, `TokenResult`, `UserInfo`

Eso ayuda mucho a la estabilidad de contratos.

---

## Hallazgos de Fase 5

| ID | Severidad | Hallazgo | Evidencia | Impacto | Acción propuesta | Estado |
|----|-----------|----------|-----------|---------|------------------|--------|
| F5-01 | P1 | La capa de contratos en frontend no está completamente centralizada: varios componentes llaman `api` directo y definen mini-servicios locales | `CreditsPanel.jsx`, `OrderItemsList.jsx`, `SalesSummaryTab.jsx`, `ImportProductsModal.jsx` | Duplica contratos, dispersa endpoints y hace más fácil romper consistencia si cambia backend | Mover esos accesos a `services/` oficiales por dominio | validado |
| F5-02 | P1 | Hay inconsistencia real de naming de campos en frontend respecto al contrato camelCase | `Layout.jsx` usa `user?.first_name` / `user?.last_name`, mientras `UserInfo` del backend serializa `firstName` / `lastName` | Genera lógica defensiva innecesaria y riesgo de bugs silenciosos en presentación/auth | Normalizar consumo a camelCase en todo frontend | validado |
| F5-03 | P2 | El frontend está demasiado acoplado al envelope `ApiResponse` dentro de componentes de UI | Uso masivo de `data?.data`, `result.data`, `res.data.data` en páginas y componentes | Cambios menores en servicios o envelope impactan directamente muchas vistas | Hacer que `services/` puedan devolver data más procesada en casos repetitivos | validado |
| F5-04 | P2 | Los contratos están repartidos entre archivos y estilos de organización heterogéneos | Ej.: `InvoiceTableRequest` vive en `CreateTableRequest.cs`; `FinanceRequests.cs` agrupa varias requests; `UserDTOs.cs` agrupa request + response models | No rompe runtime, pero hace menos evidente dónde vive cada contrato y complica navegación | Estandarizar agrupación de DTOs por feature/caso de uso o por archivo coherente | validado |
| F5-05 | P2 | Las respuestas de archivo/binario y los endpoints especiales no siguen una abstracción consistente en frontend | `ImportProductsModal.jsx` descarga plantilla con `api.get(... blob)` directo; `salesService.exportOrders()` devuelve blob mientras otros servicios devuelven envelope | Hay contratos especiales válidos, pero quedan escondidos y sin una convención clara | Definir convención para endpoints binarios / raw responses | validado |

---

## Evidencia principal

### Envelope principal coherente

Archivo:

- `backend-dotnet/src/Walos.Application/DTOs/Common/ApiResponse.cs`

El diseño principal del contrato está bien:

```text
success
message
data
count?
code?
details?
```

Y el frontend lo consume consistentemente en muchos lugares.

### Accesos directos fuera de `services/`

Archivos verificados:

- `frontend/src/modules/sales/components/CreditsPanel.jsx`
- `frontend/src/modules/sales/components/OrderItemsList.jsx`
- `frontend/src/modules/sales/components/SalesSummaryTab.jsx`
- `frontend/src/modules/inventory/components/ImportProductsModal.jsx`

Esto rompe la idea de:

```text
component -> service -> api client
```

y pasa a:

```text
component -> api client directo
```

Ahí es donde se empieza a fragmentar el contrato.

### Inconsistencia real de naming

Archivos:

- `frontend/src/components/layout/Layout.jsx`
- `backend-dotnet/src/Walos.Application/Services/IAuthService.cs`

El backend devuelve:

- `firstName`
- `lastName`

Pero `Layout.jsx` intenta leer:

- `first_name`
- `last_name`

Hoy no explota porque tiene fallback hacia `user.name`, pero eso NO significa que esté bien.

### Acoplamiento al envelope desde UI

Patrón verificado en muchísimos archivos:

- `const summary = summaryData?.data`
- `const alerts = alertsData?.data ?? []`
- `setResult(res.data); if (res.data.data?.created > 0) ...`

No es un error fatal.  
Pero sí muestra que la UI está leyendo demasiado del shape bruto de backend.

---

## Evaluación por área

### Contrato base API

**Estado:** bueno

### Naming de campos

**Estado:** mayormente bien, pero con fugas inconsistentes

### Servicios frontend

**Estado:** útil, pero no completamente respetado

### DTOs backend

**Estado:** suficientemente buenos, aunque heterogéneos en organización

### Acoplamiento vista ↔ contrato

**Estado:** más alto de lo deseable

---

## Conclusión de Fase 5

Walos tiene una ventaja importante:

> **ya existe un contrato principal compartido entre backend y frontend**.

Eso es una BASE sólida.

Pero también quedó claro esto:

1. la capa de servicios frontend todavía no está cerrando completamente el acceso a contratos
2. hay nombres mezclados y acceso demasiado directo al shape bruto de respuesta
3. si mañana cambian envelopes, responses especiales o naming, el impacto se va a sentir en demasiadas vistas

No es una crisis.  
Pero sí es deuda de consistencia.

---

## Mapa de correcciones futuras

| Ref | Nace de | Corrección futura | Fase sugerida | Prioridad | Estado |
|-----|---------|-------------------|---------------|-----------|--------|
| C-23 | F5-01 | Mover accesos directos a `api` desde componentes hacia `services/` por dominio | ejecución posterior | P1 | pendiente |
| C-24 | F5-02 | Normalizar naming camelCase en todo frontend (`firstName`, `lastName`, etc.) | ejecución posterior | P1 | pendiente |
| C-25 | F5-03 | Reducir lectura de envelope bruto en UI cuando haya patrones repetidos | ejecución posterior | P2 | pendiente |
| C-26 | F5-04 | Estandarizar organización de DTOs backend | ejecución posterior | P2 | pendiente |
| C-27 | F5-05 | Definir convención para respuestas blob/raw vs envelope JSON | ejecución posterior | P2 | pendiente |

---

## Siguiente paso

Pasar a **Fase 6 — testing y verificabilidad**, con foco en:

1. cobertura útil
2. flujos críticos protegidos
3. multi-tenant y seguridad en tests
4. huecos de validación automatizada

---

## Key Learnings

1. Walos ya tiene un contrato base frontend-backend reconocible y reutilizable.
2. El problema principal de Fase 5 no es ausencia de contrato, sino fuga de consistencia alrededor del contrato principal.
3. La UI todavía está demasiado cerca del shape bruto de algunas respuestas y eso aumenta fragilidad.

## Fase 6 — Testing y verificabilidad

### Objetivo de la fase

Validar si Walos tiene una base de tests que realmente proteja:

1. reglas de negocio importantes
2. flujos funcionales críticos
3. aislamiento multi-tenant y seguridad
4. capacidad real de detectar regresiones antes de producción

---

## Resultado general

Acá hay que ser JUSTOS:

> **Walos SÍ tiene infraestructura de testing real.**

No estamos frente a un proyecto sin tests.

Hay:

- `xUnit` + `Moq` + integración en backend
- `Vitest` + `Testing Library` en frontend
- `Playwright` para E2E
- soporte de coverage al menos a nivel de tooling

Eso es BUENO.

Pero el diagnóstico serio es este:

> **la base de testing existe, pero la cobertura útil todavía está bastante por detrás de la superficie real del producto**.

Y eso pega más fuerte todavía cuando mirás los hallazgos críticos de Fase 4.

---

## Lo que está BIEN

### 1. El backend sí tiene suite de tests con valor real

Archivos verificados:

- `backend-dotnet/tests/Walos.Tests/Services/AuthServiceTests.cs`
- `backend-dotnet/tests/Walos.Tests/Services/CompanyServiceTests.cs`
- `backend-dotnet/tests/Walos.Tests/Services/FinanceServiceTests.cs`
- `backend-dotnet/tests/Walos.Tests/Services/InventoryServiceTests.cs`
- `backend-dotnet/tests/Walos.Tests/Services/SalesServiceTests.cs`
- `backend-dotnet/tests/Walos.Tests/Validators/AiInputValidatorTests.cs`
- `backend-dotnet/tests/Walos.Tests/Validators/CreateProductValidatorTests.cs`
- `backend-dotnet/tests/Walos.Tests/Integration/AuthRepositoryIntegrationTests.cs`
- `backend-dotnet/tests/Walos.Tests/Integration/CompanyRepositoryIntegrationTests.cs`

O sea: hay tests de servicio, validación y algo de integración. Eso es una base LEGÍTIMA.

### 2. Existe preocupación explícita por aislamiento tenant en repositorios

Archivo verificado:

- `backend-dotnet/tests/Walos.Tests/Repositories/TenantIsolationSqlTests.cs`

Aunque el enfoque sea estático, igual muestra una intención correcta:

- validar joins por `company_id`
- evitar fugas multi-tenant en SQL sensible

### 3. El frontend sí tiene testing automatizado, no solo intención

Archivos verificados:

- `frontend/src/test/modules/auth/LoginPage.test.jsx`
- `frontend/src/test/stores/authStore.test.js`
- `frontend/src/test/utils/formatters.test.js`
- `frontend/e2e/auth.spec.js`
- `frontend/e2e/navigation.spec.js`
- `frontend/e2e/sales.spec.js`

No está vacío. Hay base real.

### 4. El tooling de coverage existe

Verificado en:

- `frontend/package.json`
- `backend-dotnet/tests/Walos.Tests/Walos.Tests.csproj`

Frontend tiene:

- `test:coverage`

Backend incluye:

- `coverlet.collector`

O sea: técnicamente el proyecto PUEDE medir cobertura.

---

## Hallazgos de Fase 6

| ID | Severidad | Hallazgo | Evidencia | Impacto | Acción propuesta | Estado |
|----|-----------|----------|-----------|---------|------------------|--------|
| F6-01 | P1 | La infraestructura de testing existe y el backend ya protege varias reglas de negocio importantes | `Walos.Tests.csproj`, `AuthServiceTests.cs`, `SalesServiceTests.cs`, `FinanceServiceTests.cs`, `InventoryServiceTests.cs` | Buena base para evolucionar con seguridad razonable en dominio/backend | Conservar esta base y usarla como estándar para nuevas correcciones | validado |
| F6-02 | P1 | La cobertura frontend automatizada es demasiado angosta frente a la amplitud funcional actual del producto | Solo se verifican `LoginPage`, `authStore`, `formatters` y tres specs E2E (`auth`, `navigation`, `sales`) | Regresiones en módulos reales pueden entrar sin red automática suficiente | Priorizar tests para inventory, users, settings, suppliers, admin y flujos de error | validado |
| F6-03 | P0 | Los hallazgos críticos de seguridad de Fase 4 no aparecen protegidos por tests automatizados visibles | No se encontraron tests específicos para `PlatformAdminController`, `TenantContextMiddleware` ni validación del override de `X-Branch-ID` dentro de la suite inspeccionada | Se puede corregir algo manualmente hoy y romperlo mañana sin alarma automática | Agregar tests de autorización/aislamiento antes o junto con el fix de seguridad | validado |
| F6-04 | P1 | El aislamiento tenant en tests backend está parcialmente cubierto, pero mucho de eso sigue siendo verificación estática de strings SQL | `TenantIsolationSqlTests.cs` usa `Assert.Contains(...)` sobre source code | Detecta ciertas omisiones de join, pero NO prueba comportamiento real end-to-end ni autorización por branch | Complementar con tests comportamentales/integración para company + branch | validado |
| F6-05 | P2 | No encontré evidencia visible de umbrales/gates de cobertura en la configuración inspeccionada del frontend | `vite.config.js` no define thresholds; `package.json` expone comando de coverage pero no política mínima | Se puede “tener coverage” sin criterio de calidad exigible | Definir thresholds mínimos por módulo o al menos por paquete crítico | validado |

---

## Evidencia principal

### Backend: base de testing seria

Archivo:

- `backend-dotnet/tests/Walos.Tests/Walos.Tests.csproj`

Paquetes verificados:

- `xunit`
- `Moq`
- `coverlet.collector`
- `Microsoft.NET.Test.Sdk`

Eso confirma que el proyecto backend no está improvisando tests.

### Frontend: tooling y scripts reales

Archivo:

- `frontend/package.json`

Scripts verificados:

- `test`
- `test:coverage`
- `test:e2e`

Esto confirma que el stack de pruebas está preparado.

### Cobertura frontend actual: muy concentrada

Unit/integration frontend revisados:

- `frontend/src/test/modules/auth/LoginPage.test.jsx`
- `frontend/src/test/stores/authStore.test.js`
- `frontend/src/test/utils/formatters.test.js`

E2E revisados:

- `frontend/e2e/auth.spec.js`
- `frontend/e2e/navigation.spec.js`
- `frontend/e2e/sales.spec.js`

Eso cubre una parte chica del producto comparado con:

- inventario
- proveedores
- usuarios
- configuración
- administración de plataforma
- multi-sucursal
- permisos/roles

### Tenant isolation tests: buena intención, cobertura limitada

Archivo:

- `backend-dotnet/tests/Walos.Tests/Repositories/TenantIsolationSqlTests.cs`

Lo que valida hoy es este patrón:

- que ciertos repositorios mantengan joins por `company_id`
- que no desaparezcan algunas restricciones SQL clave

Eso sirve, PERO no reemplaza:

- tests de autorización
- tests de middleware
- tests de acceso cruzado por branch
- tests E2E multi-tenant

### Falta de protección automatizada sobre hallazgos críticos

Dentro de la suite inspeccionada NO apareció cobertura visible para:

- `PlatformAdminController`
- `TenantContextMiddleware`
- override de `X-Branch-ID`
- restricción por rol para endpoints de plataforma

Y este punto importa MUCHÍSIMO porque ya vimos en Fase 4 que ahí está el riesgo más serio.

---

## Evaluación por área

### Backend services

**Estado:** bueno

Hay pruebas con valor sobre validaciones y reglas de negocio.

### Backend security / multi-tenant

**Estado:** insuficiente para el riesgo actual

Hay intención de aislamiento, pero no vi cobertura automatizada proporcional a la criticidad de los hallazgos.

### Frontend unit tests

**Estado:** básico

Sirven como punto de partida, pero todavía no acompañan la complejidad real del frontend.

### Frontend E2E

**Estado:** básico a intermedio bajo

Cubre login, navegación y algo de ventas; no alcanza para dar confianza global sobre el producto.

### Verificabilidad general

**Estado:** aceptable como base, insuficiente para endurecer cambios críticos sin ampliar suite

---

## Conclusión de Fase 6

La conclusión HONESTA es esta:

> **Walos sí tiene cultura de testing inicial, pero todavía no tiene red de seguridad proporcional a su superficie real ni a sus riesgos más críticos.**

Traducido:

1. el backend está bastante mejor parado que el frontend
2. el frontend tiene tests, pero todavía muy concentrados en una zona chica
3. los riesgos más delicados detectados en seguridad/multi-tenant NO aparecen blindados por tests visibles
4. hoy se puede corregir un problema serio y volver a romperlo después sin una alarma automática confiable

Eso NO invalida lo que hay.  
Pero sí marca una prioridad clarísima:

> **antes o junto con fixes críticos, hay que agregar tests que protejan exactamente esos puntos sensibles**.

---

## Mapa de correcciones futuras

| Ref | Nace de | Corrección futura | Fase sugerida | Prioridad | Estado |
|-----|---------|-------------------|---------------|-----------|--------|
| C-28 | F6-02 | Expandir unit/integration tests frontend por dominio (`inventory`, `users`, `settings`, `suppliers`) | ejecución posterior | P1 | pendiente |
| C-29 | F6-03 | Agregar tests de autorización para `PlatformAdminController` y restricciones por rol | ejecución posterior | P0 | pendiente |
| C-30 | F6-03, F6-04 | Agregar tests de `TenantContextMiddleware` y validación de override de `X-Branch-ID` | ejecución posterior | P0 | pendiente |
| C-31 | F6-04 | Complementar `TenantIsolationSqlTests` con pruebas comportamentales/integración multi-tenant | ejecución posterior | P1 | pendiente |
| C-32 | F6-05 | Definir política mínima de coverage/gates para áreas críticas | ejecución posterior | P2 | pendiente |

---

## Siguiente paso

Pasar a **Fase 7 — documentación, backlog y trazabilidad**, con foco en:

1. documentos desalineados con el código real
2. backlog desactualizado o engañoso
3. huecos entre plan, estado real y deuda detectada
4. cómo dejar un mapa ejecutable de remediación

---

## Key Learnings

1. Walos sí tiene infraestructura de testing real en frontend y backend; el problema no es ausencia total de pruebas.
2. La brecha principal está entre la superficie actual del producto y la cobertura útil que hoy lo protege.
3. Los hallazgos críticos de seguridad y multi-tenant necesitan tests dedicados, no solo fixes manuales.

## Fase 7 — Documentación, backlog y trazabilidad

### Objetivo de la fase

Verificar si la documentación operativa y el backlog siguen siendo una fuente confiable para:

1. entender el estado real del sistema
2. priorizar trabajo futuro
3. onboardear a alguien nuevo sin inducir errores
4. mapear deuda y correcciones desde un documento creíble

---

## Resultado general

Acá apareció una verdad incómoda, pero muy clara:

> **Walos tiene MÁS implementación real que documentación confiable.**

Eso es mejor que lo contrario.  
Pero sigue siendo un problema.

Porque cuando la documentación se vuelve inconsistente, deja de ser herramienta y pasa a ser ruido.

La conclusión de esta fase es:

> **el mayor problema documental hoy no es la ausencia de docs, sino la pérdida de una fuente única de verdad.**

Hay documentos útiles, sí.  
Pero también hay backlogs, READMEs y notas históricas que ya mezclan:

- estado real
- estado viejo
- trabajo completado
- trabajo supuestamente pendiente
- decisiones ya invalidadas por el código actual

---

## Lo que está BIEN

### 1. Existe bastante documentación en el repo

Archivos verificados:

- `docs/architecture.md`
- `docs/CODE_AUDIT_REPORT.md`
- `docs/conexiones.md`
- `docs/database-schema.md`
- `docs/finance-module.md`
- `docs/fase3-execution-guide.md`
- `docs/pending-*.md`
- `PENDING.md`

O sea: el problema NO es “no documentamos nada”.

### 2. Hay documentos que sí muestran intención de trazabilidad por módulo

Ejemplos sanos:

- `docs/pending-pos-gaps.md`
- `docs/pending-credit-module.md`
- `docs/pending-billing-ai-keys.md`

Aunque los nombres no siempre ayuden, esos docs conservan bastante contexto funcional e implementación.

### 3. Ya existe un documento vivo de auditoría

Archivo:

- `docs/diagnostico-auditoria-walos.md`

Esto es importante porque ahora sí hay un lugar razonable para centralizar:

- hallazgos
- severidades
- evidencia
- acciones propuestas
- mapa de correcciones

---

## Hallazgos de Fase 7

| ID | Severidad | Hallazgo | Evidencia | Impacto | Acción propuesta | Estado |
|----|-----------|----------|-----------|---------|------------------|--------|
| F7-01 | P0 | `PENDING.md` no es una fuente confiable de verdad: mezcla tareas marcadas como completadas con descripciones internas que dicen que no existe nada implementado | En el mismo archivo, `#9 Proveedores` figura `[x] Completado`, pero su detalle dice que solo existe un placeholder; `#15 Pedidos y Domicilios` también aparece completado arriba mientras su detalle habla de “no existe nada implementado” | Puede empujar decisiones erróneas, retrabajo o diagnósticos falsos si alguien confía en ese archivo | Reescribir `PENDING.md` para que sea roadmap real vigente o archivarlo y reemplazarlo por backlog saneado | validado |
| F7-02 | P1 | `backend-dotnet/README.md` está fuertemente desactualizado respecto al backend real | Dice `SQL Server`, `X-Tenant-ID`, foco casi exclusivo en inventario/auth; el código real usa `Npgsql/PostgreSQL` y hoy existen 19 controllers incluyendo `Delivery`, `Suppliers`, `Platform`, `Refund`, `CashRegister`, `Users` | Onboarding técnico defectuoso y lectura equivocada de arquitectura/capacidades | Actualizar README backend para reflejar stack, módulos y límites actuales | validado |
| F7-03 | P1 | `frontend/README.md` quedó en un estado pre-producto y contradice el frontend actual | Marca auth, ventas y proveedores como “próximamente”, sugiere setear tokens manuales en localStorage, y la estructura solo menciona inventario; `App.jsx` hoy ya enruta dashboard, inventory, sales, finance, suppliers, users, settings, delivery, admin tenants/companies | Induce una visión falsa de madurez y de uso real del frontend | Reescribir README frontend en función del estado actual de módulos y flujos | validado |
| F7-04 | P1 | `docs/architecture.md` está mejor que otros docs, pero ya contiene drift relevante frente al código y a la auditoría | Afirma que “todos los controllers son thin”, pero Fase 2 validó inconsistencia arquitectónica e `InventoryController` cargado; además marca frontend de caja/pagos/devoluciones como pendiente mientras `SalesPage` ya integra `CashRegisterBar`, modales de apertura/cierre y `RefundModal` | Da una sensación de arquitectura más limpia/cerrada de la que realmente existe | Ajustar `architecture.md` para separar “estado actual” de “estado objetivo” | validado |
| F7-05 | P2 | Hay documentos cuyo nombre comunica “pending” aunque su contenido ya documenta implementación completada | `docs/pending-credit-module.md` y `docs/pending-billing-ai-keys.md` abren con estado implementado | La nomenclatura ensucia navegación y dificulta distinguir backlog vs historial de implementación | Renombrar docs o moverlos a una carpeta de historial/decisiones implementadas | validado |
| F7-06 | P2 | `docs/CODE_AUDIT_REPORT.md` funciona más como artefacto histórico que como estado operativo actual, pero no está claramente enmarcado así | El documento lista problemas que hoy ya aparecen como resueltos en secciones posteriores, lo que exige lectura cuidadosa para no malinterpretarlo | Riesgo de tomar un reporte histórico como deuda viva actual | Marcar explícitamente el documento como “audit snapshot / historical report” y vincular al diagnóstico actual | validado |

---

## Evidencia principal

### `PENDING.md` se contradice a sí mismo

Esto NO es interpretación: está en el propio archivo.

Casos validados:

- el resumen superior marca módulos como completados
- pero luego el detalle de algunos de esos mismos módulos describe un sistema inexistente o placeholder

Los ejemplos más claros que verifiqué fueron:

- `#9 Proveedores`
- `#15 Pedidos y Domicilios`

Eso rompe totalmente su valor como backlog confiable.

### README backend desalineado con el backend real

Archivo:

- `backend-dotnet/README.md`

Problemas verificados:

- habla de `SQL Server`
- menciona `X-Tenant-ID` como header de respaldo
- retrata un backend centrado casi solo en auth + inventory

Pero el código real muestra otra cosa:

- `SqlConnectionFactory.cs` usa `NpgsqlConnection`
- `TenantContextMiddleware.cs` resuelve branch desde `X-Branch-ID`, no `X-Tenant-ID`
- el directorio `Controllers/` hoy tiene 19 controllers

### README frontend muy por detrás del producto real

Archivo:

- `frontend/README.md`

Dice que están “próximamente” cosas que YA existen o que ya están funcionalmente bastante avanzadas, por ejemplo:

- autenticación
- ventas
- proveedores

Además recomienda simular auth manual vía localStorage, cuando el frontend ya tiene:

- `LoginPage`
- rutas protegidas
- store de auth persistido
- módulos reales conectados

### `architecture.md` mezcla verdad actual con idealización

Archivo:

- `docs/architecture.md`

Lo rescatable es que está mucho mejor mantenido que los READMEs.

Pero aun así tiene drift importante:

- afirma que todos los controllers son thin
- deja como pendiente frontend de caja/pagos/devoluciones

Y eso ya no coincide con el estado real que vimos en:

- `frontend/src/modules/sales/SalesPage.jsx`
- `frontend/src/modules/sales/components/CashRegisterBar.jsx`
- `frontend/src/modules/sales/components/OpenCashRegisterModal.jsx`
- `frontend/src/modules/sales/components/CloseCashRegisterModal.jsx`
- `frontend/src/modules/sales/components/RefundModal.jsx`

### La carpeta `docs/` tiene valor, pero necesita taxonomía mejor

Detecté dos tipos de documentos mezclados bajo nombres poco útiles:

1. backlog/pending real
2. historial o especificación de cosas ya implementadas

Ese problema de naming no rompe código, pero sí rompe navegación mental.

---

## Evaluación por área

### Backlog principal

**Estado:** no confiable

`PENDING.md` hoy no puede tomarse como fuente de verdad sin auditarlo contra código.

### README backend

**Estado:** desactualizado fuerte

### README frontend

**Estado:** desactualizado fuerte

### Arquitectura global documentada

**Estado:** parcialmente útil, pero necesita corrección puntual

### Historial / docs por módulo

**Estado:** útil pero desordenado semánticamente

### Trazabilidad futura

**Estado:** mejora mucho si el documento vivo de diagnóstico pasa a ser referencia central

---

## Conclusión de Fase 7

La conclusión importante es esta:

> **el repo ya no tiene un problema de falta de documentación; tiene un problema de gobierno documental.**

Traducido:

1. hay demasiados documentos con distintos niveles de vigencia
2. el backlog principal perdió credibilidad
3. los READMEs principales están atrasados respecto al producto real
4. algunos docs sirven, pero están mal nombrados o mal posicionados
5. el mejor candidato a fuente operativa actual es el diagnóstico vivo que estamos armando

Si esto no se corrige, pasa algo clásico y peligroso:

> la próxima persona “lee para entender” y termina entendiendo MAL.

Y eso en arquitectura pega fuerte, porque las malas decisiones empiezan mucho antes de tocar código.

---

## Mapa de correcciones futuras

| Ref | Nace de | Corrección futura | Fase sugerida | Prioridad | Estado |
|-----|---------|-------------------|---------------|-----------|--------|
| C-33 | F7-01 | Reescribir o reemplazar `PENDING.md` como backlog real vigente | ejecución posterior | P0 | pendiente |
| C-34 | F7-02 | Actualizar `backend-dotnet/README.md` al stack y módulos reales actuales | ejecución posterior | P1 | pendiente |
| C-35 | F7-03 | Actualizar `frontend/README.md` a estado productivo real | ejecución posterior | P1 | pendiente |
| C-36 | F7-04 | Corregir `docs/architecture.md` separando estado actual vs estado objetivo | ejecución posterior | P1 | pendiente |
| C-37 | F7-05 | Renombrar/reorganizar docs `pending-*` que hoy son historial implementado | ejecución posterior | P2 | pendiente |
| C-38 | F7-06 | Marcar `docs/CODE_AUDIT_REPORT.md` como snapshot histórico y enlazar al diagnóstico actual | ejecución posterior | P2 | pendiente |

---

## Cierre de auditoría

Con Fase 7, la auditoría integral queda cubierta en sus capas principales:

1. arquitectura
2. backend
3. frontend
4. multi-tenant y seguridad
5. contratos y consistencia
6. testing
7. documentación y backlog

A partir de acá, el siguiente paso correcto ya NO es seguir auditando sin fin.

El siguiente paso correcto es:

> **armar un roadmap de remediación priorizado a partir de los P0 y P1 detectados**.

---

## Key Learnings

1. El principal problema documental de Walos no es cantidad, sino pérdida de una fuente única de verdad.
2. `PENDING.md` hoy no es confiable como backlog operativo porque se contradice con el código y consigo mismo.
3. Los READMEs principales quedaron muy por detrás del estado real del producto y necesitan saneamiento urgente.
