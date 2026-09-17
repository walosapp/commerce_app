# WALOS V1 — RESUMEN EJECUTIVO DEL CONTRATO MAESTRO

> **Fecha**: 2026-09-17
> **Versión**: 1.0.0-FINAL
> **Estado**: ✅ **PRE-PUSH VERIFIED — PUSH/DEPLOY PENDING**

---

## CHECKPOINT 2026-09-17

| Fase | Commit local | Estado |
|------|--------------|--------|
| Platform Closure | `e702214` | ✅ Pre-push verificado |
| Seguridad y roles | `bdb6224` | ✅ Pre-push verificado |
| Password lifecycle | `eb8df3f` | ✅ Pre-push verificado |
| POS/B1.3 | `d1c06f1` | ✅ Pre-push verificado; UAT pendiente |
| Refund preparados | `46a0a3a` | ✅ Pre-push verificado |
| Cierre de caja impreso | `723e598` | ✅ Pre-push verificado; UAT física pendiente |
| Importador preparados (Fase 6) | — | ⚪ Fuera de este push; no implementado |

Gates ejecutados: backend seguro **768 pass / 0 fail**, frontend **312 pass / 0 fail**, Walos Agent **74 pass / 0 fail** y builds verdes. Los **264 escenarios PostgreSQL** se omitieron porque no existe un entorno aislado autorizado.

Las migraciones **018**, **019** y **020** ya están aplicadas manualmente en producción; no deben reaplicarse ni revertirse. **021 no es necesaria**. Las tres vulnerabilidades críticas de `npm audit` son **DEV-ONLY / no alcanzables en runtime productivo**. Push, deploy y UAT siguen pendientes; no se creó tag.

---

## 🎯 OBJETIVO

**Walos V1** = Primer baseline comercial estable, instalable y vendible.

**Significa**:
- ✅ Operación sin defectos críticos
- ✅ Multi-tenant seguro
- ✅ Roles coherentes
- ✅ Módulos configurables
- ✅ Hardware POS funcional

**NO significa**:
- ❌ Todas las features futuras
- ❌ Cero deuda técnica
- ❌ UX perfecta

---

## 📊 ESTADO ACTUAL

### Estado del corte

| Área | Estado |
|------|--------|
| Código de los seis commits | ✅ Pre-push verificado |
| Builds y suites ejecutables | ✅ Sin fallos |
| Esquema productivo 018/019/020 | ✅ Aplicado manualmente |
| Migración 021 | ⚪ No requerida |
| Vulnerabilidades críticas npm | ✅ Dev-only; no runtime productivo |
| Fase 6 / importador | ⚪ Fuera del push; no implementada |
| Push y deploy | 🟡 Pendientes |
| UAT | 🟡 Pendiente después del deploy |

---

## 👥 ROLES — CONTRATO DEFINITIVO (AUTORITATIVO)

### Matriz Simplificada

| Rol | Dashboard | Inventario | Restaurante | Facturar | POS | Caja | Finanzas | Config |
|-----|-----------|------------|-------------|----------|-----|------|----------|--------|
| **super_admin** | ✅ | ✅ RW | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **manager** | ✅ | ✅ RW | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **cashier** | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| **waiter** | ❌ | ❌ | ✅ Mesas | ❌ | ❌ | ❌ | ❌ | ❌ |
| **dev** | ✅ | ✅ RW | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **platform_admin** | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ SaaS |

### Decisiones Críticas Tomadas

#### Dashboard
- **Contiene**: Información financiera global, resumen de ventas, métricas operativas
- **Acceso**: Solo `super_admin`, `manager`, `dev`
- **NO acceso**: `cashier`, `waiter` (contiene información financiera que no deben ver)

#### waiter / mesero
- ✅ Puede: Crear/editar mesas, agregar productos, enviar pedido
- ❌ NO puede: Facturar/cobrar, POS, Caja, Créditos, Refunds, Inventario, Finanzas, IA, Config
- **Separación**: `SalesTableOperator` ≠ `SalesInvoiceOperator`

