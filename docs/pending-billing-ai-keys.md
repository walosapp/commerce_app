# Pending: Módulo Billing B2B + API Keys de IA

> Estado: **PENDIENTE — No ejecutar hasta aprobación**  
> Creado: 2026-04-21  
> Aprobado por: pendiente

---

## Contexto y Decisiones Tomadas

### API Keys de IA
- Son **por comercio** (tenant), no por usuario
- Si el comercio tiene contrato con Walos → usa **global key** (env var de Walos)
- Si no → el comercio ingresa **su propia API key** (se guarda cifrada)
- El superadmin puede activar/desactivar el modo gestionado por comercio
- Proveedores soportados: `openai`, `gemini`, `anthropic`

### Panel de Comercios
- Gestión de servicios y precios: **solo superadmin**
- El comercio puede **ver** su plan y servicios contratados (solo lectura)
- El comercio puede gestionar su propia API key de IA y ver consumo de tokens

### Facturación
- **Fase 1**: Documento informativo (PDF) enviado por email y notificación in-app
- **Fase 2**: Integración con **Wompi** (Bancolombia) — soporta PSE, TC, Nequi, QR
  - Wompi elegido por ser nativo Colombia, API simple, soporta todos los métodos locales

### Tenants
- Ya existe `core.companies` como tabla raíz de tenant ✅
- `subscription_plan` (VARCHAR) ya existe — se mantiene pero se extiende con nuevas tablas

---

## Paso 1 — Migración de Base de Datos

### 1.1 Campos nuevos en `core.companies`
```sql
ALTER TABLE core.companies ADD COLUMN IF NOT EXISTS ai_key_managed     BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE core.companies ADD COLUMN IF NOT EXISTS ai_provider         VARCHAR(30) DEFAULT 'openai';
ALTER TABLE core.companies ADD COLUMN IF NOT EXISTS ai_api_key_enc      TEXT;          -- cifrada AES
ALTER TABLE core.companies ADD COLUMN IF NOT EXISTS ai_tokens_used      BIGINT NOT NULL DEFAULT 0;
ALTER TABLE core.companies ADD COLUMN IF NOT EXISTS ai_tokens_reset_at  TIMESTAMPTZ DEFAULT NOW();
ALTER TABLE core.companies ADD COLUMN IF NOT EXISTS ai_estimated_cost   DECIMAL(12,4) NOT NULL DEFAULT 0;
```

### 1.2 Schema `platform` + tablas nuevas

```sql
CREATE SCHEMA IF NOT EXISTS platform;

-- Catálogo maestro de servicios (solo Walos edita)
CREATE TABLE platform.service_catalog (
    id              BIGSERIAL PRIMARY KEY,
    code            VARCHAR(50) NOT NULL UNIQUE,
    name            VARCHAR(200) NOT NULL,
    description     TEXT,
    base_price      DECIMAL(12,2) NOT NULL DEFAULT 0,
    billing_unit    VARCHAR(20) NOT NULL DEFAULT 'month', -- month | year | usage
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    display_order   INT DEFAULT 0,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Servicios contratados por comercio
CREATE TABLE platform.company_subscriptions (
    id                  BIGSERIAL PRIMARY KEY,
    company_id          BIGINT NOT NULL REFERENCES core.companies(id),
    service_code        VARCHAR(50) NOT NULL REFERENCES platform.service_catalog(code),
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    custom_price        DECIMAL(12,2),           -- NULL = usa base_price del catálogo
    billing_frequency   VARCHAR(20) NOT NULL DEFAULT 'monthly', -- monthly | annual
    next_billing_date   DATE,
    started_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    cancelled_at        TIMESTAMPTZ,
    notes               TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (company_id, service_code)
);

-- Facturas generadas por comercio
CREATE TABLE platform.billing_invoices (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    invoice_number  VARCHAR(50) NOT NULL UNIQUE,
    period_start    DATE NOT NULL,
    period_end      DATE NOT NULL,
    subtotal        DECIMAL(12,2) NOT NULL DEFAULT 0,
    tax_rate        DECIMAL(5,2) NOT NULL DEFAULT 19,   -- IVA 19% Colombia
    tax_amount      DECIMAL(12,2) NOT NULL DEFAULT 0,
    total           DECIMAL(12,2) NOT NULL DEFAULT 0,
    status          VARCHAR(20) NOT NULL DEFAULT 'draft', -- draft | sent | paid | overdue | cancelled
    sent_at         TIMESTAMPTZ,
    paid_at         TIMESTAMPTZ,
    due_date        DATE,
    payment_method  VARCHAR(30),   -- card | pse | manual
    payment_ref     VARCHAR(200),  -- referencia Wompi
    notes           TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Líneas de cada factura
CREATE TABLE platform.billing_invoice_items (
    id              BIGSERIAL PRIMARY KEY,
    invoice_id      BIGINT NOT NULL REFERENCES platform.billing_invoices(id) ON DELETE CASCADE,
    service_code    VARCHAR(50),
    description     VARCHAR(500) NOT NULL,
    quantity        DECIMAL(10,2) NOT NULL DEFAULT 1,
    unit_price      DECIMAL(12,2) NOT NULL,
    subtotal        DECIMAL(12,2) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Métodos de pago registrados por comercio
CREATE TABLE platform.payment_methods (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    type            VARCHAR(20) NOT NULL,  -- card | pse
    provider        VARCHAR(30) DEFAULT 'wompi',
    provider_token  TEXT,                  -- token de Wompi (no guardar número completo)
    last4           VARCHAR(4),            -- últimos 4 dígitos TC
    bank_name       VARCHAR(100),          -- para PSE
    holder_name     VARCHAR(200),
    is_default      BOOLEAN NOT NULL DEFAULT FALSE,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Índices
CREATE INDEX ON platform.company_subscriptions (company_id);
CREATE INDEX ON platform.billing_invoices (company_id, status);
CREATE INDEX ON platform.billing_invoices (due_date) WHERE status IN ('sent', 'overdue');
CREATE INDEX ON platform.payment_methods (company_id);
```

