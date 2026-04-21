# Pending: Módulo de Pagos QR / Digital (Wompi)

> Estado: **PENDIENTE — No ejecutar hasta aprobación**  
> Creado: 2026-04-21  
> Proveedor inicial: **Wompi** (arquitectura multi-proveedor)

---

## 0. Arquitectura — Principio Clave

Mismo patrón que facturación electrónica — desacoplado del proveedor:

```
Walos
 └── Payments Module
      ├── IPaymentProvider      ← interfaz
      ├── WompiProvider         ← implementación actual
      ├── (futuro) PayUProvider
      ├── PaymentService        ← lógica de negocio
      └── WebhookHandler        ← manejo de eventos externos
```

> La misma integración Wompi sirve tanto para **pagos en POS** (QR, Nequi, PSE)
> como para **cobros automáticos de facturación B2B** (ver `pending-billing-ai-keys.md`).

---

## 1. Historias de Usuario

### HU-01 — Generar pago QR
> Como cajero, quiero generar un QR desde una venta para que el cliente pague con Nequi / Daviplata.

**Criterios:**
- Botón "Cobrar con QR" en POS
- Genera QR dinámico para el monto exacto de la venta
- Guarda referencia interna y estado = `pending`
- QR visible en pantalla grande (modo quiosco)

### HU-02 — Confirmación automática de pago
> Como sistema, quiero recibir confirmación del pago para cerrar la venta automáticamente.

**Criterios:**
- Webhook de Wompi recibido y validado (firma obligatoria)
- Cambiar estado de `pending` → `approved`
- Cerrar mesa / venta automáticamente al confirmar
- Notificación in-app al cajero

### HU-03 — Estado en tiempo real
> Como cajero, quiero ver si el pago ya se realizó para saber cuándo cerrar la mesa.

**Criterios:**
- Polling cada 3 segundos mientras QR está activo
- O WebSocket si está disponible
- Animación ✅ "Pago recibido" + cierre automático
- Timeout configurable (ej. QR expira en 10 min)

### HU-04 — Múltiples métodos de pago digital
> Como cajero, quiero ofrecer Nequi, PSE o tarjeta según lo que prefiera el cliente.

**Criterios:**
- Selector de método: QR (Nequi/Daviplata), PSE, Tarjeta
- Flujo diferente para PSE (redirección) vs QR (mostrar código)
- Efectivo sigue siendo opción nativa sin proveedor externo

### HU-05 — Integración con Agente IA
> Como usuario, quiero decirle al asistente "cobra esta mesa con Nequi" para iniciar el cobro.

**Criterios:**
- IA dispara acción `create_payment` con `saleId` + método
- Responde con QR o link de pago
- Diferenciador competitivo clave

---

## 2. Modelo de Datos

### Schema: `payments`
```sql
CREATE SCHEMA IF NOT EXISTS payments;
```

### Tabla: `payments.transactions`
```sql
CREATE TABLE payments.transactions (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id           BIGINT REFERENCES core.branches(id),
    sale_id             BIGINT,                        -- FK a sales

    -- Proveedor
    provider            VARCHAR(30) NOT NULL DEFAULT 'wompi',  -- wompi | payu | manual
    provider_payment_id VARCHAR(300),                  -- ID en Wompi
    reference           VARCHAR(100) NOT NULL UNIQUE,  -- WALOS-{companyId}-{timestamp}

    -- Método
    payment_method      VARCHAR(30) NOT NULL,           -- qr | nequi | pse | card | cash
    payment_method_detail VARCHAR(100),                 -- banco PSE, últimos 4 TC, etc.

    -- Montos
    amount              DECIMAL(18,2) NOT NULL,
    amount_in_cents     BIGINT NOT NULL,
    currency            VARCHAR(3) NOT NULL DEFAULT 'COP',

    -- QR / Checkout
    qr_code_url         TEXT,                           -- URL del QR de Wompi
    checkout_url        TEXT,                           -- link de pago (PSE)
    redirect_url        VARCHAR(500),                   -- a dónde redirigir post-pago

    -- Estado
    status              VARCHAR(20) NOT NULL DEFAULT 'pending',
    -- pending | approved | rejected | voided | error

    -- Respuesta proveedor
    provider_response   JSONB,
    error_message       TEXT,

    -- Auditoría
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    confirmed_at        TIMESTAMPTZ,
    expires_at          TIMESTAMPTZ,                    -- cuándo expira el QR
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by          BIGINT
);

CREATE INDEX ON payments.transactions (company_id, status);
CREATE INDEX ON payments.transactions (reference);
CREATE INDEX ON payments.transactions (sale_id);
CREATE INDEX ON payments.transactions (provider_payment_id);
CREATE INDEX ON payments.transactions (expires_at) WHERE status = 'pending';
```

