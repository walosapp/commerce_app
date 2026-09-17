# WALOS V1 — CONTRATO MAESTRO DE CIERRE

> **Versión**: 1.0.0-FINAL
> **Fecha**: 2026-09-17
> **Estado**: ✅ **PRE-PUSH VERIFIED — PUSH/DEPLOY PENDING**
> **Baseline producción**: `7699336e3b236d863a2e045c15457cf666c66a1a`
> **Platform Closure**: `e702214cc3cde583976ef0241a96581972525fb3`

---

## 0. CHECKPOINT DE IMPLEMENTACIÓN — 2026-09-17

Los seis commits locales de Walos V1 forman una cadena lineal sobre `origin/main`. Los cuatro mensajes que contenían atribución no permitida fueron reescritos sin cambiar el contenido funcional de los commits. El push y el despliegue continúan pendientes; no se creó tag.

| Bloque | Commit local | Estado | Evidencia principal |
|--------|--------------|--------|---------------------|
| Platform Closure | `e702214` | ✅ PRE-PUSH VERIFIED | Features por comercio y cierre de plataforma |
| Seguridad y roles | `bdb6224` | ✅ PRE-PUSH VERIFIED | Matriz de roles, aislamiento IA, separación mesa/facturación y rechazo de `add_stock` |
| Password lifecycle | `eb8df3f` | ✅ PRE-PUSH VERIFIED | Security stamp y rotación/revocación de tokens |
| POS/B1.3 | `d1c06f1` | ✅ PRE-PUSH VERIFIED | Idempotencia POS; esquema 018 ya presente en producción |
| Refunds preparados | `46a0a3a` | ✅ PRE-PUSH VERIFIED | Ledger por `source_order_item_id`; esquema 020 ya presente en producción |
| Cierre de caja impreso | `723e598` | ✅ PRE-PUSH VERIFIED — UAT PENDING | Agent 74/74, build Release; sin migración 021 |
| Importador de preparados (Fase 6) | — | ⚪ OUT OF THIS PUSH — NOT IMPLEMENTED | Fuera del corte actual; no es dependencia de los seis commits |

Gates ejecutados: backend seguro **768 pass / 0 fail**, frontend **312 pass / 0 fail**, Walos Print Agent **74 pass / 0 fail** y builds Release/producción verdes. Los **264 escenarios PostgreSQL** continúan omitidos porque no existe un entorno aislado autorizado; no se usó la base productiva para pruebas.

Base de datos: las migraciones **018**, **019** y **020** ya fueron aplicadas manualmente en la única base productiva. No deben reaplicarse, revertirse ni ejecutarse mediante este corte. La migración **021 no es necesaria**.

Seguridad de dependencias frontend: las tres vulnerabilidades críticas reportadas por `npm audit` fueron clasificadas como **DEV-ONLY / no alcanzables por el bundle o runtime productivo**. No se ejecutó `npm audit fix --force`.

Quedan preservados fuera de estos commits tres cambios antiguos de infraestructura de tests y la corrección documental de hardware; no deben mezclarse automáticamente con el siguiente bloque.

---

## 1. OBJETIVO DE WALOS V1

### 1.1 Definición de V1 Cerrada

**Walos V1** es el **primer baseline comercial estable, instalable y vendible** del sistema POS/SaaS multi-tenant.

**V1 Cerrada significa**:
- ✅ Operación sin defectos críticos conocidos
- ✅ Aislamiento multi-tenant verificado
- ✅ Roles y permisos coherentes y documentados
- ✅ Módulos habilitables/deshabilitables por comercio
- ✅ Soporte real de restaurante/POS/caja/inventario
- ✅ Hardware POS básico funcional
- ✅ Seguridad mínima comercial
- ✅ Deudas aceptadas explícitamente documentadas
- ✅ Instalable en producción con confianza
- ✅ Vendible a clientes reales

**V1 NO significa**:
- ❌ Todas las capacidades futuras implementadas
- ❌ Cero deuda técnica
- ❌ UX perfecta en todos los módulos
- ❌ Todas las integraciones posibles
- ❌ Facturación electrónica
- ❌ WhatsApp
- ❌ IA avanzada

---

## 2. ALCANCE INCLUIDO EN V1

### 2.1 Módulos Operativos CORE

| Módulo | Estado | Feature Code | Default ON | Configurable | Prioridad |
|--------|--------|--------------|------------|--------------|-----------|
| **Dashboard** | ✅ DONE | `dashboard` | ✅ Sí | ❌ No (obligatorio) | V1 CORE |
| **Inventario** | 🟡 RELEASE GATE | `inventory` | ✅ Sí | ✅ Sí | V1 CORE |
| **Recetas** | ✅ DONE | `inventory` | ✅ Sí | ✅ Sí | V1 CORE |
| **Restaurante** | 🟡 RELEASE GATE | `restaurant` | ✅ Sí | ✅ Sí | V1 CORE |
| **POS** | 🟡 RELEASE GATE | `pos` | ✅ Sí | ✅ Sí | V1 CORE |
| **Caja** | 🟡 RELEASE GATE | `cash` | ✅ Sí | ✅ Sí | V1 CORE |
| **Créditos** | ✅ DONE | `restaurant`/`pos` | ✅ Sí | ✅ Sí | V1 CORE |
| **Refunds** | 🟡 RELEASE GATE | `restaurant`/`pos` | ✅ Sí | ✅ Sí | V1 CORE |
| **Compras** | ✅ DONE | `purchases` | ✅ Sí | ✅ Sí | V1 CORE |
| **Proveedores** | ✅ DONE | `suppliers` | ✅ Sí | ✅ Sí | V1 CORE |
| **Delivery** | ✅ DONE | `delivery` | ✅ Sí | ✅ Sí | V1 CORE |
| **Finanzas** | ✅ DONE | `finance` | ✅ Sí | ✅ Sí | V1 CORE |

### 2.2 Plataforma PLATFORM

