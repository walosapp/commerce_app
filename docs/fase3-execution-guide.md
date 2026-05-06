# Fase 3 — Guía de Ejecución para Agente

> **Estado actual del proyecto**: Fases 1 y 2 completadas.  
> **Objetivo de esta fase**: P4 (Impresión de Tickets) + P6 (Historial de Ventas Extendido)  
> **Fecha**: Mayo 2026

---

## Estado Completado (NO modificar, solo referencia)

| Fase | Módulo | Estado | Commits |
|------|--------|--------|---------|
| 1 | P1: Control de Caja (backend + frontend) | ✅ | CashRegisterBar, modals, SalesPage integration |
| 1 | P2: Métodos de Pago POS (backend + frontend) | ✅ | PaymentMethodsSection en InvoicePanel |
| 2 | P3: Devoluciones / Anulaciones (backend + frontend) | ✅ | RefundController, RefundService, RefundModal |
| 2 | P5: Propinas (backend + frontend) | ✅ | Tip section en InvoicePanel, validación backend |

---

## Arquitectura del Proyecto

```
backend-dotnet/src/
├── Walos.API/                    ← Controllers, Middleware, Program.cs
│   └── Controllers/              ← [Authorize] + ITenantContext pattern
├── Walos.Application/            ← Services (interfaces + impl), DTOs
│   ├── DTOs/Sales/               ← Records para request/response
│   └── Services/                 ← Lógica de negocio
├── Walos.Domain/                 ← Entities, Interfaces (repos), Exceptions
│   ├── Entities/                 ← POCOs
│   ├── Interfaces/               ← Repository interfaces
│   └── Exceptions/               ← ValidationException, NotFoundException, BusinessException
└── Walos.Infrastructure/         ← Repositories (Dapper), DependencyInjection.cs
    ├── Repositories/             ← SQL explícito con Dapper (NO Entity Framework)
    └── DependencyInjection.cs    ← Registrar TODOS los repos y services aquí

frontend/src/
├── config/api.js                 ← Axios instance (baseURL: /api/v1)
├── modules/sales/
│   ├── SalesPage.jsx             ← Página principal con tabs: tables, credits, sales, cash
│   └── components/               ← Componentes del módulo
├── services/                     ← API wrappers (1 archivo por módulo)
├── stores/authStore.js           ← Zustand: tenantId, branchId, userId, role
└── utils/formatCurrency.js       ← formatCurrency(value) → "$1.234"
```

### Patrones Obligatorios

1. **Backend**: Dapper + SQL explícito. NO Entity Framework.
2. **Controller pattern**: `[Authorize]` + `ITenantContext` para `CompanyId`, `BranchId`, `UserId`.
3. **Response pattern**: `ApiResponse<T>.Ok(data, message, count)` o `ApiResponse.Fail(message)`.
4. **DTOs**: `record` types en `Walos.Application/DTOs/Sales/`.
5. **SQL columns**: `snake_case` → mapped con `AS PascalCase` en queries.
6. **Frontend**: React Query (`useQuery`/`useMutation`), Lucide icons, TailwindCSS, `react-hot-toast`.
7. **Invalidar queries** después de mutaciones: `queryClient.invalidateQueries(...)`.
8. **Registrar** en `DependencyInjection.cs` todo repo e interfaz de servicio nueva.

---

## P4 — IMPRESIÓN DE TICKETS / RECIBOS

### Contexto

Actualmente `InvoicePanel.jsx` tiene un `handlePrint()` básico que genera HTML inline con `window.open()` y `window.print()`. Esto es funcional pero primitivo. Se necesita:

1. **Endpoints backend** que retornen datos estructurados del recibo
2. **Componentes React** reutilizables para renderizar tickets
3. **Formatos**: Recibo de venta, Comanda de cocina, Reporte Z, Pre-cuenta

### Paso 1: DTOs Backend

Crear `backend-dotnet/src/Walos.Application/DTOs/Sales/ReceiptDtos.cs`:

