# WALOS V2 — CONTRATO TO-BE

> **Fecha**: 2026-09-14  
> **Objetivo**: Definir comportamiento esperado de Walos V2 para producto comercial "Walos Fundadores"  
> **Alcance**: WhatsApp, Facturación Electrónica, Hardware (Print Agent), Ticket Comercial, Planes/Entitlements

---

## VISIÓN V2

**Walos V2** es la versión comercial lista para **Walos Fundadores** (primeros 100 clientes), con las siguientes capacidades críticas:

1. **Recepción de pedidos por WhatsApp** con flujo automatizado
2. **Facturación electrónica** con proveedor abstracto (DIAN Colombia)
3. **Impresión real** de recibos térmicos y apertura de cajón
4. **Tickets comerciales** personalizables por comercio
5. **Planes y entitlements** con límites por suscripción

---

## 1. WHATSAPP

### 1.1 Contexto

Walos V2 debe permitir que los comercios reciban pedidos por WhatsApp Business API, con flujo automatizado desde mensaje inicial hasta creación de orden en el sistema.

### 1.2 Arquitectura

```
Cliente                WhatsApp Cloud API       Walos Backend           Walos Frontend
   │                          │                       │                       │
   │──"Hola, quiero pedir"──>│                       │                       │
   │                          │──Webhook POST──────>│                       │
   │                          │  (message event)      │                       │
   │                          │                       │──Procesar mensaje──>│
   │                          │                       │  (IA: detectar       │
   │                          │                       │   intención)         │
   │                          │<──Respuesta API──────│                       │
   │<──"¿Qué deseas pedir?"──│                       │                       │
   │                          │                       │                       │
   │──"2 hamburguesas"──────>│──Webhook POST──────>│                       │
   │                          │                       │──Armar pedido────>│
   │                          │                       │  (IA: extraer        │
   │                          │                       │   productos)         │
   │                          │<──Enviar catálogo────│                       │
   │<──Catálogo interactivo──│                       │                       │
   │                          │                       │                       │
   │──Confirmar pedido──────>│──Webhook POST──────>│                       │
   │                          │                       │──Crear orden──────>│
   │                          │                       │  (delivery.orders)   │
   │                          │<──Confirmación───────│                       │
   │<──"Pedido #123 creado"──│                       │                       │
```

### 1.3 Especificación Funcional

#### 1.3.1 Alta / Configuración por Comercio

**Tabla**: `integrations.whatsapp_config`
```sql
CREATE TABLE integrations.whatsapp_config (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    phone_number_id     VARCHAR(50) NOT NULL,        -- WhatsApp Business Phone Number ID
    business_account_id VARCHAR(50) NOT NULL,        -- WhatsApp Business Account ID
    access_token        TEXT NOT NULL,               -- Encrypted token
    webhook_verify_token VARCHAR(100) NOT NULL,      -- Token para verificar webhook
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (company_id)
);
```

**Endpoints**:
```
POST /api/v1/integrations/whatsapp/setup     → Configurar WhatsApp Business
GET  /api/v1/integrations/whatsapp/status    → Estado de integración
PUT  /api/v1/integrations/whatsapp/config    → Actualizar configuración
DELETE /api/v1/integrations/whatsapp         → Desactivar integración
```

**Flujo de configuración**:
1. Usuario admin ingresa a Settings → Integraciones → WhatsApp
2. Hace clic en "Conectar WhatsApp Business"
3. Se redirige a Meta Business Manager para autorizar
4. Meta devuelve `phone_number_id`, `business_account_id`, `access_token`
5. Walos guarda configuración encriptada en DB
6. Walos registra webhook en Meta: `https://api.walos.app/webhooks/whatsapp/{companyId}`

#### 1.3.2 Recepción de Mensajes

**Tabla**: `integrations.whatsapp_messages`
```sql
CREATE TABLE integrations.whatsapp_messages (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    whatsapp_message_id VARCHAR(100) NOT NULL,       -- ID de mensaje de WhatsApp
    from_number         VARCHAR(20) NOT NULL,        -- Número del cliente
    message_type        VARCHAR(20) NOT NULL,        -- text | image | interactive
    message_body        TEXT,                        -- Contenido del mensaje
    media_url           TEXT,                        -- URL de imagen/video
    timestamp           TIMESTAMPTZ NOT NULL,        -- Timestamp del mensaje
    status              VARCHAR(20) NOT NULL DEFAULT 'received', -- received | processed | failed
    conversation_id     BIGINT REFERENCES integrations.whatsapp_conversations(id),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (whatsapp_message_id)
);

CREATE INDEX idx_whatsapp_messages_company ON integrations.whatsapp_messages (company_id, created_at DESC);
CREATE INDEX idx_whatsapp_messages_conversation ON integrations.whatsapp_messages (conversation_id);
```

**Endpoint webhook**:
```
POST /api/v1/webhooks/whatsapp/{companyId}  → Recibir mensaje de WhatsApp
GET  /api/v1/webhooks/whatsapp/{companyId}  → Verificar webhook (Meta)
```

