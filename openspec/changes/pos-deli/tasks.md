# Tasks: Módulo POS-Deli

> **Change ID**: pos-deli
> **Status**: in_progress
> **Version**: 1.0

---

## Phase 1 — Vista POS-Deli (Frontend)

**Objetivo**: Construir la UI completa del POS sin integración real de báscula ni backend.

### 1.1 Crear estructura del módulo

- [x] Crear directorio `frontend/src/modules/pos-deli/`
- [x] Crear `PosDeliPage.jsx` con layout de dos paneles
- [x] Agregar ruta `/pos-deli` en `App.jsx` (protegida, roles: admin, manager, cashier)
- [ ] Agregar item de navegación en sidebar

**Archivos**: `App.jsx`, `components/layout/Sidebar.jsx`, `modules/pos-deli/PosDeliPage.jsx`

### 1.2 Crear store del ticket

- [x] Crear `modules/pos-deli/stores/posDeliStore.js` con Zustand
- [x] Implementar acciones: `addWeighedItem`, `addUnitItem`, `removeItem`, `updateQuantity`, `clearTicket`
- [x] Agregar persist middleware con `sessionStorage`
- [ ] Calcular total automáticamente en cada cambio

**Archivos**: `modules/pos-deli/stores/posDeliStore.js`

### 1.3 Crear componente TicketPanel

- [x] Lista de items con nombre, qty/peso, precio, subtotal
- [x] Total en tiempo real
- [ ] Botón eliminar por item
- [ ] Botón COBRAR (F12) prominente
- [x] Modificar cantidad de items por unidad (+ / -)
- [ ] Indicador de ticket vacío

**Archivos**: `modules/pos-deli/components/TicketPanel.jsx`

### 1.4 Crear componente ProductGrid

- [ ] Sección "Pesables" con tarjetas de productos (nombre + precio/kg)
- [ ] Sección "Por unidad" con tarjetas (nombre + precio)
- [ ] Click en pesable → agregar al ticket (con peso mock de 0.350 por ahora)
- [ ] Click en unidad → agregar al ticket con qty=1
- [x] Productos cargados desde inventario existente

**Archivos**: `modules/pos-deli/components/ProductGrid.jsx`

### 1.5 Crear componente ProductSearchBar

- [x] Input con autofocus permanente
- [ ] Búsqueda por nombre o barcode
- [ ] Resultados en dropdown
- [ ] Al seleccionar → agregar al ticket
- [ ] Clear automático después de agregar

**Archivos**: `modules/pos-deli/components/ProductSearchBar.jsx`

### 1.6 Crear hook useBarcodeScanner

- [x] Listener global de `keydown`
- [ ] Detectar input rápido (< 50ms entre chars)
- [x] Detectar Enter como fin de barcode
- [x] Callback `onScan(barcode)` al detectar
- [ ] Ignorar si un input de texto tiene focus (excepto el de búsqueda)

**Archivos**: `modules/pos-deli/hooks/useBarcodeScanner.js`

### 1.7 Crear ScaleIndicator (mock)

- [x] Display de peso actual (mock: "0.000 kg")
- [x] Indicador de estado: Conectada / Desconectada / Inestable
- [ ] Botón "Conectar báscula" (no-op por ahora)
- [ ] Diseño prominente y visible

**Archivos**: `modules/pos-deli/components/ScaleIndicator.jsx`

### 1.8 Crear PaymentModal

- [ ] Modal fullscreen con métodos de pago (reutilizar estilo de Sales)
- [x] Selector: Efectivo, Nequi, Tarjeta
- [x] Campo "Monto recibido" grande para efectivo
- [ ] Cálculo de cambio en tiempo real
- [ ] Botón confirmar
- [ ] Cierra y limpia ticket al confirmar (sin backend aún)

**Archivos**: `modules/pos-deli/components/PaymentModal.jsx`

### 1.9 Atajos de teclado

- [ ] F12 → Abrir modal de cobro
- [ ] F1 → Focus en barra de búsqueda
- [ ] Escape → Cerrar modal activo
- [ ] Delete → Eliminar item seleccionado

**Archivos**: `modules/pos-deli/PosDeliPage.jsx` (useEffect con keydown)

### Criterio de cierre Fase 1

- [ ] La vista POS se renderiza correctamente en `/pos-deli`
- [ ] Se pueden agregar productos al ticket (mock)
- [ ] Se puede "cobrar" (sin backend, solo limpia ticket)
- [ ] Atajos de teclado funcionan
- [ ] El barcode scanner detecta input rápido vs humano

---