```csharp
namespace Walos.Application.DTOs.Sales;

// Recibo de venta — entregado al cliente
public record ReceiptData(
    // Empresa
    string CompanyName,
    string? CompanyLegalName,
    string? CompanyNit,          // Traer de core.companies: nit / tax_id si existe
    string? CompanyAddress,      // Si existe en la tabla
    string? CompanyPhone,
    string? CompanyLogoUrl,
    
    // Orden
    long OrderId,
    string OrderNumber,
    string TableName,
    int TableNumber,
    DateTime CreatedAt,
    string CashierName,          // JOIN core.users ON orders.created_by
    
    // Items
    List<ReceiptItemDto> Items,
    
    // Totales
    decimal Subtotal,
    string? DiscountType,        // percentage | fixed | null
    decimal DiscountValue,
    decimal DiscountAmount,
    decimal FinalTotalPaid,
    decimal TipAmount,
    bool TipIncluded,
    int SplitCount,
    
    // Pagos
    List<ReceiptPaymentDto> Payments,
    
    // Crédito (si aplica)
    bool HasCredit,
    decimal? CreditAmount,
    string? CreditCustomerName
);

public record ReceiptItemDto(
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal Subtotal
);

public record ReceiptPaymentDto(
    string Method,       // cash, card, transfer, nequi
    decimal Amount,
    string? Reference
);

// Comanda de cocina — SIN precios
public record KitchenTicketData(
    string TableName,
    int TableNumber,
    DateTime CreatedAt,
    string CashierName,
    List<KitchenItemDto> Items
);

public record KitchenItemDto(
    string ProductName,
    decimal Quantity,
    string? Notes
);

// Reporte Z — cierre de caja
public record ZReportData(
    // Caja
    long CashRegisterId,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    string OpenedByName,
    string? ClosedByName,
    decimal OpeningAmount,
    decimal? ClosingAmount,
    decimal? ExpectedCash,
    decimal? Difference,
    
    // Totales
    decimal TotalSales,
    int OrderCount,
    decimal TotalCashSales,
    decimal TotalCardSales,
    decimal TotalTransferSales,
    decimal TotalOtherSales,
    decimal TotalDiscounts,
    decimal TotalCredits,
    decimal TotalTips,
    decimal CashIn,
    decimal CashOut,
    
    // Desglose por método
    List<PaymentMethodSummaryDto> PaymentBreakdown,
    
    // Movimientos manuales
    List<CashMovementResponse> Movements,
    
    // Empresa
    string CompanyName,
    string? CompanyNit
);
```

### Paso 2: Endpoints Backend

Agregar a `SalesController.cs` (ya existente en `Walos.API/Controllers/`):

```
GET /api/v1/sales/orders/{id}/receipt    → ReceiptData
GET /api/v1/sales/orders/{id}/kitchen    → KitchenTicketData  
```

Agregar a `CashRegisterController.cs` (ya existente):

```
GET /api/v1/sales/cash-register/{id}/z-report → ZReportData
```

**Lógica**: 
- El endpoint de receipt necesita JOINs para traer: company info (de `CompanySettings` o `core.companies`), items de la orden, pagos, datos del cajero.
- Puedes agregar los métodos al `ISalesService`/`SalesService` existente, o crear un servicio `IReceiptService` dedicado.
- Para company info, ya existe `ICompanyRepository` con `GetSettingsAsync`.
- Los pagos de una orden están en `sales.order_payments` → usar `IOrderPaymentRepository`.

**Datos disponibles** que ya existen en las queries actuales:

| Dato | Fuente | Cómo obtener |
|------|--------|--------------|
| Company info | `CompanySettings` | `companyRepo.GetSettingsAsync(companyId)` |
| Order + items | `ISalesRepository` | `GetOrderByIdAsync` + `GetOrderItemsAsync` |
| Pagos | `IOrderPaymentRepository` | Necesita método `GetByOrderIdAsync` (verificar si existe) |
| Cajero | `core.users` | JOIN en query o consulta separada |
| Cash register summary | `ICashRegisterService` | `GetSummaryAsync(id, companyId)` ya existe |

**Verificar si `IOrderPaymentRepository` tiene `GetByOrderIdAsync`**:

```bash
grep -r "GetByOrderId" backend-dotnet/src/
```

Si no existe, agregarlo:
```csharp
// En IOrderPaymentRepository
Task<IEnumerable<OrderPayment>> GetByOrderIdAsync(long orderId, long companyId);
```

### Paso 3: Frontend — Componentes de Impresión

Crear en `frontend/src/modules/sales/components/`:

#### 3a. `ReceiptPreview.jsx`
- Modal que muestra vista previa del recibo en formato térmico (80mm = ~302px)
- Estilo monospaciado, líneas punteadas, alineación derecha para precios
- Botón "Imprimir" que usa `window.print()` con `@media print` styles
- Recibe `orderId` como prop, hace fetch a `/sales/orders/{id}/receipt`

```jsx
// Estructura sugerida:
// ┌─────────────────────────┐
// │      NOMBRE EMPRESA     │
// │     NIT: 900.123.456    │
// │    Calle 123 #45-67     │
// ├─────────────────────────┤
// │ Mesa: 5  Cajero: Juan   │
// │ Fecha: 06/05/2026 14:30 │
// │ Orden: ORD-62-178807    │
// ├─────────────────────────┤
// │ Producto     Cant  Subt │
// │ Hamburguesa    2  24000 │
// │ Coca Cola      1   5000 │
// ├─────────────────────────┤
// │ Subtotal:         29000 │
// │ Descuento:        -5000 │
// │ Total:            24000 │
// │ Propina:           2400 │
// │ Total a cobrar:   26400 │
// ├─────────────────────────┤
// │ Pago: Efectivo    26400 │
// ├─────────────────────────┤
// │  Gracias por su visita  │
// └─────────────────────────┘
```

#### 3b. `KitchenTicket.jsx`
- Formato GRANDE: fuente 18-24px, solo producto + cantidad + notas
- **SIN precios** — regla del negocio
- Recibe `orderId`, fetch a `/sales/orders/{id}/kitchen`

```
// ┌─────────────────────────┐
// │   🍳 COMANDA COCINA     │
// │   Mesa 5  -  14:30      │
// ├─────────────────────────┤
// │ 2x Hamburguesa Clásica  │
// │ 1x Coca Cola            │
// │ 3x Papas Francesas      │
// │    → Sin sal (nota)     │
// └─────────────────────────┘
```

#### 3c. `ZReportPrint.jsx`
- Formato detallado con secciones: datos de caja, desglose ventas por método, movimientos, totales
- Recibe `registerId`, fetch a `/sales/cash-register/{id}/z-report`

#### 3d. `PrintButton.jsx`
- Componente reutilizable: recibe `content` (ReactNode) y llama a `window.print()`
- Usa un `<iframe>` oculto o `window.open()` para no afectar la página actual
- Inyecta estilos de impresión térmica automáticamente

### Paso 4: Estilos de Impresión Térmica

Crear `frontend/src/modules/sales/components/printStyles.js` con CSS-in-JS:

```javascript
export const thermalStyles = `
  @page { 
    size: 80mm auto; 
    margin: 0; 
  }
  body {
    font-family: 'Courier New', monospace;
    font-size: 12px;
    width: 80mm;
    max-width: 80mm;
    margin: 0 auto;
    padding: 4mm;
  }
  .receipt-header { text-align: center; margin-bottom: 8px; }
  .receipt-divider { border-top: 1px dashed #000; margin: 6px 0; }
  .receipt-row { display: flex; justify-content: space-between; }
  .receipt-total { font-size: 16px; font-weight: bold; }
  .kitchen-item { font-size: 18px; font-weight: bold; margin: 4px 0; }
  .kitchen-note { font-size: 14px; color: #666; padding-left: 16px; }
`;
```

### Paso 5: Integración con Flujos Existentes

1. **Al facturar** (`InvoicePanel.jsx`): Después de `onConfirm` exitoso, ofrecer "¿Imprimir recibo?" → abrir `ReceiptPreview` con el orderId retornado.
   - Actualmente `handleInvoice()` llama `await onConfirm(table.id, {...})` → el `onConfirm` en `SalesPage.jsx` retorna el resultado de `salesService.invoiceTable()`. Modificar para capturar el `orderId` del response.

2. **Al crear mesa** (`SalesPage.jsx`): Opción de imprimir comanda de cocina.
   - La función `handleCreateTable` ya retorna la respuesta. Agregar botón/opción post-creación.

3. **Al cerrar caja** (`CloseCashRegisterModal.jsx`): Auto-ofrecer imprimir Reporte Z.
   - El `onConfirm` retorna el registro cerrado. Ofrecer "Imprimir Reporte Z" con el `registerId`.

4. **En `SalesSummaryTab.jsx`**: Botón de imprimir recibo en cada orden (junto al botón de anular).

**NOTA sobre `handlePrint()` en InvoicePanel**: Ya existe una versión básica con HTML inline (líneas ~295-329). Reemplazar con el nuevo componente `ReceiptPreview` que use datos del backend.

### Paso 6: Servicio Frontend

Crear `frontend/src/services/printService.js`:

```javascript
import api from '../config/api';

export const printService = {
  getReceipt: async (orderId) => {
    const response = await api.get(`/sales/orders/${orderId}/receipt`);
    return response.data;
  },
  getKitchenTicket: async (orderId) => {
    const response = await api.get(`/sales/orders/${orderId}/kitchen`);
    return response.data;
  },
  getZReport: async (registerId) => {
    const response = await api.get(`/sales/cash-register/${registerId}/z-report`);
    return response.data;
  },
};

export default printService;
```

---

## P6 — HISTORIAL DE VENTAS EXTENDIDO

### Contexto

Actualmente `SalesSummaryTab.jsx` muestra ventas del día con un date picker simple. Las queries existentes:

- `GET /sales/summary?branchId=X&date=Y` → `SalesSummary` (totales, top productos, ventas por hora)
- `GET /sales/orders/completed?branchId=X&date=Y` → `List<CompletedOrder>` (lista del día)

Se necesita búsqueda avanzada multi-día con filtros y exportación CSV.

### Paso 1: DTOs Backend

Crear o agregar en `Walos.Application/DTOs/Sales/`:

```csharp
// Puede ir en un nuevo archivo OrderHistoryDtos.cs
namespace Walos.Application.DTOs.Sales;

public class OrderSearchRequest
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public long? CashierId { get; set; }
    public string? PaymentMethod { get; set; }  // cash, card, transfer, nequi, mixed
    public string? Status { get; set; }          // completed, cancelled
    public string? RefundStatus { get; set; }    // null, partial_refund, full_refund
    public string? Search { get; set; }          // buscar por orderNumber o tableName
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}

public record OrderDetailResponse(
    long Id,
    string OrderNumber,
    string TableName,
    int TableNumber,
    string Status,
    string? RefundStatus,
    decimal Subtotal,
    string? DiscountType,
    decimal DiscountValue,
    decimal DiscountAmount,
    decimal FinalTotalPaid,
    decimal TipAmount,
    bool TipIncluded,
    int SplitReferenceCount,
    bool HasCredit,
    decimal? CreditAmount,
    string? CreditCustomerName,
    string? CashierName,           // JOIN core.users ON created_by
    string? PaymentMethodSummary,  // "Efectivo" o "Mixto (Efectivo + Tarjeta)"
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    
    // Sub-colecciones (solo para detalle individual)
    List<ReceiptItemDto>? Items,
    List<ReceiptPaymentDto>? Payments,
    List<RefundSummaryDto>? Refunds
);

public record RefundSummaryDto(
    long Id,
    string RefundType,
    decimal RefundAmount,
    string Reason,
    DateTime CreatedAt
);
```

### Paso 2: Repository — Búsqueda Paginada

Agregar a `ISalesRepository`:

```csharp
Task<IEnumerable<OrderDetailResponse>> SearchOrdersAsync(long companyId, long branchId, OrderSearchRequest request);
Task<int> SearchOrdersCountAsync(long companyId, long branchId, OrderSearchRequest request);
Task<IEnumerable<OrderDetailResponse>> GetOrdersForExportAsync(long companyId, long branchId, DateTime dateFrom, DateTime dateTo);
```

**Implementación** en `SalesRepository.cs`:

La query SQL debe:
1. JOIN `sales.orders o` con `core.users u ON o.created_by = u.id` (nombre cajero)
2. LEFT JOIN `sales.order_payments op ON op.order_id = o.id` para determinar método de pago
3. Filtros dinámicos: `WHERE o.company_id = @CompanyId AND o.branch_id = @BranchId AND o.status IN ('completed', 'cancelled')`
4. Agregar condiciones opcionales según parámetros (dateFrom, dateTo, cashierId, etc.)
5. `ORDER BY o.created_at DESC LIMIT @Limit OFFSET @Offset`

**Para determinar método de pago de la orden**: 
```sql
-- Sub-query o CTE para determinar si es pago único o mixto
CASE 
    WHEN (SELECT COUNT(DISTINCT method) FROM sales.order_payments WHERE order_id = o.id) > 1 
    THEN 'mixed'
    ELSE (SELECT method FROM sales.order_payments WHERE order_id = o.id LIMIT 1)
END AS PaymentMethodSummary
```

**Para filtrar por método de pago**:
```sql
-- Si el filtro es 'cash', buscar órdenes donde al menos un pago sea 'cash'
AND EXISTS (SELECT 1 FROM sales.order_payments WHERE order_id = o.id AND method = @PaymentMethod)
```

### Paso 3: Service Layer

Agregar a `ISalesService` y `SalesService`:

```csharp
Task<(IEnumerable<OrderDetailResponse> Items, int TotalCount)> SearchOrdersAsync(
    long companyId, long branchId, OrderSearchRequest request);
Task<byte[]> ExportOrdersCsvAsync(
    long companyId, long branchId, DateTime dateFrom, DateTime dateTo);
```

**CSV Export**: Generar CSV manualmente (no necesita librería externa):
```csharp
var sb = new StringBuilder();
sb.AppendLine("Orden,Mesa,Fecha,Cajero,Subtotal,Descuento,Total,Propina,MetodoPago,Estado");
foreach (var o in orders)
{
    sb.AppendLine($"{o.OrderNumber},{o.TableName},{o.CreatedAt:yyyy-MM-dd HH:mm},{o.CashierName},{o.Subtotal},{o.DiscountAmount},{o.FinalTotalPaid},{o.TipAmount},{o.PaymentMethodSummary},{o.Status}");
}
return Encoding.UTF8.GetBytes(sb.ToString());
```

### Paso 4: Controller Endpoints

Agregar a `SalesController.cs`:

```csharp
[HttpGet("orders/search")]
public async Task<IActionResult> SearchOrders([FromQuery] OrderSearchRequest request)
{
    var branchId = _tenant.BranchId
        ?? throw new ValidationException("ID de sucursal requerido");
    
    var (items, count) = await _salesService.SearchOrdersAsync(
        _tenant.CompanyId, branchId, request);
    
    return Ok(ApiResponse<IEnumerable<OrderDetailResponse>>.Ok(items, count: count));
}

[HttpGet("orders/export")]
public async Task<IActionResult> ExportOrders(
    [FromQuery] DateTime dateFrom, [FromQuery] DateTime dateTo)
{
    var branchId = _tenant.BranchId
        ?? throw new ValidationException("ID de sucursal requerido");
    
    var csv = await _salesService.ExportOrdersCsvAsync(
        _tenant.CompanyId, branchId, dateFrom, dateTo);
    
    return File(csv, "text/csv", $"ventas_{dateFrom:yyyyMMdd}_{dateTo:yyyyMMdd}.csv");
}
```

### Paso 5: Servicio Frontend

Agregar a `salesService.js`:

```javascript
searchOrders: async (params = {}) => {
    const response = await api.get('/sales/orders/search', { params });
    return response.data;
},

exportOrders: async (dateFrom, dateTo) => {
    const response = await api.get('/sales/orders/export', {
        params: { dateFrom, dateTo },
        responseType: 'blob',
    });
    // Trigger download
    const url = window.URL.createObjectURL(new Blob([response.data]));
    const link = document.createElement('a');
    link.href = url;
    link.download = `ventas_${dateFrom}_${dateTo}.csv`;
    link.click();
    window.URL.revokeObjectURL(url);
},
```

### Paso 6: Frontend — Tab "Historial"

Agregar nuevo tab en `SalesPage.jsx`:

1. En el array `TABS` (línea ~299), agregar:
```javascript
{ k: 'history', label: 'Historial', icon: Clock },  // import Clock from lucide-react
```

2. Crear `frontend/src/modules/sales/components/OrderHistoryTab.jsx`:

**Estructura del componente**:

```
┌──────────────────────────────────────────────┐
│  📅 Desde [____]  Hasta [____]  🔍 Buscar    │
│  Cajero: [▼ Todos]  Método: [▼ Todos]       │
│  Estado: [▼ Todos]  [Buscar] [Exportar CSV]  │
├──────────────────────────────────────────────┤
│  Resultados: 156 órdenes                     │
├──────────────────────────────────────────────┤
│  ORD-62-178807 │ Mesa 5 │ $24,000 │ Efectivo│
│  14:30 │ Juan P. │ ✅ Completada             │
│  ─── click para expandir ───                 │
│    Items: 2x Hamburguesa, 1x Cola            │
│    Pagos: Efectivo $26,400                   │
│    [Imprimir Recibo] [Anular]                │
├──────────────────────────────────────────────┤
│  ORD-61-178807 │ Mesa 3 │ $15,000 │ Tarjeta │
│  ...                                         │
├──────────────────────────────────────────────┤
│  ◀ Página 1 de 8 ▶                          │
└──────────────────────────────────────────────┘
```

**Implementación**:
- **Filtros**: `useState` para cada filtro + `useQuery` con los params
- **Paginación**: `page` y `limit` en el state, botones Anterior/Siguiente
- **Expandir orden**: click para ver items, pagos y devoluciones (sub-fetch o inline)
- **Exportar**: botón que llama `salesService.exportOrders(dateFrom, dateTo)`
- **Refund**: reutilizar el `RefundModal` existente (mismo patrón que en `SalesSummaryTab`)
- **Imprimir**: usar el `ReceiptPreview` de P4

**React Query keys**:
```javascript
queryKey: ['orders-search', branchId, dateFrom, dateTo, cashierId, method, status, search, page]
```

### Paso 7: Integración con SalesPage

En `SalesPage.jsx`:
1. Importar `OrderHistoryTab` y el icono `Clock`
2. Agregar al array `TABS`
3. Agregar renderizado condicional: `{activeTab === 'history' && <OrderHistoryTab />}`

---

## Checklist de Archivos a Crear/Modificar

### P4 — Impresión

| Acción | Archivo | Descripción |
|--------|---------|-------------|
| **CREAR** | `Application/DTOs/Sales/ReceiptDtos.cs` | DTOs: ReceiptData, KitchenTicketData, ZReportData |
| **CREAR o MODIFICAR** | Service layer para receipt | Lógica para construir DTOs con JOINs |
| **MODIFICAR** | `API/Controllers/SalesController.cs` | Agregar endpoints receipt + kitchen |
| **MODIFICAR** | `API/Controllers/CashRegisterController.cs` | Agregar endpoint z-report |
| **VERIFICAR** | `IOrderPaymentRepository` | Asegurar que tiene `GetByOrderIdAsync` |
| **CREAR** | `frontend/src/services/printService.js` | API wrapper |
| **CREAR** | `frontend/src/modules/sales/components/ReceiptPreview.jsx` | Modal recibo |
| **CREAR** | `frontend/src/modules/sales/components/KitchenTicket.jsx` | Comanda cocina |
| **CREAR** | `frontend/src/modules/sales/components/ZReportPrint.jsx` | Reporte Z |
| **CREAR** | `frontend/src/modules/sales/components/PrintButton.jsx` | Componente reutilizable |
| **CREAR** | `frontend/src/modules/sales/components/printStyles.js` | CSS térmico |
| **MODIFICAR** | `frontend/.../InvoicePanel.jsx` | Reemplazar handlePrint con ReceiptPreview |
| **MODIFICAR** | `frontend/.../SalesSummaryTab.jsx` | Botón imprimir en cada orden |
| **MODIFICAR** | `frontend/.../CloseCashRegisterModal.jsx` | Ofrecer imprimir Reporte Z |
| **MODIFICAR** | `Infrastructure/DependencyInjection.cs` | Si se crea servicio nuevo |

### P6 — Historial

| Acción | Archivo | Descripción |
|--------|---------|-------------|
| **CREAR** | `Application/DTOs/Sales/OrderHistoryDtos.cs` | OrderSearchRequest, OrderDetailResponse |
| **MODIFICAR** | `Domain/Interfaces/ISalesRepository.cs` | Agregar SearchOrdersAsync, CountAsync |
| **MODIFICAR** | `Infrastructure/Repositories/SalesRepository.cs` | Implementar queries paginadas |
| **MODIFICAR** | `Application/Services/ISalesService.cs` + `SalesService.cs` | SearchOrders + ExportCsv |
| **MODIFICAR** | `API/Controllers/SalesController.cs` | Endpoints search + export |
| **MODIFICAR** | `frontend/src/services/salesService.js` | searchOrders + exportOrders |
| **CREAR** | `frontend/src/modules/sales/components/OrderHistoryTab.jsx` | Tab completo con filtros |
| **MODIFICAR** | `frontend/src/modules/sales/SalesPage.jsx` | Agregar tab "Historial" |

---

## Tablas SQL Existentes (referencia)

```sql
-- Ya existen, NO crear:
sales.orders          -- id, company_id, branch_id, table_id, order_number, status, subtotal, 
                      -- discount_type, discount_value, discount_amount, final_total_paid,
                      -- split_reference_count, notes, created_by, refund_status,
                      -- cash_register_id, tip_amount, tip_included, created_at, updated_at

sales.order_items     -- id, company_id, order_id, product_id, product_name, quantity, 
                      -- unit_price, subtotal, notes, created_at

sales.order_payments  -- id, company_id, order_id, method, amount, reference, created_at

sales.tables          -- id, company_id, branch_id, table_number, name, status, total,
                      -- order_id, created_at, updated_at

sales.cash_registers  -- id, company_id, branch_id, opened_by, closed_by, status,
                      -- opening_amount, closing_amount, expected_cash, difference,
                      -- total_sales, total_cash_sales, total_card_sales, total_transfer_sales,
                      -- total_other_sales, total_discounts, total_credits, total_tips,
                      -- cash_in, cash_out, order_count, notes, opened_at, closed_at

sales.cash_movements  -- id, company_id, cash_register_id, type, amount, reason, notes,
                      -- created_by, created_at

sales.refunds         -- id, company_id, branch_id, order_id, refund_type, refund_amount,
                      -- reason, status, approved_by, created_by, created_at

sales.refund_items    -- id, refund_id, order_item_id, quantity, unit_price, subtotal, created_at

core.companies        -- id, name, legal_name, nit, address, phone, logo_url, ...
core.users            -- id, first_name, last_name, email, role, ...
```

**NOTA**: Si alguna columna no existe en la DB real (ej: `tip_amount` en `orders`, `refund_status` en `orders`), ejecutar el ALTER TABLE correspondiente en Supabase:

```sql
-- Verificar y ejecutar si faltan:
ALTER TABLE sales.orders ADD COLUMN IF NOT EXISTS tip_amount DECIMAL(18,2) NOT NULL DEFAULT 0;
ALTER TABLE sales.orders ADD COLUMN IF NOT EXISTS tip_included BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE sales.orders ADD COLUMN IF NOT EXISTS refund_status VARCHAR(20);
ALTER TABLE sales.orders ADD COLUMN IF NOT EXISTS cash_register_id BIGINT REFERENCES sales.cash_registers(id);
```

---

## Orden de Ejecución Recomendado

```
1. P4 Backend: DTOs + endpoints (receipt, kitchen, z-report)
2. P4 Frontend: printStyles + PrintButton + ReceiptPreview
3. P4 Frontend: KitchenTicket + ZReportPrint
4. P4 Integración: InvoicePanel, SalesSummaryTab, CloseCashRegisterModal
5. P6 Backend: DTOs + repo + service + controller (search + export)
6. P6 Frontend: OrderHistoryTab + integración en SalesPage
7. Build + verificación completa (dotnet build + npx vite build)
8. Commit
```

---

## Reglas para el Agente Ejecutor

1. **NO modificar** lógica existente de facturación, descuentos, créditos o pagos — ya funciona.
2. **NO usar Entity Framework** — todo es Dapper + SQL explícito.
3. **Registrar en DI** (`DependencyInjection.cs`) cualquier repositorio o servicio nuevo.
4. **Build verification**: `dotnet build --nologo` (0 errors) + `npx vite build` (✔ built).
5. **Si el proceso Walos.API está corriendo**, matarlo antes de hacer `dotnet build` (DLL lock).
6. **Iconos Lucide**: verificar que existen antes de importar. Usar `node -e "const l=require('lucide-react');console.log('IconName' in l)"`.
7. **Estilo frontend**: TailwindCSS, mobile-first, componentes funcionales con hooks.
8. **Query invalidation**: después de mutaciones, invalidar las queries relevantes.
9. **Español** para UI labels y mensajes de error del backend.
10. **Commits atómicos**: un commit por feature completada, con mensaje descriptivo.
