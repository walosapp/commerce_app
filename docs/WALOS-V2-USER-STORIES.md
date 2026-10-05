# WALOS V2 — HISTORIAS DE USUARIO

> **Fecha**: 2026-09-14  
> **Formato**: Como [rol] quiero [acción] para [beneficio]  
> **Organización**: Por épicas (EPIC-WA, EPIC-FE, EPIC-HW, EPIC-TICKET, EPIC-PLANS, EPIC-UX, EPIC-CORE)

---

## EPIC-WA: WhatsApp Integration

### WA-001: Configurar WhatsApp Business
**Prioridad**: P0  
**Como** administrador de comercio  
**Quiero** conectar mi cuenta de WhatsApp Business a Walos  
**Para** recibir pedidos por WhatsApp automáticamente

**Criterios de aceptación**:
- ✅ Puedo ingresar a Settings → Integraciones → WhatsApp
- ✅ Puedo hacer clic en "Conectar WhatsApp Business"
- ✅ Me redirige a Meta Business Manager para autorizar
- ✅ Después de autorizar, veo mi número conectado
- ✅ Puedo desconectar WhatsApp en cualquier momento
- ✅ Sistema guarda `phone_number_id`, `access_token` encriptado

**Dependencias**: Ninguna

**Tablas impactadas**: `integrations.whatsapp_config`

**API impactada**:
- `POST /api/v1/integrations/whatsapp/setup`
- `GET /api/v1/integrations/whatsapp/status`
- `DELETE /api/v1/integrations/whatsapp`

**Frontend impactado**: `SettingsPage.jsx`, nuevo `WhatsAppConfig.jsx`

**Tests requeridos**:
- Unit: `WhatsAppServiceTests.cs` (setup, validación token)
- Integration: Webhook verification
- E2E: Flujo completo de configuración

---

### WA-002: Recibir Mensaje de Cliente
**Prioridad**: P0  
**Como** sistema  
**Quiero** recibir mensajes de WhatsApp vía webhook  
**Para** procesarlos automáticamente

**Criterios de aceptación**:
- ✅ Webhook responde a verificación de Meta (GET con challenge)
- ✅ Webhook recibe mensaje (POST con event)
- ✅ Sistema valida firma HMAC del webhook
- ✅ Sistema guarda mensaje en `whatsapp_messages`
- ✅ Sistema crea/actualiza conversación en `whatsapp_conversations`
- ✅ Sistema retorna 200 OK a Meta en < 5 segundos

**Dependencias**: WA-001

**Tablas impactadas**: `whatsapp_messages`, `whatsapp_conversations`

**API impactada**:
- `POST /api/v1/webhooks/whatsapp/{companyId}`
- `GET /api/v1/webhooks/whatsapp/{companyId}`

**Frontend impactado**: Ninguno (backend only)

**Tests requeridos**:
- Unit: Validación HMAC, parseo de mensaje
- Integration: Webhook real de Meta (sandbox)

---

### WA-003: Procesar Pedido con IA
**Prioridad**: P0  
**Como** sistema  
**Quiero** extraer productos y cantidades del mensaje del cliente  
**Para** armar el pedido automáticamente

**Criterios de aceptación**:
- ✅ Mensaje "2 hamburguesas y 1 coca" → IA extrae productos
- ✅ IA retorna `confidence >= 0.8` → Confirmar con cliente
- ✅ IA retorna `confidence < 0.8` → Pedir aclaración
- ✅ IA usa catálogo de productos del comercio
- ✅ IA maneja sinónimos (ej: "coca" → "Coca Cola 400ml")
- ✅ IA detecta cantidades (ej: "2", "dos", "un par")

**Dependencias**: WA-002, Inventario (productos)

**Tablas impactadas**: `whatsapp_conversations`, `inventory.products`

**API impactada**:
- Interno: `OrchestratorService.ProcessWhatsAppMessage()`

**Frontend impactado**: Ninguno

**Tests requeridos**:
- Unit: Extracción de productos con diferentes formatos
- Integration: Llamada real a OpenAI

---