## Phase 2 — Hook useScale (Web Serial API)

**Objetivo**: Leer peso real de la báscula BBG SHOP POLE.

**Dependencia**: Tener el adaptador USB-Serial conectado.

### 2.1 Implementar useScale hook

- [ ] Función `connect()` que llama `navigator.serial.requestPort()`
- [ ] Configuración: baudRate 9600, dataBits 8, stopBits 1, parity 'none'
- [x] Reader loop con `ReadableStream`
- [x] Buffer de bytes para acumular mensajes parciales
- [ ] Detección de delimitador (CR/LF)

**Archivos**: `modules/pos-deli/hooks/useScale.js`

### 2.2 Implementar parser BBG SHOP POLE

- [ ] Parsear formato de la báscula (ajustar con datos reales)
- [ ] Extraer: peso (number), estable (boolean), unidad (string)
- [ ] Manejar formatos con/sin signo, con/sin espacios
- [ ] Ignorar mensajes incompletos o corruptos
- [ ] Tests unitarios para el parser

**Archivos**: `modules/pos-deli/hooks/parsers/bbgShopPole.js`, test file

### 2.3 Integrar useScale en PosDeliPage

- [x] Reemplazar ScaleIndicator mock con datos reales
- [ ] Peso en tiempo real con throttle (max 5 updates/s)
- [ ] Estado de conexión real
- [ ] Al click en producto pesable → leer `weight` del hook
- [ ] Validar: si weight=0 → mostrar error "Coloca producto en báscula"

**Archivos**: `modules/pos-deli/PosDeliPage.jsx`, `ScaleIndicator.jsx`

### 2.4 Fallback peso manual

- [x] Crear `WeightInputModal.jsx`
- [ ] Se abre cuando báscula está desconectada y se selecciona producto pesable
- [ ] Campo numérico para ingresar peso en kg
- [ ] Validar: > 0, <= peso máximo del producto

**Archivos**: `modules/pos-deli/components/WeightInputModal.jsx`

### Criterio de cierre Fase 2

- [ ] El peso de la báscula aparece en tiempo real en el POS
- [ ] Se puede agregar un producto pesable con el peso real
- [ ] Si la báscula está desconectada, funciona el peso manual
- [ ] Parser tiene tests unitarios que pasan

---

## Phase 3 — Backend endpoint

**Objetivo**: Crear el endpoint que registra la venta, descuenta stock y genera factura.

### 3.1 Crear PosDeliController

- [ ] `POST /api/v1/pos-deli/sale` — registrar venta completa
- [ ] `GET /api/v1/pos-deli/products` — listar productos para POS (con filtro search, category, barcode)
- [ ] `GET /api/v1/pos-deli/favorites` — productos marcados como favoritos
- [ ] Inyectar `IInventoryRepository`, `ISalesRepository`, `IOrderPaymentRepository`, `ITenantContext`

**Archivos**: `backend-dotnet/src/Walos.API/Controllers/PosDeliController.cs`

### 3.2 Crear DTOs

- [x] `PosDeliSaleRequest` (items + payments)
- [x] `PosDeliSaleItem` (productId, quantity, unitPrice, isWeighed)
- [x] `PosDeliPayment` (method, amount)
- [x] `PosDeliSaleResponse` (saleId, ticketNumber, total, change)

**Archivos**: `backend-dotnet/src/Walos.Application/DTOs/PosDeli/`

### 3.3 Implementar lógica de venta

- [ ] Validar request (items no vacíos, quantities > 0)
- [x] Verificar stock disponible para cada item
- [ ] Crear orden en `sales.orders` con `order_type = 'pos-deli'`
- [x] Crear items en `sales.order_items`
- [x] Registrar pagos
- [x] Descontar stock (UPDATE inventory.stock)
- [x] Crear movimientos (inventory.movements, type='sale')
- [ ] Todo en transacción

**Archivos**: `PosDeliController.cs` (o extraer a service si es complejo)

### 3.4 Endpoint de búsqueda por barcode

- [x] Agregar filtro por barcode en `GET /pos-deli/products?barcode=XXX`
- [x] Retornar producto con: id, name, sku, barcode, salePrice, productType, trackStock, imageUrl, unit
- [ ] Si no existe → 404

**Archivos**: `PosDeliController.cs`

### 3.5 Registrar DI

- [ ] Registrar controller en DI si es necesario
- [ ] Verificar que repos existentes cubren las queries necesarias
- [ ] Si falta query (ej: búsqueda por barcode) → agregar método al repo

