# Design: Módulo POS-Deli

> **Change ID**: pos-deli
> **Status**: draft
> **Version**: 1.0

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│                         FRONTEND (React)                              │
│                                                                      │
│  modules/pos-deli/                                                   │
│  ├── PosDeliPage.jsx          ← Vista principal (fullscreen)         │
│  ├── hooks/                                                          │
│  │   ├── useScale.js          ← Web Serial API + parser BBG         │
│  │   └── useBarcodeScanner.js ← Detección de input rápido           │
│  ├── components/                                                     │
│  │   ├── ProductGrid.jsx      ← Grid visual de productos            │
│  │   ├── ScaleIndicator.jsx   ← Display de peso + estado            │
│  │   ├── TicketPanel.jsx      ← Factura activa                      │
│  │   ├── PaymentModal.jsx     ← Modal de cobro                      │
│  │   ├── WeightInputModal.jsx ← Fallback peso manual                │
│  │   └── ProductSearchBar.jsx ← Búsqueda con autofocus              │
│  └── stores/                                                         │
│      └── posDeliStore.js      ← Estado del ticket (Zustand)          │
│                                                                      │
├──────────────────────────────────────────────────────────────────────┤
│                         BACKEND (ASP.NET Core)                        │
│                                                                      │
│  Controllers/PosDeliController.cs                                    │
│  ├── POST /api/v1/pos-deli/sale       ← Registrar venta             │
│  ├── GET  /api/v1/pos-deli/products   ← Productos para el POS       │
│  └── GET  /api/v1/pos-deli/favorites  ← Productos favoritos/frecuentes│
│                                                                      │
│  Reutiliza:                                                          │
│  ├── IInventoryRepository (buscar productos, descontar stock)        │
│  ├── ISalesRepository (registrar orden)                              │
│  └── IOrderPaymentRepository (registrar pago)                        │
│                                                                      │
├──────────────────────────────────────────────────────────────────────┤
│                         DATABASE (PostgreSQL)                         │
│                                                                      │
│  Reutiliza tablas existentes:                                        │
│  ├── inventory.products (catálogo)                                   │
│  ├── inventory.stock (stock por sucursal)                            │
│  ├── inventory.movements (trazabilidad)                              │
│  ├── sales.orders (registro de venta)                                │
│  ├── sales.order_items (items de la venta)                           │
│  └── sales.order_payments (pagos)                                    │
│                                                                      │
│  Nuevo (si se requiere):                                             │
│  └── sales.pos_deli_config (favoritos, config de báscula por branch) │
└──────────────────────────────────────────────────────────────────────┘
```

---

## Component Design

### Frontend

#### 1. `PosDeliPage.jsx` — Vista principal

Layout de dos paneles (responsive pero optimizado para desktop/tablet):

```
┌─────────────────────────────────────────────────────────────────┐
│ Header: [Nombre Sucursal] [⚖️ Status báscula] [Hora] [Usuario]  │
├─────────────────────────────────┬───────────────────────────────┤
│  LEFT PANEL (60%)               │  RIGHT PANEL (40%)            │
│                                 │                               │
│  ┌─────────────────────────┐   │  ┌─────────────────────────┐ │
│  │ 🔍 Buscar producto...   │   │  │  TICKET #0042            │ │
│  └─────────────────────────┘   │  │                          │ │
│                                 │  │  Jamón     0.35kg $9,975 │ │
│  ┌─────────────────────────┐   │  │  Coca-Cola  x1   $3,200 │ │
│  │ ⚖️ 0.350 kg (estable)   │   │  │  Queso     0.50kg $9,000│ │
│  └─────────────────────────┘   │  │                          │ │
│                                 │  │  ─────────────────────── │ │
│  PESABLES:                      │  │  Items: 3               │ │
│  ┌─────┐ ┌─────┐ ┌─────┐      │  │  TOTAL: $22,175         │ │
│  │Jamón│ │Queso│ │Salch│      │  │                          │ │
│  │$28.5│ │ $18k│ │$9.8k│      │  │  ┌─────────────────────┐│ │
│  └─────┘ └─────┘ └─────┘      │  │  │   💰 COBRAR (F12)   ││ │
│                                 │  │  └─────────────────────┘│ │
│  UNIDAD:                        │  └─────────────────────────┘ │
│  ┌─────┐ ┌─────┐ ┌─────┐      │                               │
│  │Coca │ │ Pan │ │Leche│      │                               │
│  │$3.2k│ │ $2k │ │$4.5k│      │                               │
│  └─────┘ └─────┘ └─────┘      │                               │
└─────────────────────────────────┴───────────────────────────────┘
```

**Comportamiento clave**:
- El campo de búsqueda SIEMPRE tiene focus (captura barcode automáticamente).
- El grid muestra dos secciones: "Pesables" y "Por unidad".
- Click en producto pesable → lee peso de báscula → agrega al ticket.
- Click en producto unidad → agrega qty=1 al ticket.
- F12 → abre PaymentModal.

#### 2. `useScale.js` — Hook de Web Serial API

```javascript
// Interfaz pública del hook
const {
  weight,          // number: peso actual en kg (ej: 0.350)
  isStable,        // boolean: si el peso es estable
  isConnected,     // boolean: si la báscula está conectada
  error,           // string | null: mensaje de error
  connect,         // () => Promise<void>: solicitar conexión
  disconnect,      // () => void: cerrar conexión
} = useScale({
  baudRate: 9600,
  parser: 'bbg-shop-pole',  // parser intercambiable
});
```

**Flujo interno**:
1. `connect()` → llama `navigator.serial.requestPort()` → abre puerto con config.
2. Inicia `ReadableStream` reader en loop.
3. Cada chunk de bytes se pasa al parser.
4. Parser extrae peso + flag estable/inestable.
5. Actualiza state con throttle (max 5 updates/segundo).

**Parser BBG SHOP POLE** (ajustable con datos reales):
```
Input crudo:  "ST,+  0.350 kg\r\n"
Parsed:       { weight: 0.350, unit: 'kg', stable: true }
```

#### 3. `useBarcodeScanner.js` — Hook de detección de barcode

```javascript
// Interfaz pública
const { lastBarcode } = useBarcodeScanner({
  onScan: (barcode) => { /* callback cuando se detecta scan */ },
  minLength: 6,       // mínimo caracteres para considerar barcode
  maxDelay: 50,       // ms máximo entre caracteres (humano > 100ms, scanner < 30ms)
});
```

**Lógica**: Escucha `keydown` global. Si detecta N caracteres en < 50ms + Enter → es barcode. Si el tiempo entre teclas es > 100ms → es humano tecleando.

#### 4. `posDeliStore.js` — Estado del ticket (Zustand)

```javascript
{
  items: [
    { id: 'uuid', productId: 1, name: 'Jamón', quantity: 0.350, unit: 'kg',
      unitPrice: 28500, subtotal: 9975, isWeighed: true },
    { id: 'uuid', productId: 5, name: 'Coca-Cola', quantity: 1, unit: 'und',
      unitPrice: 3200, subtotal: 3200, isWeighed: false },
  ],
  total: 13175,
  ticketNumber: 42,

  // Actions
  addWeighedItem: (product, weight) => ...,
  addUnitItem: (product) => ...,
  removeItem: (itemId) => ...,
  updateQuantity: (itemId, newQty) => ...,
  clearTicket: () => ...,
}
```

**Persistencia local**: `zustand/middleware` con `persist` en `sessionStorage` para sobrevivir refresh accidental.

#### 5. `PaymentModal.jsx` — Modal de cobro

Reutiliza la lógica de pagos del módulo de ventas. Mismos métodos de pago, misma UI de selección. Diferencias:
- No hay propinas.
- No hay split de cuenta.
- Más prominente el campo de "monto recibido" para efectivo.

---

### Backend

#### `PosDeliController.cs`

```csharp
[ApiController]
[Route("api/v1/pos-deli")]
[Authorize]
public class PosDeliController : ControllerBase
{
    // POST /api/v1/pos-deli/sale
    // Body: { items: [...], payments: [...] }
    // Response: { saleId, ticketNumber, change }