### Tabla: `payments.webhook_events`
```sql
CREATE TABLE payments.webhook_events (
    id              BIGSERIAL PRIMARY KEY,
    provider        VARCHAR(30) NOT NULL,
    event_type      VARCHAR(100),                       -- transaction.updated, etc.
    payload         JSONB NOT NULL,
    signature       TEXT,
    is_valid        BOOLEAN,
    processed       BOOLEAN NOT NULL DEFAULT FALSE,
    error           TEXT,
    received_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    processed_at    TIMESTAMPTZ
);
-- Tabla de auditoría: guardar TODOS los webhooks recibidos antes de procesarlos
```

---

## 3. Endpoints Internos (Walos Backend)

### Pagos
```
POST   /api/v1/payments                     ← Crear pago (QR, PSE, tarjeta)
GET    /api/v1/payments/{id}                ← Consultar estado
GET    /api/v1/payments/by-sale/{saleId}    ← Pagos de una venta
POST   /api/v1/payments/{id}/void           ← Anular pago (si aplica)
```

### Webhooks
```
POST   /api/v1/payments/webhook/wompi       ← Recibe eventos de Wompi (público, sin auth)
```

#### Body POST /payments
```json
{
  "saleId": 123,
  "amount": 120000,
  "method": "qr",
  "customerEmail": "cliente@correo.com",
  "redirectUrl": "https://app.walos.co/pago-exitoso"
}
```

#### Response
```json
{
  "paymentId": 45,
  "reference": "WALOS-1-1714567890",
  "qrCodeUrl": "https://checkout.wompi.co/p/abc123",
  "checkoutUrl": "https://checkout.wompi.co/p/abc123",
  "status": "pending",
  "expiresAt": "2026-04-21T13:00:00Z"
}
```

---

## 4. Interfaz IPaymentProvider (C#)

```csharp
public interface IPaymentProvider
{
    Task<PaymentResponse> CreatePaymentAsync(PaymentRequest request);
    Task<PaymentStatusResponse> GetStatusAsync(string providerPaymentId);
    Task<bool> ValidateWebhookAsync(string payload, string signature);
    Task<PaymentResponse?> VoidPaymentAsync(string providerPaymentId);
}

public record PaymentRequest(
    string Reference,           // WALOS-{companyId}-{timestamp}
    decimal Amount,
    long AmountInCents,
    string Currency,
    string Method,              // QR | PSE | CARD | NEQUI
    string? CustomerEmail,
    string? RedirectUrl,
    string? CustomerPhone
);

public record PaymentResponse(
    string ProviderPaymentId,
    string Status,
    string? QrCodeUrl,
    string? CheckoutUrl,
    DateTime? ExpiresAt,
    object? RawResponse
);

public record PaymentStatusResponse(
    string Status,              // PENDING | APPROVED | DECLINED | VOIDED | ERROR
    string? ProviderPaymentId,
    string? PaymentMethodDetail,
    DateTime? ConfirmedAt,
    object? RawResponse
);
```

---

## 5. WompiProvider — Mapeo