| Componente | Estado | Descripción | Prioridad |
|------------|--------|-------------|-----------|
| **Multi-tenant** | ✅ PRE-PUSH VERIFIED | Aislamiento por `company_id` | V1 PLATFORM |
| **Usuarios** | ✅ DONE | CRUD de usuarios por tenant | V1 PLATFORM |
| **Roles** | ✅ PRE-PUSH VERIFIED | 6 roles canónicos | V1 PLATFORM |
| **Sucursales** | ✅ DONE | Gestión de branches | V1 PLATFORM |
| **Branding** | ✅ DONE | Logo/nombre por tenant | V1 PLATFORM |
| **PWA** | ✅ DONE | Manifest + iconos | V1 PLATFORM |
| **Features por comercio** | ✅ DONE | Habilitar/deshabilitar módulos | V1 PLATFORM |
| **dev** | ✅ DONE | Superusuario técnico | V1 PLATFORM |
| **platform_admin** | ✅ DONE | Admin SaaS | V1 PLATFORM |

### 2.3 Hardware HARDWARE

| Componente | Estado | Descripción | Prioridad |
|------------|--------|-------------|-----------|
| **Walos Print Agent** | ✅ DONE | Servicio Windows .NET 10 | V1 HARDWARE |
| **Impresión térmica** | ✅ DONE | ESC/POS 58mm (Digital POS DIG-58IIA) | V1 HARDWARE |
| **Cajón** | ✅ DONE | Apertura por pulso | V1 HARDWARE |
| **Pairing** | ✅ DONE | Código de 6 dígitos | V1 HARDWARE |
| **Instalador Windows** | ✅ DONE | Inno Setup (Walos-Agent-Setup.exe) | V1 HARDWARE |
| **Impresión postventa** | ✅ DONE | Auto-print después de checkout | V1 HARDWARE |

**Nota**: UAT física aprobada con impresora Digital POS DIG-58IIA de 58mm en segundo equipo.

### 2.4 IA OPTIONAL

| Componente | Estado | Descripción | Prioridad |
|------------|--------|-------------|-----------|
| **Asistente IA básico** | 🟡 RELEASE GATE | Consultas inventario/búsqueda | V1 OPTIONAL |
| **IA OFF por defecto** | ✅ DONE | `default_enabled = FALSE` | V1 OPTIONAL |
| **Tenant isolation IA** | ✅ PRE-PUSH VERIFIED | Validación `company_id` en queries | V1 OPTIONAL |
| **add_stock deshabilitado** | ✅ PRE-PUSH VERIFIED | Tool no ejecutable | V1 OPTIONAL |

---

## 3. ALCANCE EXCLUIDO DE V1 (V2+)

### 3.1 Funcionalidades V2

| Funcionalidad | Razón de exclusión | Target |
|---------------|-------------------|--------|
| **WhatsApp** | Integración compleja, no crítica | V2.0 |
| **Facturación electrónica** | Regulatorio complejo por país | V2.0 |
| **Clientes** | CRM no es core POS | V2.0 |
| **Ticket customization** | UX avanzada, no bloqueante | V2.0 |
| **IA avanzada** | Creación conversacional de recetas | V2.1 |
| **Employees** | Módulo de RRHH completo | V2.1 |
| **Document module** | Gestión documental | V2.2 |
| **Updater automático** | Requiere Authenticode | V2.0 |
| **Planes/billing completo** | SaaS billing avanzado | V2.0 |
| **Báscula** | Hardware adicional | V2.1 |
| **Lectores barcode** | Hardware adicional | V2.1 |
| **Device Agent general** | Abstracción hardware | V2.2 |
| **Contraseñas temporales** | must_change_password | V2.0 |

---

## 4. ROLES Y PERMISOS — CONTRATO DEFINITIVO

### 4.1 Matriz de Roles × Capacidades (AUTORITATIVA)

| Capacidad | super_admin | manager | cashier | waiter | dev | platform_admin |
|-----------|-------------|---------|---------|--------|-----|----------------|
| **Dashboard** | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ |
| **Inventario (módulo)** | ✅ RW | ✅ RW | ❌ | ❌ | ✅ RW | ❌ |
| **Catálogo productos (venta)** | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Recetas** | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ |
| **Restaurante (mesas)** | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Facturar/cobrar** | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ |
| **POS** | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ |
| **Caja** | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ |
| **Créditos** | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ |
| **Refunds** | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ |
| **Compras** | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ |
| **Proveedores** | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ |
| **Delivery/Pedidos** | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Finanzas** | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ |
| **IA** | ✅* | ✅* | ❌ | ❌ | ✅* | ❌ |
| **Usuarios** | ✅ | ✅† | ❌ | ❌ | ✅ | ❌ |
| **Configuración** | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ |
| **Administración SaaS** | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |

**Notas**:
- * IA requiere además feature `ai` habilitada para el comercio.
- † `manager` administra usuarios ordinarios y roles autorizados, pero la identidad y autoridad de `super_admin` están protegidas jerárquicamente.

### 4.2 Descripción de Roles

#### super_admin
**Propósito**: Autoridad máxima y administrador completo del tenant (normalmente el dueño del establecimiento).

**Capacidades**:
- Dashboard con información financiera/resumen de ventas
- Inventario completo (lectura/escritura)
- Recetas
- Restaurante completo (mesas, facturación, cobro)
- POS
- Caja (abrir/cerrar, movimientos)
- Créditos y refunds
- Compras y proveedores
- Delivery/Pedidos
- Finanzas
- IA (si feature habilitada)
- Usuarios
- Configuración del comercio

**Restricciones**:
- Limitado a su propio `company_id`
- NO puede acceder a plataforma SaaS
- NO puede administrar otros tenants

---

#### manager / gerente
**Propósito**: Administrador con equivalencia operativo-administrativa a `super_admin` dentro del tenant, con `super_admin` protegido jerárquicamente.