### WA-004: Crear Orden desde WhatsApp
**Prioridad**: P0  
**Como** sistema  
**Quiero** crear una orden de delivery cuando el cliente confirma  
**Para** que aparezca en el tablero Kanban

**Criterios de aceptación**:
- ✅ Cliente confirma pedido → Sistema crea orden en `delivery.orders`
- ✅ Orden tiene `source = 'whatsapp'`
- ✅ Orden incluye `customer_phone`, `customer_name`, `delivery_address`
- ✅ Orden aparece en tablero Kanban con estado `new`
- ✅ Sistema envía confirmación al cliente con número de pedido
- ✅ Conversación se marca como `completed`

**Dependencias**: WA-003, Delivery (módulo)

**Tablas impactadas**: `delivery.orders`, `whatsapp_conversations`

**API impactada**:
- `POST /api/v1/integrations/whatsapp/orders/create`

**Frontend impactado**: `DeliveryBoard.jsx` (muestra orden nueva)

**Tests requeridos**:
- Unit: Creación de orden con datos de WhatsApp
- E2E: Flujo completo desde mensaje hasta orden

---

### WA-005: Sincronizar Estados con Cliente
**Prioridad**: P1  
**Como** sistema  
**Quiero** enviar mensajes automáticos al cliente cuando cambia el estado del pedido  
**Para** mantenerlo informado

**Criterios de aceptación**:
- ✅ Estado `new` → "Pedido recibido"
- ✅ Estado `confirmed` → "Pedido confirmado, estamos preparando"
- ✅ Estado `preparing` → "Tu pedido está en preparación"
- ✅ Estado `dispatched` → "Tu pedido salió, llegará en 15 min"
- ✅ Estado `delivered` → "Pedido entregado. ¡Gracias!"
- ✅ Mensajes se envían vía WhatsApp Cloud API
- ✅ Mensajes se registran en `whatsapp_order_events`

**Dependencias**: WA-004, Delivery (cambio de estado)

**Tablas impactadas**: `whatsapp_order_events`, `delivery.order_status_history`

**API impactada**:
- Interno: `DeliveryService.ChangeStatusAsync()` (trigger de mensaje)

**Frontend impactado**: Ninguno

**Tests requeridos**:
- Unit: Envío de mensaje por cada estado
- Integration: Webhook de estado de mensaje (delivered/read)

---

### WA-006: Publicar Catálogo en WhatsApp
**Prioridad**: P1  
**Como** administrador  
**Quiero** publicar productos en el catálogo de WhatsApp  
**Para** que los clientes puedan verlos

**Criterios de aceptación**:
- ✅ Puedo activar "Publicar en WhatsApp" en producto
- ✅ Sistema sincroniza producto con Meta Catalog API
- ✅ Sistema guarda `whatsapp_product_id` en `whatsapp_catalog`
- ✅ Cliente puede ver catálogo interactivo en WhatsApp
- ✅ Puedo despublicar producto en cualquier momento
- ✅ Cambios en precio/nombre se sincronizan automáticamente

**Dependencias**: WA-001, Inventario (productos)

**Tablas impactadas**: `whatsapp_catalog`

**API impactada**:
- `POST /api/v1/integrations/whatsapp/catalog/sync`
- `PUT /api/v1/integrations/whatsapp/catalog/{id}`

**Frontend impactado**: `ProductFormModal.jsx` (checkbox "Publicar en WhatsApp")

**Tests requeridos**:
- Unit: Sincronización de producto
- Integration: Llamada real a Meta Catalog API

---

### WA-007: Gestionar Clientes
**Prioridad**: P1  
**Como** sistema  
**Quiero** crear/actualizar clientes automáticamente desde WhatsApp  
**Para** tener historial de pedidos por cliente

**Criterios de aceptación**:
- ✅ Mensaje entrante → Buscar cliente por `phone`
- ✅ Si NO existe → Crear cliente con `phone`, `name = NULL`
- ✅ Durante conversación → Pedir nombre "¿Cómo te llamas?"
- ✅ Actualizar `core.customers.name` con respuesta
- ✅ Guardar dirección en `core.customers.address` para próximos pedidos
- ✅ Incrementar `total_orders` y `total_spent` al crear orden