**Flujo de recepción**:
1. Cliente envía mensaje por WhatsApp
2. Meta Cloud API llama webhook de Walos: `POST /webhooks/whatsapp/{companyId}`
3. Walos valida firma HMAC del webhook
4. Walos guarda mensaje en `whatsapp_messages`
5. Walos procesa mensaje con IA (ver 1.3.4)
6. Walos envía respuesta a cliente vía WhatsApp Cloud API

#### 1.3.3 Flujo de Pedidos

**Tabla**: `integrations.whatsapp_conversations`
```sql
CREATE TABLE integrations.whatsapp_conversations (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    customer_phone      VARCHAR(20) NOT NULL,
    customer_name       VARCHAR(200),
    status              VARCHAR(20) NOT NULL DEFAULT 'active', -- active | completed | abandoned
    order_id            BIGINT REFERENCES delivery.orders(id),
    started_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at        TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_whatsapp_conversations_company ON integrations.whatsapp_conversations (company_id, status);
CREATE INDEX idx_whatsapp_conversations_phone ON integrations.whatsapp_conversations (customer_phone, status);
```

**Estados de conversación**:
- `active` → Conversación en curso
- `completed` → Pedido creado exitosamente
- `abandoned` → Cliente no respondió en 24 horas

**Flujo de pedido**:
```
1. Cliente: "Hola, quiero pedir"
   → Sistema: Crea conversación, responde "¿Qué deseas pedir?"

2. Cliente: "2 hamburguesas y 1 coca cola"
   → Sistema: IA extrae productos, responde "Encontré: 2x Hamburguesa Clásica ($15,000), 1x Coca Cola ($3,000). Total: $33,000. ¿Confirmas?"

3. Cliente: "Sí"
   → Sistema: Pide dirección "¿A qué dirección lo enviamos?"

4. Cliente: "Calle 123 #45-67"
   → Sistema: Confirma "Pedido confirmado. Llegará en 30-45 min. Pedido #456"
   → Sistema: Crea orden en delivery.orders
   → Sistema: Marca conversación como completed
```

#### 1.3.4 Catálogo

**Tabla**: `integrations.whatsapp_catalog`
```sql
CREATE TABLE integrations.whatsapp_catalog (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    product_id          BIGINT NOT NULL REFERENCES inventory.products(id),
    whatsapp_product_id VARCHAR(100),                -- ID del producto en Meta Catalog
    is_published        BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (company_id, product_id)
);
```

**Endpoints**:
```
POST /api/v1/integrations/whatsapp/catalog/sync     → Sincronizar catálogo con Meta
GET  /api/v1/integrations/whatsapp/catalog          → Ver catálogo publicado
PUT  /api/v1/integrations/whatsapp/catalog/{id}     → Publicar/despublicar producto
```

**Flujo de catálogo**:
1. Admin activa "Publicar en WhatsApp" en producto
2. Walos llama Meta Graph API: `POST /{catalog_id}/products`
3. Meta retorna `whatsapp_product_id`
4. Walos guarda en `whatsapp_catalog`
5. Cliente puede ver catálogo interactivo en WhatsApp

#### 1.3.5 Resolución de Cliente

**Tabla**: `core.customers` (NUEVA)
```sql
CREATE TABLE core.customers (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    phone               VARCHAR(20) NOT NULL,
    name                VARCHAR(200),
    email               VARCHAR(200),
    address             TEXT,
    notes               TEXT,
    total_orders        INT NOT NULL DEFAULT 0,
    total_spent         DECIMAL(18,2) NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (company_id, phone)
);

CREATE INDEX idx_customers_company ON core.customers (company_id);
CREATE INDEX idx_customers_phone ON core.customers (phone);
```

**Lógica de resolución**:
1. Mensaje entrante con `from_number = "+573001234567"`
2. Buscar en `core.customers` por `phone = "+573001234567"` y `company_id`
3. Si existe → Usar `customer_id` existente
4. Si NO existe → Crear nuevo cliente con `phone`, `name = NULL`
5. Durante conversación, pedir nombre: "¿Cómo te llamas?"
6. Actualizar `core.customers.name` con respuesta

#### 1.3.6 Armado de Pedido

**Lógica de extracción de productos**:
1. Mensaje: "2 hamburguesas y 1 coca cola"
2. Enviar a IA (OpenAI GPT-4):
   ```json
   {
     "system": "Eres un asistente de pedidos. Extrae productos y cantidades del mensaje del cliente. Catálogo disponible: [lista de productos]",
     "user": "2 hamburguesas y 1 coca cola"
   }
   ```
3. IA responde:
   ```json
   {
     "items": [
       {"product_id": 123, "quantity": 2, "name": "Hamburguesa Clásica"},
       {"product_id": 456, "quantity": 1, "name": "Coca Cola 400ml"}
     ],
     "confidence": 0.95
   }
   ```
4. Si `confidence < 0.8` → Pedir aclaración: "¿Te refieres a Hamburguesa Clásica o Hamburguesa Especial?"
5. Si `confidence >= 0.8` → Confirmar con cliente