### 1.3 Seed catálogo de servicios
```sql
INSERT INTO platform.service_catalog (code, name, description, base_price, billing_unit, display_order) VALUES
('platform_base',    'Plataforma Base',          'Acceso a inventario, ventas, finanzas, reportes', 0,      'month', 1),
('ai_agents',        'Agentes IA',               'Asistente inteligente con IA para gestión y análisis', 0, 'month', 2),
('e_invoicing',      'Facturación Electrónica',  'Emisión de facturas electrónicas DIAN',            0,      'month', 3),
('delivery_module',  'Módulo Domicilios',        'Gestión de pedidos y domicilios',                  0,      'month', 4),
('multi_branch',     'Multi-Sucursal',           'Gestión de múltiples sucursales',                  0,      'month', 5)
ON CONFLICT (code) DO NOTHING;
```

---

## Paso 2 — Backend (.NET)

### 2.1 Nuevas entidades de dominio
- `platform/ServiceCatalog.cs`
- `platform/CompanySubscription.cs`
- `platform/BillingInvoice.cs` + `BillingInvoiceItem.cs`
- `platform/PaymentMethod.cs`
- Agregar campos AI a entidad `Company.cs`

### 2.2 DTOs
- `GetCompanyPlanResponse` — servicios activos + próxima factura + uso AI
- `UpdateAiKeyRequest` — provider + api_key (en plain text, se cifra en backend)
- `AssignServiceRequest` — superadmin asigna servicio a comercio
- `GenerateInvoiceRequest` — genera factura para un período
- `RegisterPaymentMethodRequest` — guarda TC/PSE via Wompi

### 2.3 Repositorios e interfaces
- `IPlatformRepository` con métodos:
  - `GetServiceCatalogAsync()`
  - `GetCompanySubscriptionsAsync(companyId)`
  - `UpsertSubscriptionAsync(...)`
  - `GetInvoicesAsync(companyId)`
  - `CreateInvoiceAsync(...)`
  - `UpdateInvoiceStatusAsync(...)`
  - `GetPaymentMethodsAsync(companyId)`
  - `UpdateAiKeyAsync(companyId, ...)`
  - `IncrementAiTokensAsync(companyId, tokens, cost)`

### 2.4 Controladores
- `PlatformAdminController` (solo superadmin):
  - `GET /api/v1/platform/companies` — lista comercios con estado de suscripción
  - `GET /api/v1/platform/companies/{id}/plan`
  - `POST /api/v1/platform/companies/{id}/services` — asignar servicio
  - `PATCH /api/v1/platform/companies/{id}/services/{code}` — editar precio/estado
  - `POST /api/v1/platform/companies/{id}/invoices` — generar factura
  - `PATCH /api/v1/platform/invoices/{id}/status` — marcar pagada/vencida
- `PlatformController` (comercio autenticado):
  - `GET /api/v1/platform/my-plan` — ver plan propio
  - `GET /api/v1/platform/my-invoices` — historial facturas
  - `GET /api/v1/platform/ai-usage` — consumo tokens del mes
  - `PUT /api/v1/platform/ai-key` — guardar/actualizar propia API key
  - `GET /api/v1/platform/payment-methods`
  - `POST /api/v1/platform/payment-methods`
  - `DELETE /api/v1/platform/payment-methods/{id}`

### 2.5 Servicio de facturación (job mensual)
- `BillingJobService` — cron que:
  1. Consulta suscripciones activas con `next_billing_date <= today`
  2. Genera `billing_invoice` con sus líneas
  3. Actualiza `next_billing_date`
  4. Envía email + notificación in-app al comercio
  5. Si tiene método de pago por defecto → intenta cobro automático via Wompi