**Dependencias**: WA-002

**Tablas impactadas**: `core.customers` (NUEVA)

**API impactada**:
- `GET /api/v1/customers`
- `POST /api/v1/customers`
- `PUT /api/v1/customers/{id}`

**Frontend impactado**: Nuevo `CustomersPage.jsx`

**Tests requeridos**:
- Unit: Resolución de cliente por teléfono
- Integration: Creación automática de cliente

---

## EPIC-FE: Facturación Electrónica

### FE-001: Configurar Proveedor de Facturación
**Prioridad**: P0  
**Como** administrador  
**Quiero** conectar Walos con un proveedor de facturación electrónica  
**Para** emitir facturas válidas ante DIAN

**Criterios de aceptación**:
- ✅ Puedo ingresar a Settings → Facturación Electrónica
- ✅ Puedo seleccionar proveedor (Alegra o FacturaDirecta)
- ✅ Puedo ingresar API Key y API Secret
- ✅ Puedo activar/desactivar modo de prueba
- ✅ Sistema valida credenciales con proveedor
- ✅ Sistema guarda configuración encriptada

**Dependencias**: Ninguna

**Tablas impactadas**: `integrations.electronic_invoice_config`

**API impactada**:
- `POST /api/v1/integrations/electronic-invoice/setup`
- `GET /api/v1/integrations/electronic-invoice/status`
- `POST /api/v1/integrations/electronic-invoice/test`

**Frontend impactado**: `SettingsPage.jsx`, nuevo `ElectronicInvoiceConfig.jsx`

**Tests requeridos**:
- Unit: Validación de credenciales
- Integration: Conexión real con Alegra (sandbox)

---

### FE-002: Configurar Resolución DIAN
**Prioridad**: P0  
**Como** administrador de sucursal  
**Quiero** configurar la resolución DIAN de mi sucursal  
**Para** generar números de factura válidos

**Criterios de aceptación**:
- ✅ Puedo ingresar número de resolución DIAN
- ✅ Puedo ingresar prefijo (ej: "FV")
- ✅ Puedo ingresar rango (inicio/fin)
- ✅ Puedo ingresar fecha de resolución y vencimiento
- ✅ Sistema valida que `current_number <= end_number`
- ✅ Sistema alerta si resolución está por vencer (< 30 días)

**Dependencias**: FE-001

**Tablas impactadas**: `integrations.branch_invoice_config`

**API impactada**:
- `POST /api/v1/integrations/electronic-invoice/resolution`
- `GET /api/v1/integrations/electronic-invoice/resolution`

**Frontend impactado**: `SettingsPage.jsx`, nuevo `ResolutionConfig.jsx`

**Tests requeridos**:
- Unit: Validación de rango de numeración
- Unit: Alerta de vencimiento

---

### FE-003: Emitir Factura Electrónica
**Prioridad**: P0  
**Como** cajero  
**Quiero** emitir factura electrónica al facturar una mesa  
**Para** cumplir con la ley

**Criterios de aceptación**:
- ✅ Al facturar mesa → Sistema pide datos de cliente (NIT/CC obligatorio)
- ✅ Sistema genera número de factura (prefijo + número)
- ✅ Sistema envía factura a proveedor
- ✅ Proveedor envía XML a DIAN
- ✅ DIAN valida y retorna CUFE
- ✅ Sistema descarga PDF y XML del proveedor
- ✅ Sistema guarda archivos en Supabase Storage
- ✅ Sistema envía PDF por email al cliente
- ✅ Sistema muestra factura emitida en UI

**Dependencias**: FE-001, FE-002, Ventas (facturación)

**Tablas impactadas**: `sales.electronic_invoices`

**API impactada**:
- `POST /api/v1/sales/orders/{orderId}/issue-invoice`
- `GET /api/v1/sales/invoices/{invoiceId}/status`
- `GET /api/v1/sales/invoices/{invoiceId}/pdf`

**Frontend impactado**: `InvoicePanel.jsx` (agregar datos de cliente)

**Tests requeridos**:
- Unit: Generación de número de factura
- Integration: Emisión real con Alegra (sandbox)
- E2E: Flujo completo de facturación