#### 1.3.7 Confirmación

**Flujo de confirmación**:
1. Sistema envía resumen:
   ```
   Tu pedido:
   - 2x Hamburguesa Clásica ($15,000 c/u)
   - 1x Coca Cola 400ml ($3,000)
   
   Subtotal: $33,000
   Domicilio: $5,000
   Total: $38,000
   
   ¿Confirmas? (Sí/No)
   ```
2. Cliente responde "Sí"
3. Sistema pide dirección (si no existe en `core.customers.address`)
4. Cliente responde "Calle 123 #45-67"
5. Sistema crea orden en `delivery.orders`:
   ```sql
   INSERT INTO delivery.orders (
       company_id, customer_id, customer_phone, delivery_address,
       status, total_amount, source
   ) VALUES (
       1, 789, '+573001234567', 'Calle 123 #45-67',
       'new', 38000, 'whatsapp'
   );
   ```
6. Sistema envía confirmación:
   ```
   ✅ Pedido #456 confirmado
   Llegará en 30-45 minutos
   Total: $38,000
   
   Gracias por tu pedido!
   ```

#### 1.3.8 Dirección

**Validación de dirección**:
- Si cliente tiene `address` guardada → Preguntar "¿Enviamos a [dirección guardada]? (Sí/Otra)"
- Si cliente responde "Otra" → Pedir nueva dirección
- Si cliente NO tiene `address` → Pedir dirección obligatoriamente
- Guardar dirección en `core.customers.address` para próximos pedidos

#### 1.3.9 Delivery

**Integración con módulo Delivery**:
1. Orden creada en `delivery.orders` con `source = 'whatsapp'`
2. Orden aparece en tablero Kanban de Delivery
3. Estados se sincronizan con WhatsApp:
   - `new` → "Pedido recibido"
   - `confirmed` → "Pedido confirmado, estamos preparando"
   - `preparing` → "Tu pedido está en preparación"
   - `dispatched` → "Tu pedido salió, llegará en 15 min"
   - `delivered` → "Pedido entregado. ¡Gracias!"
4. Cada cambio de estado envía mensaje automático al cliente

#### 1.3.10 Creación de Orden

**Endpoint interno**:
```
POST /api/v1/integrations/whatsapp/orders/create
```

**Request**:
```json
{
  "conversation_id": 123,
  "customer_id": 789,
  "items": [
    {"product_id": 123, "quantity": 2},
    {"product_id": 456, "quantity": 1}
  ],
  "delivery_address": "Calle 123 #45-67",
  "notes": "Sin cebolla"
}
```

**Response**:
```json
{
  "success": true,
  "data": {
    "order_id": 456,
    "order_number": "WA-20260914-001",
    "total_amount": 38000,
    "estimated_delivery": "2026-09-14T15:30:00Z"
  }
}
```

#### 1.3.11 Trazabilidad

**Tabla**: `integrations.whatsapp_order_events`
```sql
CREATE TABLE integrations.whatsapp_order_events (
    id                  BIGSERIAL PRIMARY KEY,
    order_id            BIGINT NOT NULL REFERENCES delivery.orders(id),
    event_type          VARCHAR(50) NOT NULL,        -- message_sent | status_changed | error
    event_data          JSONB NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_whatsapp_order_events_order ON integrations.whatsapp_order_events (order_id, created_at DESC);
```

**Eventos registrados**:
- `message_sent` → Mensaje enviado al cliente
- `status_changed` → Estado de orden cambió
- `error` → Error al enviar mensaje o procesar

---

## 2. FACTURACIÓN ELECTRÓNICA

### 2.1 Contexto

Walos V2 debe emitir facturas electrónicas válidas ante DIAN (Colombia) con proveedor abstracto (inicialmente Alegra o FacturaDirecta).

### 2.2 Arquitectura

```
Walos Backend          Proveedor FE          DIAN
     │                      │                  │
     │──Emitir factura────>│                  │
     │  (invoice data)      │                  │
     │                      │──Enviar XML────>│
     │                      │                  │
     │                      │<──Validación────│
     │                      │  (CUFE)          │
     │<──Factura aprobada──│                  │
     │  (PDF + XML)         │                  │
```

### 2.3 Especificación Funcional

#### 2.3.1 Proveedor Abstracto

**Interfaz**: `IElectronicInvoiceProvider`
```csharp
public interface IElectronicInvoiceProvider
{
    Task<InvoiceResult> IssueInvoiceAsync(InvoiceRequest request);
    Task<InvoiceStatus> GetInvoiceStatusAsync(string invoiceId);
    Task<CreditNoteResult> IssueCreditNoteAsync(CreditNoteRequest request);
    Task<byte[]> GetInvoicePdfAsync(string invoiceId);
    Task<string> GetInvoiceXmlAsync(string invoiceId);
}
```

**Implementaciones**:
- `AlegraInvoiceProvider` (prioridad 1)
- `FacturaDirectaInvoiceProvider` (prioridad 2)
- `MockInvoiceProvider` (para testing)

#### 2.3.2 Configuración por Comercio