**Capacidades**:
- Dashboard
- Inventario
- Recetas
- Restaurante
- POS
- Caja
- Créditos
- Refunds
- Compras
- Proveedores
- Delivery
- Finanzas
- IA, cuando la feature esté habilitada
- Configuración
- Crear usuarios
- Editar usuarios ordinarios
- Resetear contraseñas de usuarios ordinarios
- Asignar roles inferiores o autorizados

**Justificación**:
- En algunos comercios el dueño será también gerente
- En otros casos, una sucursal tendrá un gerente propio que debe poder administrar usuarios/configuración sin depender del `super_admin`
- Para V1 existe equivalencia operativo-administrativa, con `super_admin` protegido jerárquicamente como autoridad máxima/dueño del tenant

**Restricciones**:
- Limitado a su propio `company_id`
- NO puede acceder a plataforma SaaS
- NO puede crear otro `super_admin`
- NO puede promover un usuario a `super_admin`
- NO puede degradar un `super_admin`
- NO puede editar privilegios de un `super_admin`
- NO puede resetear la contraseña de un `super_admin`
- NO puede eliminar un `super_admin`

---

#### cashier / cajero
**Propósito**: Operador de caja y ventas.

**Capacidades**:
- Restaurante (facturación/cobro de mesas)
- POS (venta rápida)
- Caja (abrir/cerrar, movimientos)
- Créditos y refunds (con trazabilidad obligatoria)
- Delivery/Pedidos (operación)
- Catálogo/búsqueda de productos (necesaria para vender)

**NO puede**:
- Acceder al Dashboard (contiene información financiera global)
- Acceder al módulo administrativo de Inventario
- Modificar inventario
- Gestionar recetas
- Compras
- Proveedores
- Finanzas
- IA
- Usuarios
- Configuración

**Importante**:
- `cashier` SÍ puede realizar créditos y refunds
- Debe existir trazabilidad obligatoria: `user_id`, fecha/hora, caja/sucursal, orden, refund/crédito realizado
- "Consultar productos para vender" ≠ "acceder al módulo Inventario"

---

#### waiter / mesero
**Propósito**: Operador de restaurante (mesas).

**Capacidades**:
- Restaurante (crear/editar mesas, agregar productos, enviar pedido)
- Delivery/Pedidos (operación básica)
- Catálogo/búsqueda de productos (para agregar a pedidos)

**NO puede**:
- Acceder al Dashboard
- Facturar/cobrar mesas
- POS
- Caja
- Créditos
- Refunds
- Inventario (módulo administrativo)
- Recetas
- Compras
- Proveedores
- Finanzas
- IA
- Usuarios
- Configuración

**Separación de responsabilidades**:
- Debe separarse capacidad `SalesTableOperator` (crear/editar pedidos) de `SalesInvoiceOperator` (facturar/cobrar)

---

#### dev
**Propósito**: Superusuario técnico de Walos para testing completo.

**Capacidades**:
- Acceso total a todos los módulos
- Bypass de features (para testing)
- Acceso a múltiples tenants (con trazabilidad)
- Administración de plataforma

**Restricciones**:
- DEBE pertenecer a `WALOS-SYSTEM-001`
- DEBE tener `isPlatformAdmin = true`
- Acciones auditadas
- Mantiene aislamiento tenant y trazabilidad

---

#### platform_admin
**Propósito**: Administrador del SaaS Walos.

**Capacidades**:
- Gestión de comercios (tenants)
- Gestión de sucursales cross-tenant
- Configuración de features por comercio
- Administración de usuarios platform
- Onboarding

**Restricciones**:
- DEBE pertenecer a `WALOS-SYSTEM-001`
- NO es operador automático de todos los tenants
- NO accede a módulos operativos (Dashboard, Ventas, etc.) de los clientes
- Solo administración de plataforma

---

### 4.3 Dashboard — Clarificación Crítica

**Dashboard contiene**:
- Información global del negocio
- Información financiera
- Resumen de ventas
- Métricas operativas

**Acceso por rol**:
- `super_admin`: ✅ SÍ
- `manager`: ✅ SÍ
- `cashier`: ❌ NO (contiene información financiera que no debe ver)
- `waiter`: ❌ NO
- `dev`: ✅ SÍ
- `platform_admin`: ❌ NO (como dashboard operativo del tenant)

**Importante**: Diferenciar claramente:
- **Feature habilitada** (a nivel de comercio) — Dashboard siempre ON
- **Rol autorizado** (a nivel de usuario) — Solo admin/manager/dev

---

## 5. FEATURES POR COMERCIO — CONTRATO

### 5.1 Matriz de Features

| Feature Code | Nombre | Default ON | Mandatory | Configurable | Independiente |
|--------------|--------|------------|-----------|--------------|---------------|
| `dashboard` | Dashboard | ✅ | ✅ | ❌ | N/A |
| `inventory` | Inventario | ✅ | ❌ | ✅ | ✅ |
| `restaurant` | Restaurante | ✅ | ❌ | ✅ | ✅ |
| `pos` | POS | ✅ | ❌ | ✅ | ✅ |
| `cash` | Caja | ✅ | ❌ | ✅ | ❌ (dep: sales) |
| `purchases` | Compras | ✅ | ❌ | ✅ | ✅ |
| `suppliers` | Proveedores | ✅ | ❌ | ✅ | ✅ |
| `delivery` | Delivery | ✅ | ❌ | ✅ | ✅ |
| `finance` | Finanzas | ✅ | ❌ | ✅ | ✅ |
| `ai` | Asistente IA | ❌ | ❌ | ✅ | ✅ |

### 5.2 Independencia de Restaurante y POS

**Requisito V1**: Un comercio debe poder tener:
- ✅ Restaurante ON / POS OFF
- ✅ Restaurante OFF / POS ON
- ✅ Ambos ON
- ✅ Ambos OFF (si no vende)