---

### FE-004: Emitir Nota Crédito
**Prioridad**: P1  
**Como** administrador  
**Quiero** emitir nota crédito cuando anulo una venta  
**Para** cumplir con la ley

**Criterios de aceptación**:
- ✅ Puedo hacer clic en "Anular" en factura electrónica
- ✅ Sistema pide motivo de anulación (obligatorio)
- ✅ Sistema genera nota crédito con proveedor
- ✅ Proveedor envía XML a DIAN
- ✅ Sistema descarga PDF y XML de nota crédito
- ✅ Sistema marca factura original como `cancelled`
- ✅ Sistema envía nota crédito por email al cliente

**Dependencias**: FE-003, Devoluciones

**Tablas impactadas**: `sales.credit_notes`

**API impactada**:
- `POST /api/v1/sales/invoices/{invoiceId}/credit-note`

**Frontend impactado**: `SalesSummaryTab.jsx` (botón "Anular")

**Tests requeridos**:
- Unit: Generación de nota crédito
- Integration: Emisión real con Alegra (sandbox)

---

### FE-005: Reintentar Factura Fallida
**Prioridad**: P1  
**Como** sistema  
**Quiero** reintentar automáticamente facturas fallidas  
**Para** no perder ventas por errores temporales

**Criterios de aceptación**:
- ✅ Si emisión falla → Sistema guarda en `invoice_retry_log`
- ✅ Sistema reintenta cada 5 minutos (máximo 3 intentos)
- ✅ Si falla 3 veces → Sistema marca como `rejected`
- ✅ Sistema notifica admin por email
- ✅ Admin puede reintentar manualmente desde UI
- ✅ Sistema muestra log de reintentos en UI

**Dependencias**: FE-003

**Tablas impactadas**: `sales.invoice_retry_log`

**API impactada**:
- `POST /api/v1/sales/invoices/{invoiceId}/retry`
- `GET /api/v1/sales/invoices/{invoiceId}/retry-log`

**Frontend impactado**: Nuevo `InvoiceRetryPanel.jsx`

**Tests requeridos**:
- Unit: Lógica de reintentos
- Integration: Reintentos con errores simulados

---

## EPIC-HW: Hardware (Print Agent)

### HW-001: Instalar Print Agent
**Prioridad**: P0  
**Como** administrador de comercio  
**Quiero** instalar el Print Agent en mi PC de caja  
**Para** imprimir recibos térmicos

**Criterios de aceptación**:
- ✅ Puedo descargar instalador desde `walos.app/downloads/print-agent`
- ✅ Instalador requiere permisos admin
- ✅ Instalador copia archivos a `C:\Program Files\Walos\PrintAgent\`
- ✅ Instalador crea servicio de Windows `WalosPrintAgent`
- ✅ Instalador configura auto-start
- ✅ Instalador abre puerto 8080 en firewall
- ✅ Instalador muestra icono en system tray
- ✅ Puedo ver estado del agente en system tray (verde = running)

**Dependencias**: Ninguna

**Tablas impactadas**: Ninguna

**API impactada**: Ninguna (instalador standalone)

**Frontend impactado**: Ninguno

**Tests requeridos**:
- Manual: Instalación en Windows 10/11
- Manual: Desinstalación limpia

---

### HW-002: Imprimir Recibo de Venta
**Prioridad**: P0  
**Como** cajero  
**Quiero** imprimir recibo térmico al facturar  
**Para** entregarlo al cliente

**Criterios de aceptación**:
- ✅ Al facturar mesa → Sistema ofrece "Imprimir recibo"
- ✅ Sistema genera token de impresión (JWT, exp: 30s)
- ✅ Frontend llama `POST http://localhost:8080/print/receipt`
- ✅ Print Agent valida token
- ✅ Print Agent obtiene datos de recibo desde Walos API
- ✅ Print Agent genera comandos ESC/POS
- ✅ Print Agent envía a impresora térmica
- ✅ Recibo se imprime correctamente
- ✅ Frontend muestra "Recibo impreso" o error

**Dependencias**: HW-001, Ventas (facturación)