    // GET /api/v1/pos-deli/products?search=&category=
    // Reutiliza IInventoryRepository.GetAllProductsAsync con filtros

    // GET /api/v1/pos-deli/favorites
    // Productos marcados como favoritos para POS (campo is_pos_favorite o tabla config)
}
```

#### DTO de venta

```csharp
public record PosDeliSaleRequest
{
    public List<PosDeliSaleItem> Items { get; init; }
    public List<PosDeliPayment> Payments { get; init; }
}

public record PosDeliSaleItem
{
    public long ProductId { get; init; }
    public decimal Quantity { get; init; }      // kg para pesados, unidades para otros
    public decimal UnitPrice { get; init; }     // precio/kg o precio/unidad
    public bool IsWeighed { get; init; }
}

public record PosDeliPayment
{
    public string Method { get; init; }         // "cash", "nequi", "card"
    public decimal Amount { get; init; }
}
```

#### Flujo de `POST /sale`

```
1. Validar request (items no vacíos, quantities > 0, precios > 0)
2. Verificar stock disponible para cada item
3. Crear orden en sales.orders (type = 'pos-deli')
4. Crear items en sales.order_items
5. Registrar pagos en sales.order_payments
6. Descontar stock por item (inventory.stock)
7. Registrar movimientos (inventory.movements, type = 'sale')
8. Retornar { saleId, ticketNumber, total, change }
```

Todo dentro de una transacción DB.

---

## Sequence Diagrams

### Flujo: Producto pesado

```
Cajero          Browser/React      useScale       Backend         DB
  |                  |                |              |             |
  | [coloca item     |                |              |             |
  |  en báscula]     |                |              |             |
  |                  |← peso: 0.350 ──|              |             |
  |                  |  (streaming)   |              |             |
  |                  |                |              |             |
  | click "Jamón"    |                |              |             |
  |─────────────────>|                |              |             |
  |                  | lee weight=0.350              |             |
  |                  | calcula: 0.350×28500=9975     |             |
  |                  | addWeighedItem(jamón, 0.350)  |             |
  |                  |                |              |             |
  |<── ticket updated|                |              |             |
  |    (Jamón $9,975)|                |              |             |