**Validación**:
- ✅ Backend: `[RequireAnyFeature(WalosFeatures.Restaurant, WalosFeatures.Pos)]` en `SalesController`
- ✅ Frontend: Features independientes en config
- ✅ Sidebar: Muestra solo features habilitadas

**Estado**: ✅ CUMPLE

### 5.3 Dependencias de Features

#### Caja → Ventas
Si `require_cash_register = true` en `core.companies`:
- Habilitar `restaurant` o `pos` requiere que `cash` esté habilitada
- Deshabilitar `cash` requiere que `restaurant` y `pos` estén deshabilitadas

**Validación**: ✅ Implementado en `CompanyFeatureRepository.cs:161-211`

---

## 6. NOMBRES Y UX DE MÓDULOS

### 6.1 Nomenclatura Definitiva

| Path interno | Nombre UI V1 | Feature code | Estado |
|--------------|--------------|--------------|--------|
| `/` | Dashboard | `dashboard` | ✅ OK |
| `/inventory` | Inventario | `inventory` | ✅ OK |
| `/sales` | **Restaurante** | `restaurant` | ✅ OK |
| `/pos-deli` | **POS** | `pos` | ✅ OK |
| `/cash` | Caja | `cash` | ✅ OK |
| `/purchases` | Compras | `purchases` | ✅ OK |
| `/suppliers` | Proveedores | `suppliers` | ✅ OK |
| `/delivery` | Delivery | `delivery` | ✅ OK |
| `/finance` | Finanzas | `finance` | ✅ OK |
| `/ai-assistant` | Asistente IA | `ai` | ✅ OK |

### 6.2 Cambios Requeridos (RELEASE GATE)

#### ✅ RG-UX01: Renombrar "Ventas" a "Restaurante" en UI

**Estado**: Implementado y verificado en `bdb6224`.

**Justificación**: "Ventas" es ambiguo. El módulo es específicamente para **mesas de restaurante**.

---

#### ✅ RG-UX02: Renombrar "POS Deli" a "POS" en UI

**Estado**: Implementado y verificado en `bdb6224`.

**Justificación**: "POS Deli" es confuso. El módulo es **POS de mostrador**, no específicamente delivery.

**Nota**: NO es necesario renombrar paths internos (`/pos-deli`) si genera riesgo. Solo UI visible.

---

## 7. CICLO DE CONTRASEÑAS — CONTRATO V1

### 7.1 Cambiar Mi Contraseña (RELEASE GATE)

**Disponible para**: TODO usuario autenticado

**Endpoint**: `POST /api/v1/auth/change-password`

**Request**:
```json
{
  "currentPassword": "string",
  "newPassword": "string",
  "confirmPassword": "string"
}
```

**Validaciones**:
- Contraseña actual correcta
- Nueva contraseña != actual
- Nueva contraseña cumple política (min 8 caracteres, complejidad)
- `newPassword === confirmPassword`

**Comportamiento**:
- Hash nueva contraseña (bcrypt)
- Actualizar `password_hash` en `core.users`
- Invalidar refresh tokens anteriores
- Mantener sesión actual activa
- Log de auditoría

**Estado**: ✅ **PRE-PUSH VERIFIED** — Commit local `eb8df3f`; validación focal y suites ejecutables verdes

---

### 7.2 Reset Administrativo (EXISTENTE — Requiere Auditoría)

**Disponible para**: `super_admin`, `manager` (del mismo tenant y respetando la protección jerárquica de `super_admin`)

**Endpoint**: `POST /api/v1/users/{userId}/reset-password` (EXISTENTE)

**Request**:
```json
{
  "newPassword": "string"
}
```

**Validaciones requeridas**:
- Usuario objetivo pertenece al mismo `company_id`
- Usuario actual tiene permiso `Users`
- NO puede resetear su propia contraseña (usar cambiar contraseña)
- Un `manager` NO puede resetear la contraseña de un `super_admin`
- Nueva contraseña cumple política

**Comportamiento esperado**:
- Hash nueva contraseña
- Actualizar `password_hash`
- Invalidar TODOS los refresh tokens del usuario objetivo
- Forzar re-login del usuario objetivo
- Log de auditoría (quién reseteó a quién)

**Estado**: ✅ **AUDITED AND HARDENED** — Commit local `eb8df3f`; aislamiento tenant, protección de self-reset, protección jerárquica de `super_admin` y revocación de sesiones

---

### 7.3 Usuario Inicial del Comercio

**Comportamiento actual**: Al crear un comercio, se crea un usuario `super_admin` inicial que NO muestra opción de reset en UI.

**Análisis**:
- La UI evita reset del `currentUser` para prevenir auto-bloqueo
- Esto es correcto
- El usuario inicial puede cambiar su propia contraseña con "Cambiar mi contraseña"

**Decisión V1**: Mantener comportamiento actual. Agregar tooltip explicativo:

```jsx
{user.id === currentUser.id ? (
  <Tooltip content="Usa 'Cambiar mi contraseña' en tu perfil">
    <Button disabled>Reset</Button>
  </Tooltip>
) : (
  <Button onClick={() => handleReset(user)}>Reset</Button>
)}
```

**Estado**: 🟡 ACCEPTED DEBT — UX improvement para V1.1

---

### 7.4 Contraseña Temporal (V2)

**Fuera de V1**:
- Generar contraseña temporal al crear usuario
- Flag `must_change_password`
- Forzar cambio en primer login

**Razón**: No crítico para V1. Implementar en V2.0.

---

## 8. CIERRE DE CAJA + IMPRESIÓN — CONTRATO V1

### 8.1 Flujo de Cierre (RELEASE GATE)

**Requisito cliente**: Después de cerrar caja, debe imprimirse ticket de cierre automáticamente.

**Flujo V1**:
```
1. Usuario ingresa datos de cierre (efectivo contado, observaciones)
2. Backend persiste cierre en `cash.registers` (estado = 'closed')
3. Backend retorna datos del cierre
4. Frontend envía request a Walos Print Agent
5. Walos Agent imprime ticket
6. Si impresión falla, NO se revierte el cierre
7. Usuario puede reimprimir desde histórico
```