**Tablas impactadas**: Ninguna

**API impactada**:
- `GET /api/v1/sales/orders/{orderId}/receipt` (backend)
- `POST http://localhost:8080/print/receipt` (Print Agent)

**Frontend impactado**: `InvoicePanel.jsx` (botón "Imprimir")

**Tests requeridos**:
- Unit: Generación de comandos ESC/POS
- Integration: Impresión real en impresora térmica

---

### HW-003: Abrir Cajón de Dinero
**Prioridad**: P0  
**Como** cajero  
**Quiero** abrir el cajón de dinero al facturar  
**Para** dar cambio al cliente

**Criterios de aceptación**:
- ✅ Al facturar con efectivo → Sistema abre cajón automáticamente
- ✅ Print Agent envía comando ESC/POS `ESC p 0 25 250`
- ✅ Cajón se abre mecánicamente
- ✅ Admin puede abrir cajón manualmente desde UI (requiere autenticación)
- ✅ Apertura manual requiere token con rol `admin` o `manager`

**Dependencias**: HW-001

**Tablas impactadas**: Ninguna

**API impactada**:
- `POST http://localhost:8080/drawer/open` (Print Agent)

**Frontend impactado**: `CashRegisterBar.jsx` (botón "Abrir cajón")

**Tests requeridos**:
- Manual: Apertura de cajón real

---

### HW-004: Configurar Impresoras
**Prioridad**: P1  
**Como** administrador  
**Quiero** configurar qué impresora usar para cada tipo de documento  
**Para** tener impresoras separadas (recibo, cocina, bar)

**Criterios de aceptación**:
- ✅ Puedo ver lista de impresoras disponibles
- ✅ Puedo asignar impresora para recibos
- ✅ Puedo asignar impresora para comandas de cocina
- ✅ Puedo asignar impresora para comandas de bar
- ✅ Sistema guarda configuración en archivo local
- ✅ Print Agent usa impresora correcta según tipo de documento

**Dependencias**: HW-001

**Tablas impactadas**: Ninguna (config local en Print Agent)

**API impactada**:
- `GET http://localhost:8080/printers` (Print Agent)

**Frontend impactado**: `PrinterSettings.jsx` (ya existe, conectar)

**Tests requeridos**:
- Unit: Selección de impresora por tipo

---

### HW-005: Actualizar Print Agent
**Prioridad**: P1  
**Como** sistema  
**Quiero** actualizar el Print Agent automáticamente  
**Para** tener siempre la última versión

**Criterios de aceptación**:
- ✅ Print Agent verifica versión cada 24 horas
- ✅ Si hay nueva versión → Muestra notificación en system tray
- ✅ Usuario hace clic en "Actualizar"
- ✅ Print Agent descarga nuevo instalador
- ✅ Print Agent ejecuta instalador en modo silencioso
- ✅ Servicio se reinicia automáticamente
- ✅ Usuario ve "Actualizado a v1.2.3" en system tray

**Dependencias**: HW-001

**Tablas impactadas**: Ninguna

**API impactada**:
- `GET https://api.walos.app/print-agent/latest-version`

**Frontend impactado**: Ninguno

**Tests requeridos**:
- Manual: Actualización de versión

---

## EPIC-TICKET: Personalización de Tickets

### TICKET-001: Configurar Template de Ticket
**Prioridad**: P1  
**Como** administrador  
**Quiero** personalizar el ticket impreso  
**Para** que tenga mi logo y mensajes

**Criterios de aceptación**:
- ✅ Puedo ingresar a Settings → Tickets
- ✅ Puedo activar/desactivar logo
- ✅ Puedo activar/desactivar NIT, dirección, teléfono
- ✅ Puedo ingresar mensaje superior (header)
- ✅ Puedo ingresar mensaje inferior (footer)
- ✅ Puedo seleccionar ancho de papel (58mm o 80mm)
- ✅ Puedo seleccionar tamaño de fuente (small, normal, large)
- ✅ Puedo ver vista previa del ticket

**Dependencias**: Ninguna

**Tablas impactadas**: `core.receipt_templates`