### Request a Wompi
```json
{
  "amount_in_cents": 12000000,
  "currency": "COP",
  "reference": "WALOS-1-1714567890",
  "customer_email": "cliente@correo.com",
  "payment_method": {
    "type": "NEQUI",
    "phone_number": "3001234567"
  },
  "redirect_url": "https://app.walos.co/pago-exitoso",
  "expiration_time": "2026-04-21T13:00:00.000Z"
}
```

### Métodos Wompi disponibles
| Wompi `type` | Descripción |
|-------------|-------------|
| `NEQUI` | QR / push Nequi |
| `PSE` | Débito bancario |
| `CARD` | Tarjeta crédito/débito |
| `BANCOLOMBIA_TRANSFER` | App Bancolombia |
| `BANCOLOMBIA_COLLECT` | Botón de cobro |

> **QR dinámico**: Wompi genera la URL del checkout — desde ahí el cliente escanea con Nequi/Daviplata.

### Mapeo de estados Wompi → Walos
| Wompi | Walos |
|-------|-------|
| `PENDING` | `pending` |
| `APPROVED` | `approved` |
| `DECLINED` | `rejected` |
| `VOIDED` | `voided` |
| `ERROR` | `error` |

---

## 6. Flujo Completo

```
Cajero → clic "Cobrar con QR"
        ↓
POST /api/v1/payments  { saleId, amount, method: "qr" }
        ↓
PaymentService
  → Genera reference = "WALOS-{companyId}-{timestamp}"
  → amount_in_cents = amount * 100
        ↓
IPaymentProvider → WompiProvider
  → POST https://sandbox.wompi.co/v1/transactions
  → Recibe { checkout_url, id }
        ↓
Guarda en payments.transactions
  status = 'pending', qr_code_url = checkout_url
        ↓
Responde al frontend con qr_code_url
        ↓
Frontend muestra QR grande + "Esperando pago..."
  → Polling GET /payments/{id} cada 3 segundos
        ↓
Cliente escanea QR con Nequi → paga
        ↓
Wompi → POST /api/v1/payments/webhook/wompi
  {
    "event": "transaction.updated",
    "data": { "transaction": { "id": "...", "status": "APPROVED", "reference": "WALOS-1-..." } }
  }
        ↓
WebhookHandler
  1. Valida firma HMAC-SHA256 con WOMPI_EVENTS_SECRET ← OBLIGATORIO
  2. Guarda en payments.webhook_events
  3. Busca transaction por reference
  4. Actualiza status → 'approved', confirmed_at = now()
  5. Cierra venta (sale) automáticamente
  6. Notificación in-app al cajero
        ↓
Polling del frontend detecta status = 'approved'
  → Animación ✅ "Pago recibido"
  → Mesa/venta cerrada
  → Opción: "¿Deseas emitir factura electrónica?"
```

---

## 7. Seguridad — Obligatorio

| Punto | Implementación |
|-------|---------------|
| **Validar firma webhook** | HMAC-SHA256 con `WOMPI_EVENTS_SECRET` antes de procesar |
| **Idempotencia** | Si llega el mismo webhook dos veces, no procesar doble |
| **No confiar en el frontend** | El monto siempre viene de la BD (sale), nunca del cliente |
| **HTTPS obligatorio** | El endpoint de webhook debe ser HTTPS en producción |
| **Reference única** | Validar que `reference` no se repita para evitar replay attacks |
| **Guardar todos los webhooks** | `payments.webhook_events` antes de procesar |
| **Timeout de QR** | QR expira en máx 15 minutos (`expires_at`) |

---

## 8. UX en POS

