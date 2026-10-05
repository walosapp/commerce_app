# WALOS V2 — ROADMAP

> **Fecha**: 2026-09-14  
> **Objetivo**: Bloques de implementación pequeños y cerrables para Walos V2  
> **Metodología**: Iteraciones de 1-2 semanas con entregables funcionales

---

## VISIÓN GENERAL

Walos V2 se construirá en **9 bloques** iterativos, cada uno con un entregable funcional y desplegable.

```
B1 Closure → V2.1 → V2.2 → V2.3 → V2.4 → V2.5 → V2.6 → V2.7 → V2.8 → V2.9 → V2 Production
```

**Duración estimada total**: 14-18 semanas (3.5-4.5 meses)

---

## V2.1 — CIERRE TÉCNICO B1

**Objetivo**: Corregir blockers de B1 y estabilizar base

**Duración**: 1 semana

**Entregables**:
- ✅ Fix de validación de login (FluentValidation + case-insensitive JSON)
- ✅ Fix de políticas de autorización (rol `dev` en todas las políticas)
- ✅ Fix de fallback de logo (default logo en PWA)
- ✅ Integración de Caja en flujo de facturación (frontend)
- ✅ Tests de regresión para fixes

**Historias incluidas**:
- UX-001: Corregir Errores de Inicio
- UX-002: Integrar Caja en Flujo de Facturación

**Criterios de cierre**:
- ✅ Login funciona con `username` minúscula
- ✅ Usuario `dev` puede acceder a Finance, Settings, Users
- ✅ Manifest PWA carga sin errores
- ✅ Cajero puede abrir/cerrar caja desde SalesPage
- ✅ Facturación valida caja abierta
- ✅ Tests de regresión pasan (100%)

**Riesgos**:
- 🟢 **BAJO** — Fixes simples, bien definidos

---

## V2.2 — WHATSAPP FOUNDATION

**Objetivo**: Configuración y recepción de mensajes de WhatsApp

**Duración**: 2 semanas

**Entregables**:
- ✅ Tabla `integrations.whatsapp_config`
- ✅ Endpoint `POST /api/v1/integrations/whatsapp/setup`
- ✅ Webhook `POST /api/v1/webhooks/whatsapp/{companyId}`
- ✅ Validación HMAC de webhooks
- ✅ Tabla `integrations.whatsapp_messages`
- ✅ Tabla `integrations.whatsapp_conversations`
- ✅ UI de configuración en Settings
- ✅ Tests de webhook con Meta sandbox

**Historias incluidas**:
- WA-001: Configurar WhatsApp Business
- WA-002: Recibir Mensaje de Cliente

**Criterios de cierre**:
- ✅ Admin puede conectar WhatsApp Business desde Settings
- ✅ Webhook recibe mensajes de Meta
- ✅ Mensajes se guardan en DB
- ✅ Conversaciones se crean automáticamente
- ✅ Tests de integración con Meta sandbox pasan

**Riesgos**:
- 🟡 **MEDIO** — Dependencia de Meta Cloud API (sandbox puede fallar)

---

## V2.3 — PEDIDO WHATSAPP

**Objetivo**: Flujo completo de pedido por WhatsApp

**Duración**: 2 semanas

**Entregables**:
- ✅ Integración con Orquestador IA para extracción de productos
- ✅ Tabla `core.customers`
- ✅ Endpoint `POST /api/v1/integrations/whatsapp/orders/create`
- ✅ Tabla `integrations.whatsapp_order_events`
- ✅ Sincronización de estados con WhatsApp
- ✅ UI de clientes (CustomersPage)
- ✅ Tests E2E de flujo completo

**Historias incluidas**:
- WA-003: Procesar Pedido con IA
- WA-004: Crear Orden desde WhatsApp
- WA-005: Sincronizar Estados con Cliente
- WA-007: Gestionar Clientes
- CORE-001: Módulo de Clientes

**Criterios de cierre**:
- ✅ Cliente envía "2 hamburguesas" → IA extrae productos
- ✅ Cliente confirma → Orden se crea en delivery.orders
- ✅ Orden aparece en tablero Kanban
- ✅ Cambios de estado envían mensajes a cliente
- ✅ Cliente se crea/actualiza automáticamente
- ✅ Admin puede ver lista de clientes

**Riesgos**:
- 🟡 **MEDIO** — IA puede fallar en extracción de productos (requiere fine-tuning)

---

## V2.4 — FACTURACIÓN ELECTRÓNICA PROVIDER

**Objetivo**: Integración con proveedor de facturación electrónica

