# Pending: Módulo Facturación Electrónica (DIAN)

> Estado: **PENDIENTE — No ejecutar hasta aprobación**  
> Creado: 2026-04-21  
> Proveedor inicial: **Alegra** (arquitectura multi-proveedor)

---

## 0. Arquitectura — Principio Clave

**Walos NO depende directamente de Alegra.** Se usa una capa de abstracción:

```
Walos API
 └── Facturacion Module
      ├── IFacturacionProvider   ← interfaz
      ├── AlegraProvider         ← implementación actual
      └── (futuro) FactusProvider, SiigoProvider, etc.
```

Así el proveedor es intercambiable sin tocar la lógica de negocio.

---

## 1. Historias de Usuario

### HU-01 — Activar facturación electrónica
> Como dueño del negocio, quiero conectar mi cuenta de Alegra para emitir facturas electrónicas.

**Criterios:**
- Guardar credenciales API del proveedor (cifradas)
- Validar conexión antes de guardar
- Asociar configuración a `company_id`
- Mostrar estado de conexión activa/inactiva

### HU-02 — Facturar venta desde POS
> Como cajero, quiero emitir factura electrónica desde una venta para cumplir con DIAN.

**Criterios:**
- Toggle "Facturar electrónicamente" en cierre de venta
- Seleccionar cliente existente o crear uno nuevo
- Enviar al proveedor y guardar CUFE + estado
- Mostrar confirmación o error al cajero

### HU-03 — Facturar consumidor final
> Como cajero, quiero facturar sin datos del cliente para agilizar ventas.

**Criterios:**
- Cliente genérico automático (`222222222222` / Consumidor Final)
- Documento tipo consumidor final
- Sin email requerido

### HU-04 — Consultar estado de factura
> Como admin, quiero ver si la factura fue aceptada por DIAN para control contable.

**Criterios:**
- Ver CUFE, estado (pending / accepted / rejected)
- Botón para refrescar estado desde el proveedor
- Ver respuesta DIAN en detalle (JSONB)

### HU-05 — Manejo de errores y reintentos
> Como sistema, quiero registrar errores del proveedor para reintentos y auditoría.

**Criterios:**
- Guardar error en BD con timestamp
- Endpoint de reintento manual
- Máximo 3 reintentos automáticos con backoff

### HU-06 — Integración con Agente IA
> Como usuario, quiero decirle al asistente "Factura esta mesa" para emitir sin ir al menú.

**Criterios:**
- IA dispara acción `create_invoice` con `saleId` y datos del cliente
- Responde con estado de la factura generada
- Diferenciador clave vs competidores (Siigo, etc.)

---

## 2. Modelo de Datos

### Tabla: `billing.electronic_invoices`
```sql
CREATE TABLE billing.electronic_invoices (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id           BIGINT REFERENCES core.branches(id),
    sale_id             BIGINT,                        -- FK a sales cuando exista

    -- Proveedor
    provider            VARCHAR(30) NOT NULL,          -- alegra | factus | siigo
    provider_invoice_id VARCHAR(200),                  -- ID en el sistema externo

    -- Cliente
    customer_name       VARCHAR(300) NOT NULL,
    customer_document   VARCHAR(50) NOT NULL,
    customer_document_type VARCHAR(10) DEFAULT 'CC',   -- CC | NIT | CE | PP | CF
    customer_email      VARCHAR(200),
    is_final_consumer   BOOLEAN NOT NULL DEFAULT FALSE,

    -- Montos
    subtotal            DECIMAL(18,2) NOT NULL DEFAULT 0,
    tax_amount          DECIMAL(18,2) NOT NULL DEFAULT 0,
    tax_rate            DECIMAL(5,2) NOT NULL DEFAULT 19,  -- 19% | 5% | 0%
    total               DECIMAL(18,2) NOT NULL DEFAULT 0,

    -- DIAN
    cufe                VARCHAR(500),
    prefix              VARCHAR(20),
    invoice_number      VARCHAR(50),
    resolution_number   VARCHAR(100),

    -- Estado
    status              VARCHAR(20) NOT NULL DEFAULT 'pending',  -- pending | sent | accepted | rejected | error
    error_message       TEXT,
    retry_count         INT NOT NULL DEFAULT 0,
    dian_response       JSONB,

    -- Auditoría
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    sent_at             TIMESTAMPTZ,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by          BIGINT
);

CREATE INDEX ON billing.electronic_invoices (company_id, status);
CREATE INDEX ON billing.electronic_invoices (sale_id);
CREATE INDEX ON billing.electronic_invoices (provider_invoice_id);
```