**API impactada**:
- `GET /api/v1/company/receipt-template`
- `PUT /api/v1/company/receipt-template`
- `POST /api/v1/company/receipt-template/preview`

**Frontend impactado**: `SettingsPage.jsx`, nuevo `TicketTemplateConfig.jsx`

**Tests requeridos**:
- Unit: Generación de template
- E2E: Vista previa de ticket

---

### TICKET-002: Imprimir Logo en Ticket
**Prioridad**: P1  
**Como** administrador  
**Quiero** que mi logo aparezca en el ticket  
**Para** reforzar mi marca

**Criterios de aceptación**:
- ✅ Si `show_logo = true` → Logo se imprime en ticket
- ✅ Logo se convierte a bitmap monocromático
- ✅ Logo se redimensiona según ancho de papel (384px para 80mm, 256px para 58mm)
- ✅ Logo se envía como comandos ESC/POS (GS v 0)
- ✅ Si logo no existe → Ticket se imprime sin logo (no falla)

**Dependencias**: TICKET-001, HW-002

**Tablas impactadas**: Ninguna

**API impactada**: Ninguna (lógica en Print Agent)

**Frontend impactado**: Ninguno

**Tests requeridos**:
- Unit: Conversión de logo a bitmap
- Manual: Impresión de logo real

---

## EPIC-PLANS: Planes y Entitlements

### PLANS-001: Asignar Plan a Comercio
**Prioridad**: P0  
**Como** platform admin  
**Quiero** asignar un plan a un comercio  
**Para** definir sus límites

**Criterios de aceptación**:
- ✅ Puedo ver lista de comercios en panel admin
- ✅ Puedo asignar plan (Starter, Business, Enterprise)
- ✅ Sistema crea entitlements según plan
- ✅ Sistema muestra límites actuales del comercio
- ✅ Puedo cambiar plan en cualquier momento
- ✅ Al cambiar plan → Entitlements se actualizan

**Dependencias**: Ninguna

**Tablas impactadas**: `platform.subscriptions`, `platform.company_entitlements`

**API impactada**:
- `POST /api/v1/admin/companies/{id}/subscription`
- `PUT /api/v1/admin/companies/{id}/subscription`

**Frontend impactado**: `CompaniesPage.jsx` (agregar selector de plan)

**Tests requeridos**:
- Unit: Creación de entitlements
- Integration: Cambio de plan

---

### PLANS-002: Validar Límites al Crear Recurso
**Prioridad**: P0  
**Como** sistema  
**Quiero** validar límites antes de crear usuarios/sucursales/productos  
**Para** respetar el plan del comercio

**Criterios de aceptación**:
- ✅ Al crear usuario → Verificar `entitlements.users.current < limit`
- ✅ Al crear sucursal → Verificar `entitlements.branches.current < limit`
- ✅ Al crear producto → Verificar `entitlements.products.current < limit`
- ✅ Al emitir factura → Verificar `entitlements.invoices_per_month.current < limit`
- ✅ Si límite excedido → Retornar `402 Payment Required`
- ✅ Frontend muestra mensaje "Límite alcanzado. Actualiza tu plan."

**Dependencias**: PLANS-001

**Tablas impactadas**: `platform.company_entitlements`

**API impactada**: Todos los endpoints de creación (middleware)

**Frontend impactado**: Todos los modales de creación (mostrar error)

**Tests requeridos**:
- Unit: Validación de límites
- E2E: Intentar crear recurso con límite alcanzado

---

### PLANS-003: Mostrar Uso de Entitlements
**Prioridad**: P1  
**Como** administrador  
**Quiero** ver mi uso actual de entitlements  
**Para** saber cuándo actualizar mi plan

**Criterios de aceptación**:
- ✅ Puedo ver uso actual en Settings → Plan
- ✅ Veo límite y uso para cada entitlement
- ✅ Veo fecha de reset para `invoices_per_month`
- ✅ Veo alerta si estoy cerca del límite (> 80%)
- ✅ Puedo hacer clic en "Actualizar plan" (redirige a página de planes)

**Dependencias**: PLANS-001

**Tablas impactadas**: `platform.company_entitlements`