### Flujo de cobro
```
Botón "Cobrar" en mesa/venta
  ↓
Modal "Seleccionar método de pago"
  ├── 💵 Efectivo         → ingresa monto, calcula cambio
  ├── 💳 Tarjeta          → ingresa monto, registro manual
  ├── 📱 QR (Nequi)       → genera QR
  └── 🏦 PSE              → redirige al banco

Si QR seleccionado:
  ┌─────────────────────────────┐
  │    [QR GRANDE]              │
  │    $120,000                 │
  │    ⏳ Esperando pago...     │
  │    Expira en: 09:45         │
  │    [Cancelar]               │
  └─────────────────────────────┘

Al recibir webhook approved:
  ┌─────────────────────────────┐
  │    ✅ ¡Pago recibido!       │
  │    $120,000                 │
  │    Nequi — 3001234567       │
  │    [Cerrar mesa] [Facturar] │
  └─────────────────────────────┘
```

---

## 9. Integración con Agente IA

**Frases soportadas:**
- "Cobra esta mesa con Nequi"
- "Genera un QR para la venta 5"
- "Cobra $120,000 con QR"

**Acción que dispara la IA:**
```json
{
  "action": "create_payment",
  "saleId": 123,
  "amount": 120000,
  "method": "qr",
  "provider": "wompi"
}
```

---

## 10. Costos Wompi (producción)

| Método | Comisión |
|--------|---------|
| Nequi / QR | ~2.7% + IVA por transacción |
| PSE | Tarifa fija por transacción |
| Tarjeta | ~2.7% + IVA |
| **Mensualidad** | **$0** |

> Ideal para modelo SaaS — el comercio asume la comisión o la traslada al cliente.

---

## 11. Orden de Implementación

| # | Paso | Dependencia | Estimado |
|---|------|------------|---------|
| 1 | Migración BD (`013_payments_schema.sql`) | — | 1 sesión |
| 2 | Entidad + DTO + Interfaz `IPaymentProvider` | 1 | 1 sesión |
| 3 | `MockPaymentProvider` para desarrollo | 2 | 0.5 sesión |
| 4 | `WompiProvider` — sandbox + mapeo | 2 | 1 sesión |
| 5 | `PaymentService` + repositorio | 2-4 | 1 sesión |
| 6 | Controladores backend (POST + GET + webhook) | 5 | 1 sesión |
| 7 | `WebhookHandler` — validación firma + cierre venta | 6 | 1 sesión |
| 8 | Frontend POS — modal métodos + pantalla QR | 6 | 1 sesión |
| 9 | Frontend polling estado en tiempo real | 8 | 0.5 sesión |
| 10 | Integración acción IA `create_payment` | 5 | 0.5 sesión |
| 11 | Testing E2E en sandbox Wompi | Todo | 1 sesión |
| 12 | Go-live producción + monitoring | 11 | 0.5 sesión |

---

## 12. Variables de Entorno Nuevas

```env
# Wompi
WOMPI_PUBLIC_KEY=pub_stagtest_...
WOMPI_PRIVATE_KEY=prv_stagtest_...
WOMPI_EVENTS_SECRET=stagtest_events_...
WOMPI_BASE_URL=https://sandbox.wompi.co/v1
# Producción: https://production.wompi.co/v1

# App
APP_BASE_URL=https://app.walos.co   # para redirect_url post-pago
```

---

## 13. Proveedores Futuros

| Proveedor | País | Método |
|-----------|------|--------|
| **Wompi** | Colombia | QR, PSE, TC, Nequi, Bancolombia ← inicial |
| **PayU** | Latam | TC, PSE, efectivo |
| **MercadoPago** | Latam | QR, TC, efectivo |
| **Stripe** | Global | TC, Apple/Google Pay |

---

## 14. Coordinación con otros módulos

| Módulo | Relación |
|--------|---------|
| `pending-billing-ai-keys.md` | Wompi también procesa cobros B2B mensuales a comercios |
| `pending-electronic-invoicing.md` | Al confirmar pago → ofrecer opción de facturar electrónicamente |
| **Ventas/POS** | Cierre automático de venta/mesa al recibir `approved` |
| **Finanzas** | Registrar ingreso en `finance.entries` al confirmar pago |

---

> **Próximo paso cuando se apruebe:** Ejecutar Paso 1 — crear `013_payments_schema.sql`