### Tabla: `billing.facturation_settings`
```sql
CREATE TABLE billing.facturation_settings (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL UNIQUE REFERENCES core.companies(id),

    -- Proveedor
    provider_name       VARCHAR(30) NOT NULL DEFAULT 'alegra',  -- alegra | factus
    api_key_enc         TEXT,                   -- cifrada AES-256
    api_url             VARCHAR(500),           -- base URL del proveedor

    -- Resolución DIAN
    resolution_number   VARCHAR(100),
    resolution_date     DATE,
    prefix              VARCHAR(20),            -- FE, FV, etc.
    technical_key       TEXT,                   -- clave técnica DIAN
    range_from          INT,
    range_to            INT,
    range_expires_at    DATE,

    -- Estado
    enabled             BOOLEAN NOT NULL DEFAULT FALSE,
    last_validated_at   TIMESTAMPTZ,
    validation_error    TEXT,

    -- Auditoría
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### Tabla: `billing.customers`
```sql
CREATE TABLE billing.customers (
    id                      BIGSERIAL PRIMARY KEY,
    company_id              BIGINT NOT NULL REFERENCES core.companies(id),

    name                    VARCHAR(300) NOT NULL,
    document                VARCHAR(50) NOT NULL,
    document_type           VARCHAR(10) NOT NULL DEFAULT 'CC',  -- CC | NIT | CE | PP
    email                   VARCHAR(200),
    phone                   VARCHAR(30),
    address                 VARCHAR(500),
    city                    VARCHAR(100),

    is_final_consumer       BOOLEAN NOT NULL DEFAULT FALSE,
    provider_customer_id    VARCHAR(200),       -- ID del cliente en Alegra/proveedor

    is_active               BOOLEAN NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    UNIQUE (company_id, document)
);

CREATE INDEX ON billing.customers (company_id);

-- Cliente genérico "Consumidor Final" se inserta en seed
```

---

## 3. Endpoints Internos (Walos Backend)

### Configuración
```
POST   /api/v1/facturation/connect          ← Guardar y validar credenciales
GET    /api/v1/facturation/status           ← Estado de conexión con proveedor
PUT    /api/v1/facturation/settings         ← Actualizar resolución DIAN, prefijo, etc.
DELETE /api/v1/facturation/disconnect       ← Desconectar proveedor
```

### Clientes
```
GET    /api/v1/facturation/customers        ← Lista de clientes para facturar
POST   /api/v1/facturation/customers        ← Crear cliente
GET    /api/v1/facturation/customers/{id}
PUT    /api/v1/facturation/customers/{id}
```

### Facturación
```
POST   /api/v1/facturation/invoice          ← EL endpoint clave
GET    /api/v1/facturation/invoice/{id}     ← Consultar estado
POST   /api/v1/facturation/invoice/{id}/retry  ← Reintento manual
GET    /api/v1/facturation/invoices         ← Historial con filtros
```

#### Body POST /invoice
```json
{
  "saleId": 123,
  "customerId": null,
  "customer": {
    "name": "Consumidor Final",
    "document": "222222222222",
    "documentType": "CF",
    "email": null,
    "isFinalConsumer": true
  },
  "items": [
    {
      "description": "Cerveza Corona 355ml",
      "quantity": 2,
      "unitPrice": 5000,
      "taxRate": 19
    }
  ]
}
```

#### Response
```json
{
  "invoiceId": 45,
  "status": "pending",
  "providerInvoiceId": "alegra-123",
  "cufe": null,
  "message": "Factura enviada, pendiente confirmación DIAN"
}
```

---

## 4. Interfaz IFacturacionProvider (C#)

```csharp
public interface IFacturacionProvider
{
    Task<bool> ValidateConnectionAsync(FacturationSettings settings);
    Task<InvoiceResponse> CreateInvoiceAsync(InvoiceRequest request, FacturationSettings settings);
    Task<InvoiceStatusResponse> GetStatusAsync(string providerInvoiceId, FacturationSettings settings);
    Task<List<ProviderCustomer>> SyncCustomersAsync(FacturationSettings settings);
}

