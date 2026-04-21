# Pending: Módulo de Crédito en Mesas

> Estado: **PENDIENTE — No ejecutar hasta aprobación**  
> Creado: 2026-04-21

---

## Concepto

Al facturar una mesa, el cajero puede indicar que el cliente **paga parcialmente**. El saldo restante queda registrado como **crédito a nombre del cliente** (que puede ser el nombre que se le puso a la mesa).

---

## Flujo de usuario

```
Mesa "Pedro conocido de Juan" — Total: $85,000
        ↓
Cajero abre panel de facturación
        ↓
Checkbox: "¿Pago con crédito?"
        ↓ (activado)
Campo: "Monto que paga ahora": $50,000
Saldo crédito calculado automáticamente: $35,000
        ↓
Al facturar:
  - Cierra la mesa normalmente
  - Registra crédito: $35,000 a nombre "Pedro conocido de Juan"
  - Asociado a fecha, orden y productos
```

---

## Modelo de Datos

### Tabla: `sales.credits`
```sql
CREATE TABLE sales.credits (
    id              BIGSERIAL PRIMARY KEY,
    company_id      BIGINT NOT NULL REFERENCES core.companies(id),
    branch_id       BIGINT REFERENCES core.branches(id),
    order_id        BIGINT REFERENCES sales.orders(id),

    customer_name   VARCHAR(200) NOT NULL,  -- nombre que tenía la mesa o ingresado
    order_number    VARCHAR(50),

    original_total  DECIMAL(18,2) NOT NULL,
    amount_paid     DECIMAL(18,2) NOT NULL DEFAULT 0,
    credit_amount   DECIMAL(18,2) NOT NULL,  -- = original_total - amount_paid

    status          VARCHAR(20) NOT NULL DEFAULT 'pending',
    -- pending | partial | paid | cancelled

    notes           TEXT,
    paid_at         TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by      BIGINT
);

CREATE INDEX ON sales.credits (company_id, status);
CREATE INDEX ON sales.credits (company_id, customer_name);
```

### Tabla: `sales.credit_payments`
```sql
-- Pagos parciales que van abonando al crédito
CREATE TABLE sales.credit_payments (
    id          BIGSERIAL PRIMARY KEY,
    company_id  BIGINT NOT NULL REFERENCES core.companies(id),
    credit_id   BIGINT NOT NULL REFERENCES sales.credits(id),
    amount      DECIMAL(18,2) NOT NULL,
    notes       TEXT,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by  BIGINT
);
```

---

## Cambios en el flujo de facturación

### InvoicePanel (frontend)
- Agregar checkbox "Crédito / Pago parcial"
- Si activo: campo "Monto a pagar ahora" con validación (0 < monto <= total)
- Mostrar: saldo que queda como crédito
- Al confirmar: enviar `{ creditCustomerName, amountPaid }` junto con la factura

### Backend — `InvoiceTableRequest`
```csharp
public class InvoiceTableRequest
{
    // ... campos actuales ...
    public bool HasCredit { get; set; } = false;
    public decimal AmountPaid { get; set; } = 0;
    public string? CreditCustomerName { get; set; }
}
```

### Backend — `InvoiceTableAsync`
- Si `HasCredit = true` y `AmountPaid < finalTotal`:
  - Crear registro en `sales.credits`
  - El `FinalTotalPaid` de la orden = `AmountPaid`
  - El stock se descuenta igual (el crédito es solo financiero)

---

## Módulo de Gestión de Créditos

### Vista: `/sales/credits` (o pestaña en ventas)
- Tabla con: cliente, monto original, pagado, saldo, fecha, estado
- Filtros: estado (pendiente, parcial, pagado), búsqueda por nombre
- Al abrir un crédito:
  - Ver detalle de la orden (productos que pidió)
  - Botón "Registrar abono" → ingresa monto → descuenta del saldo
  - Si saldo = 0 → estado = `paid`
- Totales: suma de créditos pendientes del día / semana / mes

### Endpoints necesarios
```
GET    /api/v1/sales/credits              ← lista con filtros
GET    /api/v1/sales/credits/{id}         ← detalle
POST   /api/v1/sales/credits/{id}/pay     ← registrar abono
DELETE /api/v1/sales/credits/{id}         ← cancelar crédito
```

---

## UX en InvoicePanel

```
┌─────────────────────────────────────────┐
│ Total de la cuenta:    $85,000          │
│                                         │
│ ☑ Pago con crédito                     │
│                                         │
│ Nombre del cliente: [Pedro Pérez      ] │
│ Paga ahora:         [$ 50,000         ] │
│ Queda como crédito: $35,000  ← auto    │
│                                         │
│ [Cancelar]          [Facturar y Cerrar] │
└─────────────────────────────────────────┘
```

---

## Orden de implementación

| # | Paso | Estimado |
|---|------|---------|
| 1 | Migración BD (`sales.credits` + `sales.credit_payments`) | 0.5 sesión |
| 2 | Backend: ajuste a `InvoiceTableRequest` + lógica crédito en `InvoiceTableAsync` | 1 sesión |
| 3 | Backend: endpoints CRUD créditos + abonos | 1 sesión |
| 4 | Frontend: checkbox + campos en `InvoicePanel` | 0.5 sesión |
| 5 | Frontend: vista gestión de créditos | 1 sesión |

---

> **Relación con nombre de mesa:** El campo `customer_name` del crédito se pre-llena con el nombre que tenga la mesa (ya implementado). Si la mesa se llama "Pedro conocido de Juan", ese es el nombre que aparece en el crédito.