**Duración**: 2 semanas

**Entregables**:
- ✅ Interfaz `IElectronicInvoiceProvider`
- ✅ Implementación `AlegraInvoiceProvider`
- ✅ Tabla `integrations.electronic_invoice_config`
- ✅ Tabla `integrations.branch_invoice_config`
- ✅ Endpoint `POST /api/v1/integrations/electronic-invoice/setup`
- ✅ UI de configuración en Settings
- ✅ Tests de integración con Alegra sandbox

**Historias incluidas**:
- FE-001: Configurar Proveedor de Facturación
- FE-002: Configurar Resolución DIAN

**Criterios de cierre**:
- ✅ Admin puede conectar Alegra desde Settings
- ✅ Admin puede configurar resolución DIAN
- ✅ Sistema valida credenciales con Alegra
- ✅ Sistema valida rango de numeración
- ✅ Tests de integración con Alegra sandbox pasan

**Riesgos**:
- 🟠 **MEDIO** — Dependencia de Alegra API (puede cambiar)

---

## V2.5 — EMISIÓN FISCAL

**Objetivo**: Emisión de facturas electrónicas y notas crédito

**Duración**: 2 semanas

**Entregables**:
- ✅ Tabla `sales.electronic_invoices`
- ✅ Tabla `sales.credit_notes`
- ✅ Tabla `sales.invoice_retry_log`
- ✅ Endpoint `POST /api/v1/sales/orders/{orderId}/issue-invoice`
- ✅ Endpoint `POST /api/v1/sales/invoices/{invoiceId}/credit-note`
- ✅ Lógica de reintentos automáticos
- ✅ UI de emisión en InvoicePanel
- ✅ Tests E2E de emisión

**Historias incluidas**:
- FE-003: Emitir Factura Electrónica
- FE-004: Emitir Nota Crédito
- FE-005: Reintentar Factura Fallida

**Criterios de cierre**:
- ✅ Cajero puede emitir factura al facturar mesa
- ✅ Factura se envía a DIAN vía Alegra
- ✅ PDF y XML se descargan y guardan en Supabase
- ✅ Cliente recibe PDF por email
- ✅ Admin puede emitir nota crédito
- ✅ Facturas fallidas se reintentan automáticamente

**Riesgos**:
- 🟠 **MEDIO** — DIAN puede rechazar facturas (validaciones estrictas)

---

## V2.6 — HARDWARE H2/H3

**Objetivo**: Print Agent y impresión real

**Duración**: 2 semanas

**Entregables**:
- ✅ Print Agent (servicio Windows .NET 8)
- ✅ Instalador `WalosPrintAgent-Setup.exe`
- ✅ Endpoint `POST http://localhost:8080/print/receipt`
- ✅ Endpoint `POST http://localhost:8080/drawer/open`
- ✅ Generación de comandos ESC/POS
- ✅ UI de impresión en InvoicePanel
- ✅ Tests de impresión real

**Historias incluidas**:
- HW-001: Instalar Print Agent
- HW-002: Imprimir Recibo de Venta
- HW-003: Abrir Cajón de Dinero
- HW-004: Configurar Impresoras

**Criterios de cierre**:
- ✅ Instalador funciona en Windows 10/11
- ✅ Print Agent se ejecuta como servicio
- ✅ Cajero puede imprimir recibo al facturar
- ✅ Recibo se imprime en impresora térmica
- ✅ Cajón se abre automáticamente al facturar con efectivo
- ✅ Admin puede configurar impresoras

**Riesgos**:
- 🟠 **MEDIO** — Compatibilidad con diferentes modelos de impresoras

---

## V2.7 — TICKET CUSTOMIZATION

**Objetivo**: Personalización de tickets

**Duración**: 1 semana

**Entregables**:
- ✅ Tabla `core.receipt_templates`
- ✅ Endpoint `PUT /api/v1/company/receipt-template`
- ✅ Conversión de logo a bitmap ESC/POS
- ✅ UI de configuración en Settings
- ✅ Vista previa de ticket

**Historias incluidas**:
- TICKET-001: Configurar Template de Ticket
- TICKET-002: Imprimir Logo en Ticket

**Criterios de cierre**:
- ✅ Admin puede personalizar ticket desde Settings
- ✅ Logo se imprime correctamente en ticket
- ✅ Mensajes personalizados aparecen en ticket
- ✅ Vista previa muestra ticket real

**Riesgos**:
- 🟢 **BAJO** — Funcionalidad bien definida

---

## V2.8 — PLANS/ENTITLEMENTS