public record InvoiceRequest(
    string CustomerName,
    string CustomerDocument,
    string CustomerDocumentType,
    string? CustomerEmail,
    bool IsFinalConsumer,
    List<InvoiceItemRequest> Items,
    decimal Subtotal,
    decimal TaxAmount,
    decimal Total
);

public record InvoiceResponse(
    string ProviderInvoiceId,
    string Status,
    string? Cufe,
    string? InvoiceNumber,
    object? RawResponse
);

public record InvoiceStatusResponse(
    string Status,        // pending | accepted | rejected
    string? Cufe,
    string? ErrorMessage,
    object? DianResponse
);
```

---

## 5. AlegraProvider — Mapeo

```
Walos InvoiceRequest
        ↓
  Map → Alegra JSON format
        ↓
  POST https://app.alegra.com/api/r1/invoices
        ↓
  Map Alegra response → Walos InvoiceResponse
        ↓
  Guardar en billing.electronic_invoices
        ↓
  Webhook Alegra (estado DIAN) → actualizar status + CUFE
```

### Campos críticos Alegra
- `numberTemplate.id` — resolución configurada
- `seller` — usuario Alegra
- `client.id` — cliente en Alegra (sync previo)
- `items[].tax[].id` — ID del impuesto en Alegra (IVA 19%)
- `stamp.uuid` — CUFE devuelto por Alegra

---

## 6. Flujo Completo

```
POS (React) — toggle "Facturar electrónicamente"
        ↓
POST /api/v1/facturation/invoice
        ↓
FacturacionService
  → Carga facturation_settings del company
  → Resuelve cliente (busca o crea consumidor final)
  → Construye InvoiceRequest
        ↓
IFacturacionProvider (inyectado por DI según provider_name)
        ↓
AlegraProvider.CreateInvoiceAsync()
  → Mapea → POST Alegra API
  → Recibe respuesta
        ↓
Guarda en billing.electronic_invoices
  status = 'sent', provider_invoice_id = 'alegra-xxx'
        ↓
Webhook Alegra → POST /api/v1/facturation/webhooks/alegra
  → Actualiza status, CUFE, dian_response
        ↓