#### cashier / cajero
- ✅ Puede: Facturar/cobrar, POS, Caja, Créditos, Refunds, consultar catálogo productos
- ❌ NO puede: Dashboard, módulo Inventario, Compras, Proveedores, Finanzas, IA, Config
- **Importante**: "Consultar productos para vender" ≠ "acceder al módulo Inventario"

#### manager / gerente
- **Alcance**: equivalencia operativo-administrativa con `super_admin` dentro del tenant, con `super_admin` protegido jerárquicamente
- ✅ Puede: Dashboard, Inventario, Recetas, Restaurante, POS, Caja, Créditos, Refunds, Compras, Proveedores, Delivery, Finanzas, IA habilitada y Configuración
- ✅ Puede: Crear usuarios, editar usuarios ordinarios, resetear contraseñas de usuarios ordinarios y asignar roles inferiores/autorizados
- ❌ NO puede: Crear otro `super_admin`, promover a `super_admin`, degradar un `super_admin`, editar sus privilegios, resetear su contraseña ni eliminarlo
- **Jerarquía**: `super_admin` representa la autoridad máxima/dueño del tenant
- **Justificación**: Gerente de sucursal debe poder administrar sin depender del dueño

---

## 🎫 CIERRE DE CAJA — RELEASE GATE

### Flujo V1

```
1. Usuario cierra caja → Backend persiste
2. Backend retorna datos de cierre
3. Frontend → Walos Print Agent
4. Impresión automática de ticket
5. Si falla, NO revierte cierre
6. Reimpresión disponible desde histórico
```

### Ticket Mínimo

```
CIERRE DE CAJA
Comercio | Sucursal | Caja | Usuario
Apertura | Cierre
Saldo Inicial
Ventas (cantidad + total)
Por medio de pago (efectivo, tarjeta, etc.)
Devoluciones | Entradas | Salidas
Efectivo esperado vs contado
Diferencia
Observaciones
```

**Decisión**: ✅ Resumen, NO detalle completo de ventas

**Estado**: ✅ **PRE-PUSH VERIFIED — UAT PENDING** — Commit `723e598`; persistencia primero, Agent tipado, reimpresión y sin cajón. No se requiere migración 021.

---

## 🔄 REFUNDS DE PREPARADOS — RELEASE GATE

### Problema

Refunds restituyen cantidades incorrectas cuando:
- Receta cambia post-venta
- Orden tiene múltiples preparados con ingredientes compartidos
- Refund parcial de un item

### Solución V1

Agregar `source_order_item_id` a `inventory.movements`.

**Migración**: `020_refund_preparados_source_item.sql`

**Cambios**:
- `SaleInventoryPlanBuilder`: Emitir movimientos por item
- `RefundRepository`: Consultar por `source_order_item_id`

**Ventas legacy**: Bloquear refund si múltiples preparados.

**Decisión**: ✅ Implementar en V1.0.0

**Estado**: ✅ **PRE-PUSH VERIFIED** — Commit `46a0a3a`; migración 020 aplicada manualmente en producción.

---

## 📦 IMPORTADOR DE INVENTARIO — RELEASE GATE

### Problema

Excel permite importar productos `prepared` sin receta → Venta se bloquea.

### Solución V1

**Al finalizar importación**:
```
✅ 45 productos importados
⚠️ 12 productos preparados sin receta
✅ 33 productos listos para venta
```

**En listado**:
- ✅ Producto simple → Badge "Listo"
- ⚠️ Preparado con receta → Badge "Listo"
- 🔴 Preparado SIN receta → Badge "Receta pendiente"

**Estado**: ⚪ **OUT OF THIS PUSH — NOT IMPLEMENTED** — Fase 6 permanece fuera del corte actual; el comportamiento de dominio existente se conserva

---

## 🔐 PASSWORD LIFECYCLE — RELEASE GATE

### Cambiar Mi Contraseña

**Disponible para**: TODO usuario autenticado

**Endpoint**: `POST /api/v1/auth/change-password`

**Estado**: ✅ **PRE-PUSH VERIFIED** — Commit `eb8df3f`; cambio propio, política unificada y revocación de sesiones/tokens

---

### Reset Administrativo

**Estado**: ✅ **AUDITED AND HARDENED** — Commit `eb8df3f`; mismo tenant, no self-reset, protección jerárquica de `super_admin`, trazabilidad y revocación