**Principio**: **Persistencia primero, impresión después**.

**Estado**: ✅ **PRE-PUSH VERIFIED — UAT PENDING** — Commit local `723e598`; Agent, frontend y backend verdes; UAT física del nuevo ticket pendiente

**Migración 021**: No requerida; los datos necesarios ya se persistían.

---

### 8.2 Ticket de Cierre — Contenido Mínimo

```
┌─────────────────────────────────────┐
│         CIERRE DE CAJA              │
├─────────────────────────────────────┤
│ Comercio: [Nombre]                  │
│ Sucursal: [Nombre]                  │
│ Caja: [Nombre]                      │
│ Usuario: [Nombre]                   │
├─────────────────────────────────────┤
│ Apertura: [Fecha Hora]              │
│ Cierre:   [Fecha Hora]              │
├─────────────────────────────────────┤
│ Saldo Inicial:      $[amount]       │
│                                     │
│ VENTAS                              │
│ Cantidad:           [count]         │
│ Total:              $[amount]       │
│                                     │
│ POR MEDIO DE PAGO                   │
│ - Efectivo:         $[amount]       │
│ - Tarjeta:          $[amount]       │
│ - Transferencia:    $[amount]       │
│                                     │
│ DEVOLUCIONES                        │
│ Total:              $[amount]       │
│                                     │
│ ENTRADAS                            │
│ Total:              $[amount]       │
│                                     │
│ SALIDAS                             │
│ Total:              $[amount]       │
│                                     │
│ EFECTIVO ESPERADO:  $[calculated]   │
│ EFECTIVO CONTADO:   $[counted]      │
│ DIFERENCIA:         $[diff]         │
│                                     │
│ Observaciones:                      │
│ [notes]                             │
└─────────────────────────────────────┘
```

**NO incluir**: Detalle de todas las ventas individuales (sería muy largo).

**Detalle de ventas**: Reporte separado opcional (V2).

**Decisión de producto**: ✅ APROBADA — Resumen, no detalle completo.

---

### 8.3 Reimpresión desde Histórico (RELEASE GATE)

**Endpoint**: `GET /api/v1/cash/registers/{id}/print`

**Comportamiento**:
- Consulta datos del cierre desde DB
- Retorna datos formateados para impresión
- Frontend envía a Walos Agent
- NO modifica estado del cierre

**Estado**: ✅ **PRE-PUSH VERIFIED — UAT PENDING** — Commit local `723e598`; la reimpresión desde histórico usa un nuevo job físico, sin mutar caja ni abrir cajón

---

## 9. REFUNDS DE PREPARADOS — CONTRATO V1

### 9.1 Problema Identificado

**Referencia**: `docs/AUDITORIA-REFUND-REVISION-CRITICA.md`

**Problema**: Refunds de productos preparados restituyen cantidades incorrectas cuando:
- La receta cambia después de la venta
- La orden contiene múltiples preparados con ingredientes compartidos
- Se hace refund parcial de un item

**Causa raíz**: `inventory.movements` agrupa consumo por ingrediente a nivel de orden, sin correlación con `order_item_id`.

### 9.2 Solución Arquitectónica (RELEASE GATE)

**Decisión de producto**: ✅ APROBADA — Implementar en V1.0.0

**Migración**: `020_refund_preparados_source_item.sql`

**Cambios requeridos**:
```sql
ALTER TABLE inventory.movements
    ADD COLUMN source_order_item_id BIGINT
    REFERENCES sales.order_items(id) ON DELETE SET NULL;

CREATE INDEX idx_inv_movements_source_item
    ON inventory.movements (source_order_item_id)
    WHERE source_order_item_id IS NOT NULL;
```

**Cambios de código**:
- `SaleInventoryPlanBuilder.cs`: Emitir movimientos por item, no agregados
- `RefundRepository.cs`: Consultar movimientos por `source_order_item_id`
- `InventoryMovementPlan`: Agregar campo `SourceOrderItemId`

**Estado**: ✅ **PRE-PUSH VERIFIED** — Commit local `46a0a3a`; migración 020 ya aplicada manualmente en producción

**Evidencia**: cálculo parcial/total, receta modificada, múltiples refunds, ingrediente compartido y legacy ambiguo cubiertos. La integración PostgreSQL automatizada se omitió por no existir un entorno aislado autorizado.

---

### 9.3 Ventas Legacy

**Problema**: Ventas anteriores a la migración NO tienen `source_order_item_id`.

**Estrategia V1**:
- Si `source_order_item_id IS NULL` Y orden tiene múltiples preparados → **Bloquear refund**, requerir ajuste manual
- Si `source_order_item_id IS NULL` Y orden tiene solo 1 preparado → Permitir con fallback a receta actual + warning en logs

**Justificación**: Preferible bloquear explícitamente que restaurar stock incorrecto.

---

## 10. IMPORTACIÓN DE INVENTARIO / PREPARADOS — CONTRATO V1

### 10.1 Problema Identificado

**Caso real**: Excel permite importar productos `prepared` sin receta.

**Resultado**:
- Importación aparece OK
- Producto queda incompleto
- Venta posteriormente se bloquea (correcto)

**Problema**: UX no distingue productos listos vs pendientes de receta.

### 10.2 Contrato V1 (RELEASE GATE)

**Al finalizar importación**, reportar:
```
✅ 45 productos importados
⚠️ 12 productos preparados sin receta
✅ 33 productos listos para venta
```

**En listado de inventario**, distinguir:
- ✅ Producto simple → Badge "Listo"
- ✅ Producto preparado con receta → Badge "Listo"
- 🔴 Producto preparado SIN receta → Badge "Receta pendiente"