Notificación in-app al admin si rejected
```

---

## 7. Integración con Agente IA

El asistente puede disparar la acción desde lenguaje natural:

**Frases soportadas:**
- "Factura la mesa 3"
- "Factura a consumidor final"
- "Factura a nombre de Carlos Rodríguez cédula 12345678"

**Acción que dispara la IA:**
```json
{
  "action": "create_invoice",
  "saleId": 123,
  "customer": {
    "name": "Consumidor Final",
    "document": "222222222222",
    "documentType": "CF",
    "isFinalConsumer": true
  }
}
```

> 💡 Diferenciador clave vs Siigo y otros POS del mercado colombiano.

---

## 8. Consideraciones Obligatorias DIAN

| Item | Detalle |
|------|---------|
| **Tipos de documento** | CC, NIT, CE, PP, CF (consumidor final) |
| **IVA** | 19% (estándar), 5% (algunos alimentos), 0% (excluidos), Excluido |
| **Numeración** | Resolución DIAN con rango autorizado (range_from → range_to) |
| **Prefijos** | FE, FV, etc. según resolución |
| **CUFE** | UUID generado por DIAN al aceptar — obligatorio guardar |
| **Notas crédito** | Futuro — para devoluciones/anulaciones |
| **Clave técnica** | Para validación firma digital Alegra/DIAN |
| **Ambiente** | Habilitación (pruebas) vs Producción |
| **Fecha vencimiento resolución** | Alertar al admin cuando se acerque |

---

## 9. Frontend

### Panel Admin — Configuración Facturación
**Ruta:** `/settings/e-invoicing`
- Form conexión proveedor (Alegra): email + token API
- Botón "Validar conexión"
- Datos resolución DIAN: número, prefijo, rango, fecha vencimiento
- Estado habilitado/deshabilitado
- Contador de facturas emitidas en el período

### POS — Flujo de Facturación
- En cierre de venta: checkbox "Emitir factura electrónica"
- Si activo: modal con buscador de cliente o "Consumidor Final"
- Crear cliente rápido (nombre + documento)
- Estado de la factura en tiempo real (spinner → ✅ aceptada / ❌ rechazada)

### Historial de Facturas
**Ruta:** `/invoices` o dentro de ventas
- Tabla con: número, cliente, total, estado DIAN, CUFE, fecha
- Filtros: estado, rango fechas
- Botón "Reintentar" en las rechazadas
- Detalle con respuesta DIAN completa

---

## 10. Orden de Implementación

| # | Paso | Dependencia | Estimado |
|---|------|------------|---------|
| 1 | Migración BD (`012_billing_schema.sql`) | — | 1 sesión |
| 2 | Entidad + DTO + Interfaz `IFacturacionProvider` | 1 | 1 sesión |
| 3 | `AlegraProvider` — mapeo + llamadas API | 2 | 2 sesiones |
| 4 | `FacturacionService` + repositorio | 2-3 | 1 sesión |
| 5 | Controladores backend (config + invoice + customers) | 4 | 1 sesión |
| 6 | Webhook Alegra (estado DIAN) | 5 | 1 sesión |
| 7 | Frontend — settings conexión + resolución DIAN | 5 | 1 sesión |
| 8 | Frontend — flujo POS facturación + cliente | 5 | 1 sesión |
| 9 | Frontend — historial facturas | 5 | 1 sesión |
| 10 | Integración acción IA `create_invoice` | 4 | 1 sesión |
| 11 | Testing E2E en ambiente habilitación Alegra | Todo | 1 sesión |

---

## 11. Variables de Entorno Nuevas

```env
# Cifrado credenciales proveedores
FACTURATION_KEY_ENCRYPTION_SECRET=<32-bytes-hex>

# Alegra (solo para validaciones internas si aplica)
# Las credenciales de cada comercio van en BD cifradas

# Webhook secret Alegra
ALEGRA_WEBHOOK_SECRET=<secret>
```

---

## 12. Proveedores Futuros (roadmap)

| Proveedor | País | Notas |
|-----------|------|-------|
| **Alegra** | Colombia, Latam | Proveedor inicial |
| **Factus** | Colombia | Alternativa económica |
| **Siigo** | Colombia | Competidor pero tiene API |
| **FEL** | Guatemala, El Salvador | Expansión regional |

> Al implementar `IFacturacionProvider` correctamente, agregar un proveedor nuevo es solo crear una nueva clase `XxxProvider` sin tocar nada más.

---

> **Próximo paso cuando se apruebe:** Ejecutar Paso 1 — crear `012_billing_schema.sql`  
> **Nota:** Coordinar con plan de billing B2B (`pending-billing-ai-keys.md`) ya que `e_invoicing` es un servicio del catálogo de suscripciones.