**Validaciones requeridas**:
- Mismo tenant
- Rol autorizado
- NO resetear propio usuario
- `manager` NO puede resetear un `super_admin`
- Trazabilidad
- Invalidación de sesiones

---

## 🖥️ HARDWARE

### Estado

| Componente | Estado | UAT |
|------------|--------|-----|
| Walos Print Agent | ✅ DONE | ✅ 2 equipos |
| Impresión térmica 58mm | ✅ DONE | ✅ Digital POS DIG-58IIA |
| Cajón | ✅ DONE | ✅ Aprobado |
| Pairing | ✅ DONE | ✅ Aprobado |
| Instalador Inno Setup | ✅ DONE | ✅ Walos-Agent-Setup.exe |
| Autoarranque | ✅ DONE | ✅ Aprobado |
| Auto-print postventa | ✅ DONE | ✅ Aprobado |

**Decisión**: ✅ APROBADO — No reabrir salvo defecto real.

**Especificaciones**:
- Impresora: Digital POS DIG-58IIA
- Ancho: 58mm
- Instalador: Inno Setup (NO MSI)
- Loopback: 127.0.0.1:17831

---

## 🤖 IA

### Estado V1

| Aspecto | Estado |
|---------|--------|
| OFF por defecto | ✅ DONE |
| Tenant isolation | ✅ PRE-PUSH VERIFIED |
| add_stock deshabilitado | ✅ PRE-PUSH VERIFIED |
| Feature enforcement | ✅ DONE |

### Roles Permitidos

**Cuando feature `ai` está ON**:
- ✅ `super_admin`
- ✅ `manager`
- ✅ `dev`
- ❌ `cashier`
- ❌ `waiter`

### Capacidades V1

**Permitido**: Búsqueda, consulta stock, consulta recetas
**Prohibido**: Modificar inventario, crear productos, cross-tenant

---

## 📋 MIGRACIONES

### Estado final V1

| # | Nombre | Estado | Release Gate |
|---|--------|--------|--------------|
| 018 | pos_sale_idempotency | ✅ Aplicada manualmente; `d1c06f1` | ✅ Sí |
| 019 | company_features | ✅ Aplicada manualmente; `e702214` | ✅ Sí |
| 020 | refund_preparados_source_item | ✅ Aplicada manualmente; `46a0a3a` | ✅ Sí |
| 021 | cash_closing_improvements | ⚪ NO REQUERIDA | ❌ No |

**Operación**: No reaplicar, revertir ni ejecutar SQL adicional para 018/019/020.

### Migración 018 — Clarificación

**Estado**: Trabajo B1.3 separado del Platform Closure

**Target**: V1.0.0

**Decisión**: Separación de commits ≠ separación de release. B1.3 ES parte de V1.

---

## 🎯 RELEASE GATES V1

### Seguridad (4 gates)

| ID | Gate | Severidad | Estado |
|----|------|-----------|--------|
| RG-SEC-01 | AiSessionRepository validación `company_id` | CRITICAL | ✅ `bdb6224` |
| RG-SEC-02 | Roles frontend + backend coherentes | HIGH | ✅ `bdb6224` |
| RG-SEC-03 | Tenant isolation verificado | HIGH | ✅ `bdb6224` |
| RG-SEC-04 | add_stock inejecutable | MEDIUM | ✅ `bdb6224` |

---

### Funcionalidad (5 gates)

| ID | Gate | Severidad | Estado |
|----|------|-----------|--------|
| RG-FUN-01 | Password self-change | HIGH | ✅ `eb8df3f` |
| RG-FUN-02 | Refund preparados `source_order_item_id` | HIGH | ✅ `46a0a3a`; schema aplicado |
| RG-FUN-03 | Cierre caja impresión post-persistencia | HIGH | ✅ `723e598`; UAT física pendiente |
| RG-FUN-04 | Reimpresión cierre histórico | MEDIUM | ✅ `723e598`; UAT física pendiente |
| RG-FUN-05 | POS estable + B1.3 + consola limpia | HIGH | ✅ `d1c06f1`; UAT pendiente |

---

### UX (4 gates)