**Validación de venta**:
- Producto preparado sin receta → **Bloquear venta** con mensaje claro:
  ```
  "El producto [Nombre] es preparado pero no tiene receta configurada.
   Configure la receta antes de venderlo."
  ```

**Estado**: ⚪ **OUT OF THIS PUSH — NOT IMPLEMENTED** — Fase 6 permanece fuera del corte actual

**Estimación**: 2 horas

**Decisión**: Mantener `prepared sin receta válida = no vendible` como comportamiento correcto.

**NO incluir en V1**: IA para creación de recetas.

---

## 11. POS / POS-DELI — AUDITORÍA Y CONTRATO

### 11.1 Release Gates POS

**Requisitos V1**:
- ✅ Cero GET 400/403 inesperados durante flujo normal
- ✅ Cero requests repetitivos innecesarios
- ✅ Autorización correcta por rol
- ✅ Caja integrada
- ✅ Checkout estable
- ✅ Idempotencia B1.3 cerrada
- ✅ Consola limpia durante flujo normal
- ✅ Feedback de errores entendible
- ✅ UX mínima comercial

**Estado**: ✅ **PRE-PUSH VERIFIED — UAT PENDING** — Commit local `d1c06f1`; migración 018 ya aplicada manualmente en producción. La integración PostgreSQL automatizada se omitió por no existir un entorno aislado autorizado.

---

### 11.2 UX Mínima V1

**Requisitos**:
- ✅ Catálogo de productos visible
- ✅ Búsqueda funcional
- ✅ Categorías (si existen)
- ✅ Carrito con totales
- ✅ Métodos de pago
- ✅ Botón checkout primario
- ✅ Feedback de éxito/error
- ✅ Estados vacíos claros
- ✅ Responsividad táctil básica

**Mejoras V2**:
- Grid vs lista toggle
- Favoritos
- Descuentos rápidos
- Teclado numérico virtual
- Shortcuts de teclado

**Estado**: 🟡 **RELEASE GATE** — UX review requerida

---

## 12. HARDWARE V1 — ESTADO FINAL

### 12.1 Componentes Aprobados

| Componente | Versión | Estado | UAT |
|------------|---------|--------|-----|
| Walos Print Agent | .NET 10 | ✅ DONE | ✅ 2 equipos |
| Impresión ESC/POS | 58mm Digital POS DIG-58IIA | ✅ DONE | ✅ Aprobado |
| Cajón por pulso | - | ✅ DONE | ✅ Aprobado |
| Pairing 6 dígitos | - | ✅ DONE | ✅ Aprobado |
| Instalador Inno Setup | Walos-Agent-Setup.exe | ✅ DONE | ✅ Aprobado |
| Autoarranque Windows | - | ✅ DONE | ✅ Aprobado |
| Auto-print postventa | - | ✅ DONE | ✅ Aprobado |

**Decisión V1**: ✅ Hardware APROBADO. No reabrir salvo defecto real.

**Especificaciones físicas**:
- Impresora: Digital POS DIG-58IIA
- Ancho: 58mm
- Protocolo: ESC/POS
- Conexión: USB
- Loopback: 127.0.0.1:17831

### 12.2 Componentes V2+

| Componente | Target |
|------------|--------|
| Updater automático | V2.0 |
| Authenticode signing | V2.0 |
| Báscula | V2.1 |
| Lectores barcode | V2.1 |
| Device Agent general | V2.2 |

---

## 13. IA V1 — CONTRATO FINAL

### 13.1 Estado Actual

| Aspecto | Estado | Notas |
|---------|--------|-------|
| OFF por defecto | ✅ DONE | `default_enabled = FALSE` |
| Tenant isolation | ✅ PRE-PUSH VERIFIED | Validación `company_id` en queries |
| User isolation | ✅ DONE | Sesiones por usuario |
| Tool permissions | ✅ DONE | Solo tools de lectura |
| add_stock deshabilitado | ✅ PRE-PUSH VERIFIED | Tool inejecutable |
| Feature enforcement | ✅ DONE | `[RequireFeature(WalosFeatures.Ai)]` |

### 13.2 Capacidades V1

**Permitido**:
- ✅ Búsqueda de productos
- ✅ Consulta de stock
- ✅ Consulta de recetas
- ✅ Información general del inventario

**Prohibido**:
- ❌ Modificar inventario (`add_stock`)
- ❌ Crear productos
- ❌ Modificar recetas
- ❌ Acceso cross-tenant

### 13.3 Roles Permitidos

**Cuando feature `ai` está ON**:
- ✅ `super_admin`
- ✅ `manager`
- ✅ `dev`
- ❌ `cashier`
- ❌ `waiter`
- ❌ `platform_admin`

### 13.4 Roadmap IA V2+

**V2.1 — IA Avanzada**:
- Creación conversacional de recetas
- Detección de ingredientes desde descripción
- Matching con inventario existente
- Cálculo de costo automático
- Sugerencia de margen y precio
- Confirmación antes de escritura
- NUNCA inventar SKU/productos/datos

**Estado V2.1**: Especificación lista, implementación V2.

---

## 14. MIGRACIONES — ORDEN Y ESTADO

### 14.1 Migraciones Aplicadas (Producción)

| # | Nombre | Estado |
|---|--------|--------|
| 001-017 | core, auth, inventory, sales, etc. | ✅ Aplicadas |
| 018 | pos_sale_idempotency | ✅ Aplicada manualmente |
| 019 | company_features | ✅ Aplicada manualmente |
| 020 | refund_preparados_source_item | ✅ Aplicada manualmente |

### 14.2 Estado final del corte V1

| # | Nombre | Estado | Release Gate | Estimación |
|---|--------|--------|--------------|------------|
| **018** | pos_sale_idempotency | ✅ APPLIED MANUALLY | ✅ Sí | Commit `d1c06f1`; no reaplicar |
| **019** | company_features | ✅ APPLIED MANUALLY | ✅ Sí | Commit `e702214`; no reaplicar |
| **020** | refund_preparados_source_item | ✅ APPLIED MANUALLY | ✅ Sí | Commit `46a0a3a`; no reaplicar |
| **021** | cash_closing_improvements | ⚪ NOT REQUIRED | ❌ No | Datos existentes suficientes |