**Tabla**: `integrations.electronic_invoice_config`
```sql
CREATE TABLE integrations.electronic_invoice_config (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    provider            VARCHAR(50) NOT NULL,        -- alegra | factura_directa
    api_key             TEXT NOT NULL,               -- Encrypted
    api_secret          TEXT,                        -- Encrypted (si aplica)
    test_mode           BOOLEAN NOT NULL DEFAULT TRUE,
    is_active           BOOLEAN NOT NULL DEFAULT FALSE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (company_id)
);
```

**Endpoints**:
```
POST /api/v1/integrations/electronic-invoice/setup     → Configurar proveedor
GET  /api/v1/integrations/electronic-invoice/status    → Estado de integración
PUT  /api/v1/integrations/electronic-invoice/config    → Actualizar configuración
POST /api/v1/integrations/electronic-invoice/test      → Emitir factura de prueba
```

#### 2.3.3 Configuración por Sucursal

**Tabla**: `integrations.branch_invoice_config`
```sql
CREATE TABLE integrations.branch_invoice_config (
    id                  BIGSERIAL PRIMARY KEY,
    branch_id           BIGINT NOT NULL REFERENCES core.branches(id),
    resolution_number   VARCHAR(50) NOT NULL,        -- Número de resolución DIAN
    prefix              VARCHAR(10) NOT NULL,        -- Prefijo de factura (ej: FV)
    current_number      BIGINT NOT NULL DEFAULT 1,   -- Número actual
    start_number        BIGINT NOT NULL,             -- Número inicial
    end_number          BIGINT NOT NULL,             -- Número final
    resolution_date     DATE NOT NULL,               -- Fecha de resolución
    expiration_date     DATE NOT NULL,               -- Fecha de vencimiento
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (branch_id)
);
```

**Lógica de resolución**:
1. Al facturar, verificar `current_number <= end_number`
2. Si `current_number > end_number` → Error: "Resolución agotada"
3. Si `expiration_date < NOW()` → Error: "Resolución vencida"
4. Generar número de factura: `{prefix}{current_number}` (ej: `FV00001234`)
5. Incrementar `current_number` en transacción atómica

#### 2.3.4 Emisión

**Tabla**: `sales.electronic_invoices`
```sql
CREATE TABLE sales.electronic_invoices (
    id                  BIGSERIAL PRIMARY KEY,
    order_id            BIGINT NOT NULL REFERENCES sales.orders(id),
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id           BIGINT NOT NULL REFERENCES core.branches(id),
    invoice_number      VARCHAR(50) NOT NULL,        -- FV00001234
    resolution_number   VARCHAR(50) NOT NULL,
    cufe                VARCHAR(200),                -- Código Único de Factura Electrónica
    status              VARCHAR(20) NOT NULL DEFAULT 'pending', -- pending | issued | rejected | cancelled
    provider_invoice_id VARCHAR(100),                -- ID en proveedor externo
    pdf_url             TEXT,
    xml_url             TEXT,
    error_message       TEXT,
    issued_at           TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (company_id, invoice_number)
);

CREATE INDEX idx_electronic_invoices_order ON sales.electronic_invoices (order_id);
CREATE INDEX idx_electronic_invoices_company ON sales.electronic_invoices (company_id, created_at DESC);
```

**Endpoint**:
```
POST /api/v1/sales/orders/{orderId}/issue-invoice     → Emitir factura electrónica
```

**Request**:
```json
{
  "customer": {
    "identification_type": "CC",
    "identification_number": "1234567890",
    "name": "Juan Pérez",
    "email": "juan@example.com",
    "phone": "+573001234567",
    "address": "Calle 123 #45-67"
  },
  "payment_method": "cash",
  "notes": "Factura de venta"
}
```

**Response**:
```json
{
  "success": true,
  "data": {
    "invoice_id": 789,
    "invoice_number": "FV00001234",
    "cufe": "abc123def456...",
    "pdf_url": "https://storage.walos.app/invoices/FV00001234.pdf",
    "xml_url": "https://storage.walos.app/invoices/FV00001234.xml",
    "issued_at": "2026-09-14T10:30:00Z"
  }
}
```

**Flujo de emisión**:
1. Usuario factura mesa/pedido
2. Sistema valida datos de cliente (NIT/CC obligatorio)
3. Sistema genera número de factura
4. Sistema llama `IElectronicInvoiceProvider.IssueInvoiceAsync()`
5. Proveedor envía XML a DIAN
6. DIAN valida y retorna CUFE
7. Sistema guarda factura en `electronic_invoices`
8. Sistema descarga PDF y XML del proveedor
9. Sistema guarda archivos en Supabase Storage
10. Sistema envía PDF por email al cliente

#### 2.3.5 Consulta de Estado

**Endpoint**:
```
GET /api/v1/sales/invoices/{invoiceId}/status     → Consultar estado de factura
```

**Response**:
```json
{
  "success": true,
  "data": {
    "invoice_number": "FV00001234",
    "status": "issued",
    "cufe": "abc123def456...",
    "issued_at": "2026-09-14T10:30:00Z",
    "dian_status": "accepted"
  }
}
```