**API impactada**:
- `GET /api/v1/company/entitlements/usage`

**Frontend impactado**: `SettingsPage.jsx`, nuevo `PlanUsagePanel.jsx`

**Tests requeridos**:
- Unit: Cálculo de uso
- E2E: Vista de uso

---

## EPIC-UX: Mejoras de Experiencia de Usuario

### UX-001: Corregir Errores de Inicio (B1 Blockers)
**Prioridad**: P0  
**Como** usuario  
**Quiero** que el login funcione correctamente  
**Para** poder ingresar al sistema

**Criterios de aceptación**:
- ✅ Login con `username` (minúscula) funciona
- ✅ Rol `dev` puede acceder a Finance, Settings, Users
- ✅ Logo faltante muestra fallback default
- ✅ Manifest PWA carga correctamente

**Dependencias**: Ninguna

**Tablas impactadas**: Ninguna

**API impactada**:
- `POST /api/v1/auth/login` (fix validación)
- `GET /api/v1/pwa/icon/{tenantId}/{size}.png` (fix fallback)

**Frontend impactado**: Ninguno

**Tests requeridos**:
- Unit: Validación de login
- E2E: Login exitoso

---

### UX-002: Integrar Caja en Flujo de Facturación
**Prioridad**: P0  
**Como** cajero  
**Quiero** que el sistema valide que tengo caja abierta antes de facturar  
**Para** evitar errores contables

**Criterios de aceptación**:
- ✅ Al abrir SalesPage → Sistema verifica si tengo caja abierta
- ✅ Si NO tengo caja → Muestra modal "Abrir Caja"
- ✅ Si tengo caja → Muestra barra de estado con totales en vivo
- ✅ Al facturar → Sistema asocia venta a `cash_register_id` activo
- ✅ Al cerrar caja → Sistema muestra resumen y pide arqueo
- ✅ Puedo ver historial de turnos de caja

**Dependencias**: Caja (backend ya existe)

**Tablas impactadas**: `sales.cash_registers`

**API impactada**: Ya existen (solo integrar frontend)

**Frontend impactado**:
- `SalesPage.jsx` (agregar validación)
- `CashRegisterBar.jsx` (ya existe, integrar)
- `OpenCashRegisterModal.jsx` (ya existe, integrar)
- `CloseCashRegisterModal.jsx` (ya existe, integrar)

**Tests requeridos**:
- E2E: Flujo completo de caja

---

## EPIC-CORE: Mejoras Core

### CORE-001: Módulo de Clientes
**Prioridad**: P1  
**Como** administrador  
**Quiero** gestionar mis clientes  
**Para** tener historial de compras

**Criterios de aceptación**:
- ✅ Puedo ver lista de clientes
- ✅ Puedo crear cliente manualmente
- ✅ Puedo editar datos de cliente
- ✅ Puedo ver historial de pedidos por cliente
- ✅ Puedo ver total gastado por cliente
- ✅ Puedo buscar cliente por nombre/teléfono

**Dependencias**: Ninguna

**Tablas impactadas**: `core.customers`

**API impactada**:
- `GET /api/v1/customers`
- `POST /api/v1/customers`
- `PUT /api/v1/customers/{id}`
- `GET /api/v1/customers/{id}/orders`

**Frontend impactado**: Nuevo `CustomersPage.jsx`

**Tests requeridos**:
- Unit: CRUD de clientes
- E2E: Gestión de clientes

---

## RESUMEN DE HISTORIAS

| Épica | Cantidad | Prioridad P0 | Prioridad P1 |
|-------|----------|--------------|--------------|
| **EPIC-WA** | 7 | 4 | 3 |
| **EPIC-FE** | 5 | 3 | 2 |
| **EPIC-HW** | 5 | 3 | 2 |
| **EPIC-TICKET** | 2 | 0 | 2 |
| **EPIC-PLANS** | 3 | 2 | 1 |
| **EPIC-UX** | 2 | 2 | 0 |
| **EPIC-CORE** | 1 | 0 | 1 |
| **TOTAL** | **25** | **14** | **11** |

---

**FIN DE HISTORIAS DE USUARIO V2**