### 14.3 Migración 018 — B1.3 (Clarificación)

**Estado**: Trabajo B1.3 separado del Platform Closure

**Target**: V1.0.0

**Decisión**:
- B1.3 se mantiene separado para evitar contaminar commits
- La idempotencia POS es parte del cierre de POS V1
- Pre-push verificado; UAT posterior al deploy continúa pendiente
- NO mezclar accidentalmente con Platform Closure
- La migración 018 ya fue aplicada manualmente y no debe reaplicarse

**Razón**: Separación de commits ≠ separación de release. B1.3 ES parte de V1.

---

## 15. RELEASE GATES V1 — LISTA DEFINITIVA

### 15.1 Seguridad y Tenant Isolation

| ID | Gate | Severidad | Estado |
|----|------|-----------|--------|
| RG-SEC-01 | AiSessionRepository validación `company_id` | CRITICAL | ✅ IMPLEMENTED `bdb6224` |
| RG-SEC-02 | Roles frontend + backend coherentes | HIGH | ✅ IMPLEMENTED `bdb6224` |
| RG-SEC-03 | Tenant isolation verificado en todos los repositorios | HIGH | ✅ PRE-PUSH VERIFIED `bdb6224` |
| RG-SEC-04 | add_stock inejecutable | MEDIUM | ✅ IMPLEMENTED `bdb6224` |

### 15.2 Funcionalidad Core

| ID | Gate | Severidad | Estado |
|----|------|-----------|--------|
| RG-FUN-01 | Password self-change implementado | HIGH | ✅ PRE-PUSH VERIFIED `eb8df3f` |
| RG-FUN-02 | Refund preparados con `source_order_item_id` | HIGH | ✅ PRE-PUSH VERIFIED `46a0a3a`; schema aplicado |
| RG-FUN-03 | Cierre de caja con impresión post-persistencia | HIGH | ✅ PRE-PUSH VERIFIED `723e598`; UAT física pendiente |
| RG-FUN-04 | Reimpresión cierre desde histórico | MEDIUM | ✅ PRE-PUSH VERIFIED `723e598`; UAT física pendiente |
| RG-FUN-05 | POS estable + B1.3 + consola limpia | HIGH | ✅ PRE-PUSH VERIFIED `d1c06f1`; UAT pendiente |

### 15.3 UX

| ID | Gate | Severidad | Estado |
|----|------|-----------|--------|
| RG-UX-01 | Renombrar "Ventas" → "Restaurante" | MEDIUM | ✅ IMPLEMENTED `bdb6224` |
| RG-UX-02 | Renombrar "POS Deli" → "POS" | MEDIUM | ✅ IMPLEMENTED `bdb6224` |
| RG-UX-03 | Importador feedback receta pendiente | MEDIUM | ⚪ OUT OF THIS PUSH — FASE 6 NOT IMPLEMENTED |
| RG-UX-04 | Inventario badge "Receta pendiente" | LOW | ⚪ OUT OF THIS PUSH — FASE 6 NOT IMPLEMENTED |

### 15.4 Plataforma

| ID | Gate | Severidad | Estado |
|----|------|-----------|--------|
| RG-PLT-01 | Features por comercio operativas | HIGH | ✅ DONE |
| RG-PLT-02 | Migración 019 aplicada | HIGH | ✅ APPLIED MANUALLY |
| RG-PLT-03 | Migración 020 aplicada | HIGH | ✅ APPLIED MANUALLY |
| RG-PLT-04 | Migración 021 aplicada | MEDIUM | ⚪ NOT REQUIRED — datos persistidos existentes suficientes |

### 15.5 Hardware

| ID | Gate | Severidad | Estado |
|----|------|-----------|--------|
| RG-HW-01 | Hardware H1-H3.1 aprobado | HIGH | ✅ DONE |

### 15.6 Auditoría y QA

| ID | Gate | Severidad | Estado |
|----|------|-----------|--------|
| RG-QA-01 | Gates automatizados completos | HIGH | ✅ PRE-PUSH VERIFIED — 0 fallos |
| RG-QA-02 | Auditoría final de seguridad | CRITICAL | ✅ PRE-PUSH VERIFIED |
| RG-QA-03 | QA funcional completo en staging | HIGH | 🟡 PENDING |

---

## 16. DEUDA ACEPTADA V1

### 16.1 Deuda Técnica Permitida

| ID | Descripción | Impacto | Mitigación | Target |
|----|-------------|---------|------------|--------|
| D-01 | Refunds legacy sin `source_order_item_id` | Medio | Bloquear refund multi-preparado | V1.1 |
| D-02 | No hay rate limiting en endpoints | Bajo | Monitoreo manual | V2.0 |
| D-03 | Frontend en JavaScript (no TypeScript) | Bajo | Tests compensan | V2.0 |
| D-04 | No hay tests E2E completos | Medio | Tests unitarios + manuales | V1.1 |
| D-05 | No hay auditoría de cambios administrativos | Medio | Logs compensan | V2.0 |
| D-06 | No hay gestión de contraseñas temporales | Bajo | Reset manual funciona | V2.0 |
| D-07 | No hay métricas de uso de features | Bajo | No crítico para V1 | V2.0 |
| D-08 | Tooltip usuario inicial reset password | Bajo | Funcional sin tooltip | V1.1 |

### 16.2 Deuda UX Permitida

| ID | Descripción | Impacto | Target |
|----|-------------|---------|--------|
| UX-01 | POS grid vs lista toggle | Bajo | V2.0 |
| UX-02 | No hay loading skeletons en todas las páginas | Bajo | V2.0 |
| UX-03 | No hay tooltips explicativos en todas las acciones | Bajo | V2.0 |
| UX-04 | Importador no muestra preview antes de importar | Medio | V2.0 |