| ID | Gate | Severidad | Estado |
|----|------|-----------|--------|
| RG-UX-01 | Renombrar "Ventas" → "Restaurante" | MEDIUM | ✅ `bdb6224` |
| RG-UX-02 | Renombrar "POS Deli" → "POS" | MEDIUM | ✅ `bdb6224` |
| RG-UX-03 | Importador feedback receta pendiente | MEDIUM | ⚪ Fuera del push; Fase 6 no implementada |
| RG-UX-04 | Inventario badge "Receta pendiente" | LOW | ⚪ Fuera del push; Fase 6 no implementada |

---

## ❓ DECISIONES DE PRODUCTO — TODAS CERRADAS

### D-01: ¿Debe waiter poder facturar mesas?

**Decisión**: ✅ **NO** — Separar crear/editar de facturar

---

### D-02: ¿Debe cashier poder VER inventario?

**Decisión**: ✅ **NO** — Puede consultar catálogo desde Restaurante/POS

---

### D-03: ¿Incluir refund preparados en V1.0.0?

**Decisión**: ✅ **SÍ** — Implementar ahora

---

### D-04: ¿Nivel de detalle en ticket de cierre?

**Decisión**: ✅ **Resumen** — Detalle completo en V2

---

### D-05: ¿Password self-change en V1?

**Decisión**: ✅ **SÍ** — Release gate V1

---

## 📈 ROADMAP POST-V1

### V1.1 (1-2 meses)
- Deuda técnica prioritaria
- Tests E2E completos
- Mejoras UX POS
- Contraseñas temporales

### V2.0 (3-6 meses)
- WhatsApp
- Facturación electrónica
- Clientes (CRM)
- Updater automático

### V2.1 (6-9 meses)
- IA avanzada
- Employees
- Báscula
- Lectores barcode

---

## ✅ CRITERIOS DE ACEPTACIÓN UAT V1

Estos criterios permanecen abiertos hasta la UAT posterior al deploy; no contradicen los gates automatizados pre-push ya verificados.

### Seguridad
- [ ] Tenant isolation verificado
- [ ] Roles coherentes frontend + backend
- [ ] IA con tenant isolation
- [ ] add_stock deshabilitado

### Funcionalidad
- [ ] Todos los módulos core operativos
- [ ] Cierre de caja con impresión
- [ ] Refunds de preparados correctos
- [ ] Password self-change operativo

### Hardware
- [ ] Print Agent funcional
- [ ] Impresión térmica operativa
- [ ] Cajón operativo
- [ ] Auto-print postventa

### UX
- [ ] Nomenclatura coherente
- [ ] Mensajes claros
- [ ] Feedback de acciones
- [ ] Responsividad básica

---

## 🎬 VEREDICTO

### ✅ **WALOS V1 PRE-PUSH VERIFIED — PUSH/DEPLOY PENDING**

**Razones**:
- ✅ Todas las decisiones de producto cerradas
- ✅ Alcance V1 definido
- ✅ Roles autoritativos documentados
- ✅ Gates del corte actual verificados
- ✅ Deuda aceptada documentada
- ✅ Hardware aprobado

**Trabajo pendiente**:
- 🟡 Push sin force a `main`
- 🟡 Observación de Vercel y Railway
- 🟡 UAT posterior al deploy
- ⚪ Fase 6/importador fuera de este push y no implementada

**Próximos pasos**:
1. Push controlado a `main`
2. Verificar el commit desplegado en Vercel y Railway
3. Ejecutar UAT
4. Evaluar tag posteriormente; no crearlo en este corte

---

## 📄 DOCUMENTOS RELACIONADOS

- **Contrato completo**: `WALOS-V1-MASTER-CONTRACT.md`
- **Auditoría Platform Closure**: `AUDITORIA-V1-PLATFORM-CLOSURE.md`
- **Auditoría Refunds**: `AUDITORIA-REFUND-REVISION-CRITICA.md`

---

**Auditor**: Arquitectónico, funcional, seguridad y producto
**Fecha**: 2026-09-17
**Estado**: ✅ **PRE-PUSH VERIFIED — PUSH/DEPLOY PENDING**

---

**FIN DEL RESUMEN EJECUTIVO**