**Estados**:
- `pending` → Factura creada, no enviada a DIAN
- `issued` → Factura emitida y aceptada por DIAN
- `rejected` → Factura rechazada por DIAN
- `cancelled` → Factura anulada (nota crédito emitida)

#### 2.3.6 Notas Crédito

**Tabla**: `sales.credit_notes`
```sql
CREATE TABLE sales.credit_notes (
    id                  BIGSERIAL PRIMARY KEY,
    electronic_invoice_id BIGINT NOT NULL REFERENCES sales.electronic_invoices(id),
    credit_note_number  VARCHAR(50) NOT NULL,        -- NC00001234
    cufe                VARCHAR(200),
    reason              TEXT NOT NULL,
    status              VARCHAR(20) NOT NULL DEFAULT 'pending',
    provider_note_id    VARCHAR(100),
    pdf_url             TEXT,
    xml_url             TEXT,
    issued_at           TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (electronic_invoice_id)
);
```

**Endpoint**:
```
POST /api/v1/sales/invoices/{invoiceId}/credit-note     → Emitir nota crédito
```

**Request**:
```json
{
  "reason": "Devolución de mercancía",
  "items": [
    {"product_id": 123, "quantity": 2}
  ]
}
```

#### 2.3.7 PDF / XML

**Generación de archivos**:
1. Proveedor genera PDF y XML según formato DIAN
2. Walos descarga archivos del proveedor
3. Walos guarda en Supabase Storage: `invoices/{companyId}/{invoiceNumber}.pdf`
4. Walos guarda URL en `electronic_invoices.pdf_url`

**Endpoints**:
```
GET /api/v1/sales/invoices/{invoiceId}/pdf     → Descargar PDF
GET /api/v1/sales/invoices/{invoiceId}/xml     → Descargar XML
```

#### 2.3.8 Manejo de Errores / Reintentos

**Tabla**: `sales.invoice_retry_log`
```sql
CREATE TABLE sales.invoice_retry_log (
    id                  BIGSERIAL PRIMARY KEY,
    electronic_invoice_id BIGINT NOT NULL REFERENCES sales.electronic_invoices(id),
    attempt_number      INT NOT NULL,
    error_code          VARCHAR(50),
    error_message       TEXT,
    attempted_at        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

**Lógica de reintentos**:
1. Si emisión falla → Guardar en `invoice_retry_log`
2. Reintentar automáticamente cada 5 minutos (máximo 3 intentos)
3. Si falla 3 veces → Marcar como `rejected`, notificar admin
4. Admin puede reintentar manualmente desde UI

**Errores comunes**:
- `INVALID_NIT` → NIT del cliente inválido
- `RESOLUTION_EXPIRED` → Resolución vencida
- `RESOLUTION_EXHAUSTED` → Numeración agotada
- `DIAN_TIMEOUT` → DIAN no responde
- `INVALID_ITEMS` → Productos sin código estándar

---

## 3. HARDWARE (PRINT AGENT)

### 3.1 Contexto

Walos V2 debe imprimir recibos térmicos reales y abrir cajón de dinero mediante un agente nativo de Windows.

### 3.2 Arquitectura

```
Walos PWA (Browser)    Print Agent (Windows)    Impresora Térmica    Cajón
       │                       │                        │              │
       │──Print request──────>│                        │              │
       │  (orderId, token)     │                        │              │
       │                       │──Fetch receipt data──>│              │
       │                       │  (Walos API)           │              │
       │                       │<──Receipt JSON─────────│              │
       │                       │                        │              │
       │                       │──ESC/POS commands────>│              │
       │                       │                        │──Print──────>│
       │                       │                        │              │
       │                       │──Open drawer cmd─────────────────────>│
       │<──Print success───────│                        │              │
```

### 3.3 Especificación Funcional

#### 3.3.1 Print Agent (Windows Service)

**Tecnología**: .NET 8 Windows Service

**Responsabilidades**:
1. Escuchar requests de impresión desde PWA (localhost HTTP server)
2. Validar tokens de impresión
3. Obtener datos de recibo desde Walos API
4. Generar comandos ESC/POS
5. Enviar a impresora térmica
6. Abrir cajón de dinero

**Puerto**: `http://localhost:8080` (configurable)

**Endpoints**:
```
POST /print/receipt              → Imprimir recibo de venta
POST /print/kitchen              → Imprimir comanda de cocina
POST /print/z-report             → Imprimir reporte Z
POST /drawer/open                → Abrir cajón
GET  /printers                   → Listar impresoras disponibles
GET  /health                     → Health check
GET  /version                    → Versión del agente
```

#### 3.3.2 Impresión Real de Recibo

**Request**:
```json
POST http://localhost:8080/print/receipt
{
  "token": "eyJ...",
  "orderId": 123,
  "printerName": "EPSON TM-T20III"
}
```