---

## 17. CRITERIOS DE ACEPTACIÓN V1

### 17.1 Seguridad

- [ ] Tenant isolation verificado en todos los repositorios
- [ ] Roles y permisos coherentes frontend + backend
- [ ] JWT secret validado en startup
- [ ] No hay endpoints sin autorización
- [ ] IA con tenant isolation verificado
- [ ] add_stock completamente deshabilitado

### 17.2 Funcionalidad

- [ ] Dashboard operativo para roles autorizados
- [ ] Inventario con lectura/escritura según rol
- [ ] Restaurante operativo (mesas, pedidos, facturación)
- [ ] POS operativo (venta rápida)
- [ ] Caja con cierre e impresión automática
- [ ] Créditos y refunds funcionales
- [ ] Refunds de preparados con correlación correcta
- [ ] Compras y proveedores operativos
- [ ] Delivery operativo
- [ ] Finanzas con reportes básicos
- [ ] Features habilitables/deshabilitables por comercio
- [ ] Importador de inventario con feedback claro
- [ ] Password self-change operativo

### 17.3 Hardware

- [ ] Walos Print Agent instalable y funcional
- [ ] Impresión térmica 58mm operativa
- [ ] Cajón por pulso operativo
- [ ] Pairing funcional
- [ ] Auto-print postventa operativo
- [ ] Cierre de caja impreso automáticamente

### 17.4 UX

- [ ] Nomenclatura coherente (Restaurante, POS)
- [ ] Sidebar muestra solo features habilitadas según rol
- [ ] Mensajes de error claros
- [ ] Estados vacíos informativos
- [ ] Feedback de acciones (loading, success, error)
- [ ] Responsividad básica (desktop + tablet)

### 17.5 Documentación

- [ ] README actualizado
- [ ] Guía de instalación
- [ ] Guía de roles y permisos
- [ ] Guía de features por comercio
- [ ] Guía de hardware
- [ ] Changelog V1
- [ ] Deuda documentada

---

## 18. ROADMAP POST-V1

### V1.1 (1-2 meses post-V1)
- Deuda técnica prioritaria
- Tests E2E completos
- Mejoras UX POS
- Contraseñas temporales
- Auditoría de cambios administrativos
- Tooltip usuario inicial

### V2.0 (3-6 meses post-V1)
- WhatsApp
- Facturación electrónica
- Clientes (CRM básico)
- Ticket customization
- Updater automático
- Planes/billing completo
- Rate limiting

### V2.1 (6-9 meses post-V1)
- IA avanzada (creación conversacional de recetas)
- Employees (RRHH)
- Báscula
- Lectores barcode

### V2.2 (9-12 meses post-V1)
- Document module
- Device Agent general
- Integraciones avanzadas

---

## 19. DECISIONES DE PRODUCTO — TODAS CERRADAS

### D-01: ¿Debe waiter poder facturar mesas?

**Decisión**: ✅ **NO** — Opción A aprobada

**Implementación**: Separar `SalesTableOperator` (crear/editar) de `SalesInvoiceOperator` (facturar)

**Estimación**: 1 hora

---

### D-02: ¿Debe cashier poder acceder al módulo Inventario?

**Decisión**: ✅ **NO** — Puede consultar catálogo/productos desde Restaurante/POS

**Clarificación**: "Consultar productos para vender" ≠ "acceder al módulo Inventario"

**Estimación**: 30 minutos

---

### D-03: ¿Incluir refund preparados en V1.0.0?

**Decisión**: ✅ **SÍ** — Opción A aprobada

**Arquitectura**: `source_order_item_id` en `inventory.movements`

**Estimación**: 8-10 horas

---

### D-04: ¿Nivel de detalle en ticket de cierre?

**Decisión**: ✅ **Resumen** — Opción A aprobada

**Detalle completo**: Reporte separado V2

**Estimación**: 0 horas (ya especificado)

---

### D-05: ¿Password self-change en V1?

**Decisión**: ✅ **SÍ** — Release gate V1

**Contraseñas temporales**: V2.0

**Estimación**: 2 horas

---

## 20. VEREDICTO FINAL

### ✅ **WALOS V1 PRE-PUSH VERIFIED — PUSH/DEPLOY PENDING**

**Razones**:
- ✅ Todas las decisiones de producto cerradas
- ✅ Alcance V1 definido y consensuado
- ✅ Roles y permisos autoritativos documentados
- ✅ Gates del corte actual implementados y verificados
- ✅ Deuda aceptada explícitamente documentada
- ✅ Hardware aprobado y no reabierto
- ✅ Migraciones 018/019/020 aplicadas manualmente; 021 no requerida
- ✅ Roadmap post-V1 claro

**Trabajo pendiente**:
- 🟡 Push sin force a `main`
- 🟡 Observación de deployments Vercel y Railway
- 🟡 UAT funcional y física posterior al deploy
- ⚪ Fase 6/importador fuera de este push y aún no implementada

**Próximos pasos**:
1. Push controlado a `main`
2. Verificar commit desplegado en Vercel y Railway
3. Ejecutar UAT
4. Evaluar tag posteriormente; no crearlo en este corte

---

## FIRMA DEL CONTRATO

**Auditor**: Arquitectónico, funcional, seguridad y producto
**Fecha**: 2026-09-17
**Versión**: 1.0.0-FINAL
**Estado**: ✅ **PRE-PUSH VERIFIED — PUSH/DEPLOY PENDING**

**Aprobaciones**:
- [x] Product Owner — Decisiones D-01 a D-05 cerradas
- [ ] Tech Lead — Revisión técnica pendiente
- [ ] QA Lead — Plan de testing pendiente
- [ ] Stakeholders — Alcance y timeline pendiente

---

**FIN DEL CONTRATO MAESTRO WALOS V1**