**Objetivo**: Planes y límites por suscripción

**Duración**: 1 semana

**Entregables**:
- ✅ Tabla `platform.company_entitlements`
- ✅ Middleware `EntitlementMiddleware`
- ✅ Endpoint `GET /api/v1/company/entitlements/usage`
- ✅ UI de uso de plan en Settings
- ✅ Validación de límites en creación de recursos

**Historias incluidas**:
- PLANS-001: Asignar Plan a Comercio
- PLANS-002: Validar Límites al Crear Recurso
- PLANS-003: Mostrar Uso de Entitlements

**Criterios de cierre**:
- ✅ Platform admin puede asignar plan a comercio
- ✅ Sistema valida límites antes de crear recursos
- ✅ Admin puede ver uso de entitlements
- ✅ Sistema retorna 402 si límite excedido

**Riesgos**:
- 🟢 **BAJO** — Funcionalidad bien definida

---

## V2.9 — REGRESSION & POLISH

**Objetivo**: Tests de regresión y pulido final

**Duración**: 2 semanas

**Entregables**:
- ✅ Suite completa de tests E2E (Playwright)
- ✅ Tests de regresión para todos los módulos
- ✅ Corrección de bugs encontrados
- ✅ Optimización de performance
- ✅ Documentación actualizada
- ✅ Guías de usuario

**Criterios de cierre**:
- ✅ Tests E2E pasan al 100%
- ✅ Coverage de tests >= 80%
- ✅ No hay bugs críticos abiertos
- ✅ Performance: LCP < 2.5s, FID < 100ms
- ✅ Documentación completa y actualizada

**Riesgos**:
- 🟡 **MEDIO** — Pueden aparecer bugs inesperados

---

## CRONOGRAMA VISUAL

```
Semana  | Bloque                  | Entregable
--------|-------------------------|----------------------------------
1       | V2.1 Cierre B1          | B1 estable y desplegable
2-3     | V2.2 WhatsApp Foundation| Recepción de mensajes
4-5     | V2.3 Pedido WhatsApp    | Flujo completo de pedido
6-7     | V2.4 FE Provider        | Integración con Alegra
8-9     | V2.5 Emisión Fiscal     | Facturas electrónicas
10-11   | V2.6 Hardware           | Print Agent + impresión
12      | V2.7 Ticket Custom      | Tickets personalizados
13      | V2.8 Plans/Entitlements | Límites por plan
14-15   | V2.9 Regression         | Tests y pulido
16      | V2 Production           | 🚀 Lanzamiento
```

---

## RELEASE DEFINITIONS

### B1 CERRADA

**Criterios**:
- ✅ Todos los blockers corregidos (4/4)
- ✅ Tests de regresión pasan (100%)
- ✅ Build exitoso en CI/CD
- ✅ Migraciones aplicadas en DB
- ✅ Auditoría de seguridad aprobada
- ✅ Documentación actualizada
- ✅ Deploy en ambiente de staging
- ✅ UAT completado por equipo interno

**Fecha objetivo**: 2026-09-15

---

### V2 ALPHA

**Criterios**:
- ✅ V2.1 a V2.5 completados
- ✅ WhatsApp funcional (recepción + pedidos)
- ✅ Facturación electrónica funcional (emisión + notas crédito)
- ✅ Tests E2E para WhatsApp y FE pasan
- ✅ Build exitoso en CI/CD
- ✅ Migraciones aplicadas en DB
- ✅ Deploy en ambiente de staging
- ✅ UAT con 3 comercios piloto

**Fecha objetivo**: 2026-11-15 (2 meses desde B1)

**Limitaciones conocidas**:
- Print Agent no incluido (solo browser print)
- Tickets no personalizables
- Sin límites por plan

---

### V2 BETA

**Criterios**:
- ✅ V2.1 a V2.8 completados
- ✅ Print Agent funcional (impresión + cajón)
- ✅ Tickets personalizables
- ✅ Planes y entitlements funcionales
- ✅ Tests E2E completos pasan (100%)
- ✅ Coverage >= 80%
- ✅ Build exitoso en CI/CD
- ✅ Migraciones aplicadas en DB
- ✅ Auditoría de seguridad aprobada
- ✅ Deploy en ambiente de staging
- ✅ UAT con 10 comercios piloto
- ✅ Documentación de usuario completa

**Fecha objetivo**: 2026-12-15 (3 meses desde B1)

**Limitaciones conocidas**:
- Pueden existir bugs menores
- Performance no optimizada al 100%

---

### V2 PRODUCTION-READY