**Flujo**:
1. Print Agent valida token (firma JWT con public key de Walos API)
2. Print Agent llama `GET /api/v1/sales/orders/{orderId}/receipt` con token
3. Walos API retorna datos de recibo:
   ```json
   {
     "company": {
       "name": "Restaurante El Buen Sabor",
       "nit": "900123456-7",
       "address": "Calle 123 #45-67",
       "phone": "+573001234567"
     },
     "order": {
       "order_number": "M-001",
       "date": "2026-09-14T10:30:00Z",
       "cashier": "Juan Pérez"
     },
     "items": [
       {"name": "Hamburguesa Clásica", "quantity": 2, "unit_price": 15000, "subtotal": 30000},
       {"name": "Coca Cola 400ml", "quantity": 1, "unit_price": 3000, "subtotal": 3000}
     ],
     "subtotal": 33000,
     "discount": 0,
     "tax": 0,
     "total": 33000,
     "payment_method": "Efectivo",
     "cash_received": 50000,
     "change": 17000
   }
   ```
4. Print Agent genera comandos ESC/POS:
   ```
   ESC @ (reset)
   ESC a 1 (centrar)
   ESC E 1 (negrita)
   "Restaurante El Buen Sabor"
   ESC E 0 (normal)
   "NIT: 900123456-7"
   "Calle 123 #45-67"
   "Tel: +573001234567"
   ESC d 2 (2 líneas en blanco)
   ESC a 0 (izquierda)
   "Fecha: 2026-09-14 10:30"
   "Cajero: Juan Pérez"
   "Mesa: M-001"
   "--------------------------------"
   "2x Hamburguesa Clásica  $30,000"
   "1x Coca Cola 400ml       $3,000"
   "--------------------------------"
   "Subtotal:               $33,000"
   "Total:                  $33,000"
   "--------------------------------"
   "Efectivo:               $50,000"
   "Cambio:                 $17,000"
   ESC d 3 (3 líneas)
   ESC a 1 (centrar)
   "Gracias por su compra!"
   ESC d 5 (5 líneas)
   ESC i (cortar papel)
   ```
5. Print Agent envía comandos a impresora
6. Print Agent retorna `200 OK` a PWA

#### 3.3.3 Apertura de Cajón

**Request**:
```json
POST http://localhost:8080/drawer/open
{
  "token": "eyJ...",
  "printerName": "EPSON TM-T20III"
}
```

**Comando ESC/POS**:
```
ESC p 0 25 250
```
- `ESC p` = Comando de pulso
- `0` = Pin del cajón (drawer kick pin 0)
- `25` = Tiempo ON (25 * 2ms = 50ms)
- `250` = Tiempo OFF (250 * 2ms = 500ms)

**Flujo**:
1. Print Agent valida token (requiere rol `admin` o `manager`)
2. Print Agent envía comando ESC/POS a impresora
3. Impresora activa pin del cajón
4. Cajón se abre mecánicamente

#### 3.3.4 Instalación / Actualización

**Instalador**: `WalosPrintAgent-Setup.exe` (Inno Setup)

**Proceso de instalación**:
1. Usuario descarga instalador desde `https://walos.app/downloads/print-agent`
2. Ejecuta instalador (requiere permisos admin)
3. Instalador copia archivos a `C:\Program Files\Walos\PrintAgent\`
4. Instalador crea servicio de Windows: `WalosPrintAgent`
5. Instalador configura auto-start
6. Instalador abre puerto 8080 en firewall
7. Instalador muestra icono en system tray

**Proceso de actualización**:
1. Print Agent verifica versión cada 24 horas: `GET https://api.walos.app/print-agent/latest-version`
2. Si hay nueva versión → Muestra notificación en system tray
3. Usuario hace clic en "Actualizar"
4. Print Agent descarga nuevo instalador
5. Print Agent ejecuta instalador en modo silencioso
6. Servicio se reinicia automáticamente

#### 3.3.5 Impresoras Múltiples

**Configuración**:
```json
{
  "printers": {
    "receipt": "EPSON TM-T20III",
    "kitchen": "Star TSP143III",
    "bar": "Bixolon SRP-350III"
  }
}
```

**Lógica**:
- Recibo de venta → `receipt` printer
- Comanda de cocina → `kitchen` printer
- Comanda de bar → `bar` printer
- Si printer específico no configurado → Usar impresora por defecto del sistema

#### 3.3.6 Preparación para Báscula

**Interfaz**: `IScaleReader`
```csharp
public interface IScaleReader
{
    Task<decimal> ReadWeightAsync();
    Task<bool> IsConnectedAsync();
    Task<string> GetModelAsync();
}
```

**Implementaciones futuras**:
- `SerialScaleReader` (báscula serial RS-232)
- `UsbScaleReader` (báscula USB)
- `MockScaleReader` (para testing)

**Configuración**:
```json
{
  "scale": {
    "enabled": false,
    "port": "COM3",
    "baudRate": 9600,
    "model": "RHINO BPS-30"
  }
}
```

---

## 4. TICKET COMERCIAL

### 4.1 Contexto

Cada comercio debe poder personalizar el ticket impreso con su logo, datos y mensajes.