### 2.6 Cifrado de API keys
- Usar `AES-256-GCM` para cifrar `ai_api_key` antes de guardar en BD
- La clave de cifrado va en variable de entorno `AI_KEY_ENCRYPTION_SECRET`
- Nunca exponer la key completa en respuestas API (solo retornar si está configurada: `true/false`)

---

## Paso 3 — Integración Wompi

### Credenciales necesarias
- `WOMPI_PUBLIC_KEY` — en frontend (para widget)
- `WOMPI_PRIVATE_KEY` — solo en backend
- `WOMPI_EVENTS_SECRET` — para validar webhooks

### Flujo TC
1. Frontend usa **Widget Wompi** para tokenizar la tarjeta → obtiene `payment_source_id`
2. Backend guarda el token en `platform.payment_methods`
3. En cobro automático: backend hace `POST /transactions` a Wompi con el token

### Flujo PSE
1. Frontend solicita lista de bancos PSE a backend
2. Usuario selecciona banco → backend crea transacción PSE en Wompi → devuelve URL de redirección
3. Usuario completa en banco → Wompi hace webhook al backend → backend actualiza estado factura

### Webhook endpoint
- `POST /api/v1/platform/webhooks/wompi` — valida firma + actualiza estado de pago

---

## Paso 4 — Frontend

### 4.1 Panel Superadmin — Gestión de Comercios
**Ruta:** `/admin/companies` (protegida por rol superadmin global)
- Tabla de comercios con: nombre, plan activo, servicios activos, próxima factura, estado
- Al abrir un comercio:
  - Tab **Servicios**: toggle activo/inactivo por servicio + precio personalizado + frecuencia
  - Tab **Facturas**: historial + botón "Generar factura" + cambiar estado
  - Tab **Configuración IA**: ver si usa key propia o gestionada, toggle
  - Tab **Info**: datos del comercio (readonly)

### 4.2 Vista Comercio — Mi Plan
**Ruta:** `/settings/plan`
- Cards de servicios contratados (solo lectura)
- Próxima fecha de facturación
- Historial de facturas con botón de descarga PDF

### 4.3 Vista Comercio — Configuración IA
**Ruta:** `/settings/ai`
- Si `ai_key_managed = true`: mensaje "Tu API key está gestionada por Walos"
- Si `ai_key_managed = false`: formulario para ingresar key propia + selector de proveedor
- Gráfico de consumo de tokens del mes actual vs mes anterior
- Costo estimado acumulado

### 4.4 Vista Comercio — Métodos de Pago
**Ruta:** `/settings/payments`
- Lista de TC/PSE registrados
- Botón "Agregar tarjeta" → abre Widget Wompi
- Botón "Agregar PSE" → flujo PSE
- Marcar método por defecto

---

## Paso 5 — Emails y Notificaciones

### Templates de email necesarios
- `invoice-generated.html` — factura mensual con detalle de servicios + total + botón pagar
- `payment-confirmed.html` — confirmación de pago recibido
- `payment-failed.html` — fallo en cobro automático + link para pagar manualmente
- `subscription-expiring.html` — aviso 7 días antes de vencimiento

### Notificaciones in-app (ya existe el sistema)
- `Nueva factura disponible` → link a `/settings/plan`
- `Pago confirmado`
- `Pago fallido — acción requerida`

---

## Orden de Implementación

| # | Paso | Dependencias | Estimado |
|---|------|-------------|---------|
| 1 | Migración BD (`012_platform_billing.sql`) | — | 1 sesión |
| 2 | Entidades + Repositorio backend | Paso 1 | 1 sesión |
| 3 | Controladores + DTOs backend | Paso 2 | 1 sesión |
| 4 | Panel superadmin frontend | Paso 3 | 2 sesiones |
| 5 | Vistas comercio (plan + AI + pagos) | Paso 3 | 2 sesiones |
| 6 | Job de facturación mensual | Paso 2 | 1 sesión |
| 7 | Integración Wompi (TC + PSE) | Paso 5 | 2 sesiones |
| 8 | Emails (templates + envío) | Paso 6 | 1 sesión |
| 9 | Webhooks Wompi | Paso 7 | 1 sesión |
| 10 | Testing E2E del flujo completo | Todo | 1 sesión |

---

## Variables de Entorno Nuevas

```env
# Cifrado API keys de IA
AI_KEY_ENCRYPTION_SECRET=<32-bytes-hex>

# Wompi
WOMPI_PUBLIC_KEY=pub_stagtest_...
WOMPI_PRIVATE_KEY=prv_stagtest_...
WOMPI_EVENTS_SECRET=stagtest_events_...
WOMPI_BASE_URL=https://sandbox.wompi.co/v1   # producción: https://production.wompi.co/v1

# Email (ya debería existir)
SMTP_HOST=...
SMTP_USER=...
SMTP_PASS=...
BILLING_FROM_EMAIL=facturacion@walos.app
```

---

> **Próximo paso cuando se apruebe:** Ejecutar Paso 1 — crear `012_platform_billing.sql`