```

### Flujo: Barcode scan

```
Cajero         Scanner(HID)     Browser/React     Backend          DB
  |                |                 |               |              |
  | [escanea]      |                 |               |              |
  |───────────────>| keydown events  |               |              |
  |                | (< 50ms/char)   |               |              |
  |                |────────────────>|               |              |
  |                |                 | detectBarcode |              |
  |                |                 | "7702004003218"              |
  |                |                 |── GET /products?barcode= ──>|
  |                |                 |<── product: Coca-Cola ──────|
  |                |                 | addUnitItem(coca)           |
  |                |                 |               |              |
  |<── ticket updated                |               |              |
  |    (Coca $3,200)                 |               |              |
```

### Flujo: Cobro completo

```
Cajero          Browser/React          Backend              DB
  |                  |                    |                   |
  | F12 (cobrar)     |                    |                   |
  |─────────────────>|                    |                   |
  |                  | abre PaymentModal  |                   |
  |                  |                    |                   |
  | "Efectivo $25k"  |                    |                   |
  |─────────────────>|                    |                   |
  |                  | muestra cambio     |                   |
  |                  | $2,825             |                   |
  |                  |                    |                   |
  | confirma         |                    |                   |
  |─────────────────>|                    |                   |
  |                  |── POST /pos-deli/sale ──────────────>|
  |                  |   { items, payments }                 |
  |                  |                    |── BEGIN TX ──────>|
  |                  |                    |   insert order    |
  |                  |                    |   insert items    |
  |                  |                    |   insert payment  |
  |                  |                    |   update stock ×N |
  |                  |                    |   insert movements|
  |                  |                    |── COMMIT ────────>|
  |                  |<── { saleId, change: 2825 } ─────────|
  |                  |                    |                   |
  |                  | clearTicket()      |                   |
  |                  | printTicket()      |                   |
  |<── "Venta #42 OK"|                    |                   |
```

---

## Architecture Decisions

### ADR-01: Módulo nuevo vs extender Sales

**Decision**: Crear módulo `pos-deli/` independiente.

**Context**: El módulo de ventas actual tiene concepto de mesas, meseros, estados de pedido, split de cuenta, propinas. Nada de eso aplica a una salsamentaría.

**Rationale**: Agregar condicionales `if (isSalsamentaria)` por todo el módulo de Sales crea acoplamiento y complejidad. Un módulo nuevo es más limpio, testeable, y eliminable sin riesgo.

**Consequence**: Algo de duplicación en el flujo de pago. Se mitiga reutilizando DTOs y la tabla `order_payments` del backend.

### ADR-02: Web Serial API vs Bridge local

**Decision**: Web Serial API directa en el browser.

**Context**: Necesitamos leer datos de una báscula RS-232 desde una aplicación web.

**Rationale**:
- No requiere instalar software extra.
- Funciona nativamente en Chrome/Edge (>95% de uso en POS).
- El permiso se concede una vez y persiste.
- La latencia es suficiente (<50ms) para lectura de peso.

**Consequence**: Solo funciona en Chrome/Edge. Si el cliente usa Firefox, necesitaría el bridge (fase futura).

### ADR-03: Precios desde inventario, no desde PLU de la báscula

**Decision**: Los precios siempre vienen de nuestra base de datos.

**Context**: La BBG SHOP POLE tiene 72 memorias PLU con precios configurables.

**Rationale**:
- Single source of truth para precios.
- No hay que sincronizar PLUs cada vez que cambia un precio.
- El admin cambia precios en Walos y aplica inmediatamente.

**Consequence**: La báscula solo se usa como sensor de peso. Su display de "total" no coincide con nuestro cálculo (aceptable — el cajero mira la pantalla del PC).

### ADR-04: Reutilizar tablas de ventas existentes

**Decision**: Guardar ventas POS-Deli en `sales.orders` con `order_type = 'pos-deli'`.

**Context**: Podríamos crear tablas nuevas o reutilizar las existentes.

**Rationale**:
- Los reportes de ventas globales incluyen automáticamente POS-Deli.
- Reutiliza la infraestructura de pagos y movimientos.
- Menos migraciones y menos mantenimiento.

**Consequence**: La tabla `orders` necesita el campo `order_type` si no lo tiene (verificar). Los queries de Sales que no filtran por tipo podrían incluir ventas POS-Deli (revisar).

---

## Non-Functional Requirements

| Aspecto | Requisito |
|---|---|
| **Latencia barcode** | < 500ms desde escaneo hasta item en ticket |
| **Latencia peso** | < 200ms desde peso estable hasta display actualizado |
| **Disponibilidad** | Funcionar sin báscula (modo degradado: peso manual) |
| **Browser** | Chrome 89+ o Edge 89+ (Web Serial API) |
| **Responsive** | Optimizado para 1024px+ (POS suele ser monitor dedicado) |
| **Persistencia** | Ticket sobrevive refresh (sessionStorage) |
| **Seguridad** | Misma auth JWT que el resto de la app |
| **Multi-tenant** | Aislado por companyId/branchId como todo Walos |