### 4.2 Especificación Funcional

#### 4.2.1 Personalización

**Tabla**: `core.receipt_templates`
```sql
CREATE TABLE core.receipt_templates (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    template_name       VARCHAR(100) NOT NULL,       -- default | custom
    header_text         TEXT,                        -- Texto superior
    footer_text         TEXT,                        -- Texto inferior
    show_logo           BOOLEAN NOT NULL DEFAULT TRUE,
    show_nit            BOOLEAN NOT NULL DEFAULT TRUE,
    show_address        BOOLEAN NOT NULL DEFAULT TRUE,
    show_phone          BOOLEAN NOT NULL DEFAULT TRUE,
    show_qr_code        BOOLEAN NOT NULL DEFAULT FALSE,
    paper_width         INT NOT NULL DEFAULT 80,     -- 58 | 80 mm
    font_size           VARCHAR(20) NOT NULL DEFAULT 'normal', -- small | normal | large
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (company_id, template_name)
);
```

**Endpoints**:
```
GET  /api/v1/company/receipt-template          → Obtener template activo
PUT  /api/v1/company/receipt-template          → Actualizar template
POST /api/v1/company/receipt-template/preview  → Vista previa de ticket
```

#### 4.2.2 Logo

**Lógica**:
1. Si `show_logo = true` → Imprimir logo en formato raster (ESC/POS GS v 0)
2. Logo debe ser monocromático (blanco/negro)
3. Resolución máxima: 384px ancho (para 80mm) o 256px (para 58mm)
4. Logo se convierte a bitmap y se envía como comandos ESC/POS

**Comando ESC/POS**:
```
GS v 0 m xL xH yL yH d1...dk
```

#### 4.2.3 Datos de Comercio

**Campos configurables**:
- Nombre comercial (`company.display_name`)
- NIT (`company.tax_id`)
- Dirección (`company.address`)
- Teléfono (`company.phone`)
- Email (`company.email`)
- Sitio web (`company.website`)

**Ejemplo de header**:
```
        [LOGO]
   Restaurante El Buen Sabor
   NIT: 900123456-7
   Calle 123 #45-67, Bogotá
   Tel: +573001234567
   www.elbuensabor.com
```

#### 4.2.4 Mensajes

**Campos**:
- `header_text` → Mensaje superior (ej: "Bienvenido!")
- `footer_text` → Mensaje inferior (ej: "Gracias por su compra. Vuelva pronto!")

**Ejemplo**:
```
================================
     ¡Bienvenido!
================================
[... contenido del ticket ...]
================================
  Gracias por su compra
  Vuelva pronto!
  Síguenos en @elbuensabor
================================
```

#### 4.2.5 Layout 58/80 mm

**58mm**:
- Ancho: 32 caracteres
- Logo máximo: 256px
- Fuente: Condensada

**80mm**:
- Ancho: 48 caracteres
- Logo máximo: 384px
- Fuente: Normal

**Adaptación automática**:
1. Print Agent detecta ancho de papel configurado
2. Ajusta layout según ancho
3. Trunca líneas largas si exceden ancho

---

## 5. PLANES / ENTITLEMENTS

### 5.1 Contexto

Walos Fundadores tendrá 3 planes con límites diferenciados.

### 5.2 Planes

| Plan | Precio | Usuarios | Sucursales | Productos | Facturas/mes | WhatsApp | Soporte |
|------|--------|----------|------------|-----------|--------------|----------|---------|
| **Starter** | $99,000/mes | 3 | 1 | 500 | 100 | ❌ | Email |
| **Business** | $199,000/mes | 10 | 3 | 2,000 | 500 | ✅ | Email + Chat |
| **Enterprise** | $399,000/mes | Ilimitado | Ilimitado | Ilimitado | Ilimitado | ✅ | Prioritario |

### 5.3 Especificación Funcional

#### 5.3.1 Planes

**Tabla**: `platform.subscription_plans` (ya existe en migración `013_platform_billing.sql`)

**Planes predefinidos**:
```sql
INSERT INTO platform.subscription_plans (name, code, price, billing_cycle, features) VALUES
('Starter', 'starter', 99000, 'monthly', '{"users": 3, "branches": 1, "products": 500, "invoices_per_month": 100, "whatsapp": false}'),
('Business', 'business', 199000, 'monthly', '{"users": 10, "branches": 3, "products": 2000, "invoices_per_month": 500, "whatsapp": true}'),
('Enterprise', 'enterprise', 399000, 'monthly', '{"users": -1, "branches": -1, "products": -1, "invoices_per_month": -1, "whatsapp": true}');
```

#### 5.3.2 Entitlements

**Tabla**: `platform.company_entitlements`
```sql
CREATE TABLE platform.company_entitlements (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    subscription_id     BIGINT NOT NULL REFERENCES platform.subscriptions(id),
    feature_code        VARCHAR(50) NOT NULL,        -- users | branches | products | invoices_per_month | whatsapp
    limit_value         INT NOT NULL,                -- -1 = ilimitado
    current_usage       INT NOT NULL DEFAULT 0,
    reset_period        VARCHAR(20),                 -- monthly | never
    last_reset_at       TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (company_id, feature_code)
);

CREATE INDEX idx_company_entitlements_company ON platform.company_entitlements (company_id);
```