**Archivos**: `Infrastructure/DependencyInjection.cs`, `IInventoryRepository.cs`

### 3.6 Tests

- [ ] Test de integración para `POST /pos-deli/sale`
- [ ] Test que verifica descuento de stock correcto
- [ ] Test que verifica transacción se revierte si falla

**Archivos**: `tests/Walos.Tests/Integration/PosDeliIntegrationTests.cs`

### Criterio de cierre Fase 3

- [ ] `POST /pos-deli/sale` crea orden, descuenta stock, retorna cambio
- [ ] `GET /pos-deli/products?barcode=XXX` retorna producto correcto
- [ ] Tests de integración pasan
- [ ] `dotnet build` + `dotnet test` exitosos

---

## Phase 4 — Integración completa

**Objetivo**: Conectar frontend con backend real.

### 4.1 Crear posDeliService.js

- [ ] `createSale(items, payments)` → POST /pos-deli/sale
- [ ] `getProducts(search, category, barcode)` → GET /pos-deli/products
- [ ] `getFavorites()` → GET /pos-deli/favorites

**Archivos**: `frontend/src/services/posDeliService.js`

### 4.2 Conectar ProductGrid con backend

- [x] Cargar productos reales con `useQuery`
- [ ] Filtrar por categoría pesable vs unidad (usar campo `productType` o nuevo flag)
- [ ] Implementar búsqueda real por nombre/barcode

**Archivos**: `ProductGrid.jsx`, `ProductSearchBar.jsx`

### 4.3 Conectar PaymentModal con backend

- [ ] Al confirmar pago → llamar `posDeliService.createSale()`
- [x] Manejar errores (stock insuficiente, etc.)
- [ ] Mostrar toast de éxito/error
- [x] Limpiar ticket solo si la venta fue exitosa

**Archivos**: `PaymentModal.jsx`

### 4.4 Conectar barcode scan con búsqueda real

- [ ] `onScan(barcode)` → buscar en backend por barcode
- [ ] Si encontrado → agregar al ticket
- [ ] Si no encontrado → toast de error

**Archivos**: `PosDeliPage.jsx`

### Criterio de cierre Fase 4

- [ ] Venta completa funcional: seleccionar/escanear → cobrar → stock descontado
- [ ] Barcode busca producto real en backend
- [ ] Errores se manejan gracefully (stock insuficiente, red, etc.)

---

## Phase 5 — Impresión y pulido

**Objetivo**: Ticket impreso, atajos refinados, UX pulida.

### 5.1 Impresión de ticket

- [ ] Reutilizar `printService.js` o crear template de ticket POS-Deli
- [ ] Template: nombre comercio, fecha, items (nombre, peso/qty, precio, subtotal), total, pago, cambio
- [ ] Imprimir automáticamente al cerrar venta (configurable)

**Archivos**: `services/printService.js` (agregar template), `PaymentModal.jsx`

### 5.2 Sonidos/feedback

- [ ] Beep corto al escanear producto exitosamente
- [ ] Beep de error si barcode no encontrado
- [ ] Feedback visual al agregar item (flash en ticket)

### 5.3 Configuración de favoritos

- [ ] Permitir marcar productos como "favorito POS" desde el grid (long press o botón)
- [ ] Los favoritos aparecen primero en el grid
- [ ] Persistir en backend

### 5.4 Tests E2E

- [ ] Playwright test: flujo completo de venta con productos mock
- [ ] Playwright test: barcode scan simulation
- [ ] Test de accesibilidad del teclado

**Archivos**: `frontend/e2e/pos-deli.spec.js`

### Criterio de cierre Fase 5

- [ ] Ticket se imprime correctamente al cerrar venta
- [ ] Atajos de teclado cubren el 100% del flujo sin mouse
- [ ] E2E test pasa
- [ ] UX validada con usuario real (cajero de salsamentaría)

---

## Summary

| Fase | Tasks | Estimación | Bloquea |
|---|---|---|---|
| 1 | 9 tasks (1.1–1.9) | 6-8h | Nada |
| 2 | 4 tasks (2.1–2.4) | 3-4h | Adaptador USB-Serial |
| 3 | 6 tasks (3.1–3.6) | 3-4h | Nada |
| 4 | 4 tasks (4.1–4.4) | 2-3h | Fase 1 + Fase 3 |
| 5 | 4 tasks (5.1–5.4) | 2-3h | Fase 4 |

**Total**: ~18-22 horas

**Pueden ejecutarse en paralelo**: Fase 1 y Fase 3 (frontend y backend independientes). Fase 2 es independiente pero necesita hardware.