**Criterios**:
- ✅ V2.1 a V2.9 completados
- ✅ Todos los tests pasan (100%)
- ✅ Coverage >= 85%
- ✅ No hay bugs críticos abiertos
- ✅ Performance: LCP < 2.5s, FID < 100ms, CLS < 0.1
- ✅ Build exitoso en CI/CD
- ✅ Migraciones aplicadas en DB producción
- ✅ Auditoría de seguridad aprobada (externa)
- ✅ Auditoría de performance aprobada
- ✅ Deploy en ambiente de producción
- ✅ UAT con 50 comercios piloto
- ✅ Documentación completa (técnica + usuario)
- ✅ Plan de rollback probado
- ✅ Monitoreo y alertas configurados
- ✅ Soporte 24/7 disponible

**Fecha objetivo**: 2027-01-15 (4 meses desde B1)

**Criterios de lanzamiento**:
- ✅ 0 bugs críticos
- ✅ < 5 bugs altos
- ✅ Uptime >= 99.5% en staging (último mes)
- ✅ Tiempo de respuesta promedio < 500ms
- ✅ 100% de comercios piloto satisfechos (NPS >= 8)

---

## DEPENDENCIAS CRÍTICAS

### Externas

| Dependencia | Proveedor | Riesgo | Mitigación |
|-------------|-----------|--------|------------|
| WhatsApp Cloud API | Meta | 🟡 MEDIO | Sandbox disponible, documentación estable |
| Alegra API | Alegra | 🟡 MEDIO | Sandbox disponible, contrato comercial |
| DIAN | Gobierno Colombia | 🟠 ALTO | Validaciones estrictas, requiere certificación |
| OpenAI API | OpenAI | 🟢 BAJO | API estable, fallback a GPT-3.5 |
| Supabase Storage | Supabase | 🟢 BAJO | SLA 99.9%, backup diario |

### Internas

| Dependencia | Módulo | Riesgo | Mitigación |
|-------------|--------|--------|------------|
| Inventario | Productos | 🟢 BAJO | Ya implementado y estable |
| Delivery | Pedidos | 🟢 BAJO | Ya implementado y estable |
| Ventas | Facturación | 🟢 BAJO | Ya implementado y estable |
| Caja | Cash Register | 🟡 MEDIO | Backend listo, frontend en V2.1 |

---

## MÉTRICAS DE ÉXITO

### V2 Alpha

- ✅ 3 comercios piloto usando WhatsApp
- ✅ 100 pedidos recibidos por WhatsApp
- ✅ 50 facturas electrónicas emitidas
- ✅ 0 errores críticos en producción

### V2 Beta

- ✅ 10 comercios piloto usando WhatsApp
- ✅ 500 pedidos recibidos por WhatsApp
- ✅ 200 facturas electrónicas emitidas
- ✅ 10 comercios usando Print Agent
- ✅ NPS >= 7

### V2 Production

- ✅ 50 comercios activos
- ✅ 2,000 pedidos recibidos por WhatsApp
- ✅ 1,000 facturas electrónicas emitidas
- ✅ 50 comercios usando Print Agent
- ✅ NPS >= 8
- ✅ Uptime >= 99.5%
- ✅ Tiempo de respuesta promedio < 500ms

---

## RIESGOS GLOBALES

| Riesgo | Probabilidad | Impacto | Mitigación |
|--------|--------------|---------|------------|
| DIAN rechaza facturas | 🟡 MEDIO | 🔴 ALTO | Validaciones exhaustivas, sandbox de Alegra |
| Meta cambia API de WhatsApp | 🟢 BAJO | 🟠 MEDIO | Monitorear changelog, tests de integración |
| Print Agent incompatible con impresoras | 🟡 MEDIO | 🟠 MEDIO | Probar con 5 modelos diferentes |
| Límites de OpenAI excedidos | 🟢 BAJO | 🟡 MEDIO | Fallback a GPT-3.5, cache de respuestas |
| Bugs críticos en producción | 🟡 MEDIO | 🔴 ALTO | Tests E2E completos, UAT exhaustivo |

---

## PRÓXIMOS PASOS

1. ✅ **Aprobar roadmap** (stakeholders)
2. ✅ **Asignar equipo** a cada bloque
3. ✅ **Crear épicas en Jira/GitHub** (una por bloque)
4. ✅ **Crear historias de usuario** en backlog
5. ✅ **Iniciar V2.1** (Cierre B1)

---

**B1 CLOSURE PROPOSAL READY**  
**V2 BACKLOG READY**

---

**FIN DE ROADMAP V2**