**Lógica de entitlements**:
1. Al crear suscripción → Crear entitlements según plan
2. Al crear usuario → Verificar `entitlements.users.current_usage < limit_value`
3. Al crear sucursal → Verificar `entitlements.branches.current_usage < limit_value`
4. Al crear producto → Verificar `entitlements.products.current_usage < limit_value`
5. Al emitir factura → Verificar `entitlements.invoices_per_month.current_usage < limit_value`
6. Si límite excedido → Error: "Límite de [feature] alcanzado. Actualiza tu plan."

#### 5.3.3 Límites por Plan

**Middleware**: `EntitlementMiddleware.cs`
```csharp
public class EntitlementMiddleware
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenant, IEntitlementService entitlements)
    {
        var endpoint = context.GetEndpoint();
        var requiredFeature = endpoint?.Metadata.GetMetadata<RequireEntitlementAttribute>()?.Feature;
        
        if (requiredFeature != null)
        {
            var hasAccess = await entitlements.CheckAccessAsync(tenant.CompanyId, requiredFeature);
            if (!hasAccess)
            {
                context.Response.StatusCode = 402; // Payment Required
                await context.Response.WriteAsJsonAsync(new { message = "Límite alcanzado. Actualiza tu plan." });
                return;
            }
        }
        
        await _next(context);
    }
}
```

**Uso**:
```csharp
[HttpPost("users")]
[RequireEntitlement("users")]
public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
{
    // Si límite de usuarios alcanzado, middleware retorna 402
    var user = await _usersService.CreateUserAsync(request);
    return Ok(user);
}
```

#### 5.3.4 Límites de Facturación Electrónica

**Lógica**:
1. Al emitir factura electrónica → Incrementar `entitlements.invoices_per_month.current_usage`
2. Si `current_usage >= limit_value` → Error: "Límite de facturas alcanzado este mes"
3. Cada 1ro de mes → Reset `current_usage = 0` para `invoices_per_month`

**Endpoint**:
```
GET /api/v1/company/entitlements/usage     → Ver uso actual de entitlements
```

**Response**:
```json
{
  "success": true,
  "data": {
    "plan": "Business",
    "entitlements": {
      "users": {"limit": 10, "current": 5, "available": 5},
      "branches": {"limit": 3, "current": 2, "available": 1},
      "products": {"limit": 2000, "current": 450, "available": 1550},
      "invoices_per_month": {"limit": 500, "current": 123, "available": 377, "resets_at": "2026-10-01T00:00:00Z"},
      "whatsapp": {"enabled": true}
    }
  }
}
```

#### 5.3.5 Módulos Habilitados

**Lógica**:
- `whatsapp = false` → Ocultar sección WhatsApp en Settings
- `whatsapp = true` → Mostrar configuración de WhatsApp

**Frontend**:
```jsx
const { entitlements } = useEntitlements();

{entitlements.whatsapp.enabled && (
  <SettingsSection title="WhatsApp">
    <WhatsAppConfig />
  </SettingsSection>
)}
```

---

## RESUMEN DE CONTRATO V2

### Módulos Nuevos

1. ✅ **WhatsApp** (10 endpoints, 6 tablas)
2. ✅ **Facturación Electrónica** (8 endpoints, 5 tablas)
3. ✅ **Print Agent** (6 endpoints, servicio Windows)
4. ✅ **Ticket Comercial** (3 endpoints, 1 tabla)
5. ✅ **Planes/Entitlements** (2 endpoints, 1 tabla)

### Tablas Nuevas

| Esquema | Tabla | Propósito |
|---------|-------|-----------|
| `integrations` | `whatsapp_config` | Configuración WhatsApp por comercio |
| `integrations` | `whatsapp_messages` | Mensajes recibidos |
| `integrations` | `whatsapp_conversations` | Conversaciones activas |
| `integrations` | `whatsapp_catalog` | Productos publicados en WhatsApp |
| `integrations` | `whatsapp_order_events` | Trazabilidad de pedidos |
| `integrations` | `electronic_invoice_config` | Configuración FE por comercio |
| `integrations` | `branch_invoice_config` | Resolución DIAN por sucursal |
| `sales` | `electronic_invoices` | Facturas electrónicas emitidas |
| `sales` | `credit_notes` | Notas crédito |
| `sales` | `invoice_retry_log` | Log de reintentos de emisión |
| `core` | `customers` | Clientes del comercio |
| `core` | `receipt_templates` | Templates de tickets |
| `platform` | `company_entitlements` | Límites por plan |

**Total tablas nuevas**: 13

### Endpoints Nuevos

**WhatsApp**: 10  
**Facturación Electrónica**: 8  
**Print Agent**: 6  
**Ticket Comercial**: 3  
**Entitlements**: 2  

**Total endpoints nuevos**: 29

---

**FIN DE CONTRATO TO-BE V2**
