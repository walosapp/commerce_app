# WALOS B1 — AUDITORÍA DE CIERRE Y CONGELACIÓN

> **Fecha**: 2026-09-14  
> **Objetivo**: Congelar estado actual como Walos B1 y definir backlog estructurado para V2  
> **Metodología**: Auditoría exhaustiva de código, endpoints, tablas, tests y deuda técnica

---

## PARTE 1 — AUDITORÍA DEL ESTADO ACTUAL

### 1.1 AUTENTICACIÓN

**Estado**: ✅ **TERMINADO**

**Archivos principales**:
- Backend: `AuthController.cs`, `AuthService.cs`, `AuthRepository.cs`
- Frontend: `LoginPage.jsx`, `authStore.js`, `authService.js`
- Middleware: `TenantContextMiddleware.cs`

**Endpoints**:
```
POST /api/v1/auth/login          → Login con JWT
POST /api/v1/auth/refresh        → Renovar token
POST /api/v1/auth/logout         → Cerrar sesión
```

**Tablas**:
- `core.users` (password_hash BCrypt, lockout, email_verified)
- `core.refresh_tokens` (token, expires_at, revoked_at)

**Tests**:
- ✅ `AuthServiceTests.cs` (login, lockout, refresh)
- ✅ `AuthRepositoryIntegrationTests.cs`
- ✅ `LoginPage.test.jsx`

**Deuda conocida**:
- 🔴 **BLOCKER**: FluentValidation sin validator para `LoginRequest` → 422 en producción
- 🟠 **HIGH**: Deserialización JSON case-sensitive (frontend envía `username`, backend espera `Username`)

**Riesgo**: 🔴 **ALTO** — Login puede fallar en producción por validación

**Dependencias**: Ninguna (módulo base)

---

### 1.2 TENANTS / COMPANY / BRANCH

**Estado**: ✅ **TERMINADO**

**Archivos principales**:
- Backend: `CompanyController.cs`, `CompanyService.cs`, `CompanyRepository.cs`, `AdminController.cs`
- Frontend: `CompaniesPage.jsx`, `TenantsPage.jsx`, `CreateTenantModal.jsx`
- Middleware: `TenantContextMiddleware.cs`, `TenantContext.cs`

**Endpoints**:
```
GET    /api/v1/company/settings              → Configuración empresa
PUT    /api/v1/company/settings              → Actualizar settings
POST   /api/v1/company/settings/logo         → Upload logo
GET    /api/v1/admin/companies               → Listar empresas (platform admin)
POST   /api/v1/admin/companies               → Crear empresa
GET    /api/v1/admin/tenants                 → Listar tenants
POST   /api/v1/admin/tenants                 → Onboarding tenant
```

**Tablas**:
- `core.companies` (name, tax_id, logo_url, theme_preference, subscription_plan)
- `core.branches` (company_id, name, code, is_main, is_active)
- `core.company_settings` (branding, operational rules)

**Tests**:
- ✅ `CompanyServiceTests.cs`
- ✅ `CompanyControllerTests.cs`
- ⚠️ Falta: Tests de aislamiento multi-tenant

**Deuda conocida**:
- 🟡 **MEDIUM**: Logo legacy (`/uploads/branding/`) sin fallback a default
- 🟡 **MEDIUM**: Archivos huérfanos en `wwwroot/uploads/branding/` (company 16 missing)
- 🟢 **LOW**: Migración pendiente de logos legacy a Supabase Storage

**Riesgo**: 🟡 **MEDIO** — Branding puede fallar si archivo no existe

**Dependencias**: Auth (JWT claims con `companyId`, `branchId`)

---

### 1.3 USUARIOS / ROLES

**Estado**: ✅ **TERMINADO**

**Archivos principales**:
- Backend: `UsersController.cs`, `UsersService.cs`, `UsersRepository.cs`
- Frontend: `UsersPage.jsx`, `UserFormModal.jsx`
- Security: `WalosAuthorization.cs`, `WalosAuthorizationExtensions.cs`

**Endpoints**:
```
GET    /api/v1/users                         → Listar usuarios
POST   /api/v1/users                         → Crear usuario
PUT    /api/v1/users/{id}                    → Actualizar usuario
DELETE /api/v1/users/{id}                    → Eliminar usuario (soft delete)
GET    /api/v1/users/{id}/permissions        → Permisos del usuario
```

**Tablas**:
- `core.users` (company_id, branch_id, role_id, first_name, last_name, email, password_hash)
- `core.roles` (company_id, code, permissions JSONB, access_level, is_system_role)

**Tests**:
- ✅ `UsersServiceSecurityTests.cs`
- ✅ `UsersRepositorySecurityIntegrationTests.cs`
- ✅ `AuthorizationPolicyTests.cs`

**Deuda conocida**:
- 🟠 **HIGH**: Rol `dev` NO incluido en políticas `Finance`, `Settings`, `Users` → 403 Forbidden
- 🟡 **MEDIUM**: Frontend `AdminUsersPage.jsx` vs `UsersPage.jsx` duplicado
- 🟡 **MEDIUM**: Permisos JSONB sin validación de estructura

**Riesgo**: 🟠 **MEDIO** — Usuario dev bloqueado en módulos críticos

**Dependencias**: Auth, Tenants

---

### 1.4 POS-DELI

**Estado**: ✅ **FUNCIONAL CON DEUDA**

**Archivos principales**:
- Backend: `PosDeliController.cs`, `PosDeliService.cs`
- Frontend: `PosDeliPage.jsx`, `ProductGrid.jsx`, `PaymentModal.jsx`, `TicketPanel.jsx`, `ScaleIndicator.jsx`, `WeightInputModal.jsx`

**Endpoints**:
```
POST /api/v1/pos-deli/sale                   → Crear venta directa (idempotente)
GET  /api/v1/pos-deli/products               → Productos para POS
```

**Tablas**:
- `sales.orders` (order_type = 'pos_deli', idempotency_key)
- `sales.order_items`
- `sales.order_payments`
- `sales.idempotency_log` (key, payload_hash, response, created_at)

**Tests**:
- ✅ E2E: `pos-deli.spec.js` (Playwright)
- ✅ Backend: Idempotencia verificada en `REPORTE-NOCTURNO-FASE-1.md`

**Deuda conocida**:
- 🟡 **MEDIUM**: Báscula simulada (no integración real con hardware)
- 🟡 **MEDIUM**: `ScaleIndicator` solo muestra estado mock
- 🟢 **LOW**: Falta configuración de impresora por defecto

**Riesgo**: 🟢 **BAJO** — Funcional para ventas directas sin hardware

**Dependencias**: Inventario (productos), Caja (cash_register_id)

---

### 1.5 RESTAURANTE / MESAS

**Estado**: ✅ **TERMINADO**

**Archivos principales**:
- Backend: `SalesController.cs`, `SalesService.cs`, `SalesRepository.cs`
- Frontend: `SalesPage.jsx`, `TableCard.jsx`, `InvoicePanel.jsx`, `AddTablePanel.jsx`, `OrderItemsList.jsx`

**Endpoints**:
```
GET    /api/v1/sales/tables                  → Mesas activas
POST   /api/v1/sales/tables                  → Crear mesa
POST   /api/v1/sales/tables/{id}/invoice     → Facturar mesa
POST   /api/v1/sales/tables/{id}/cancel      → Cancelar mesa
PATCH  /api/v1/sales/items/{id}/quantity     → Actualizar cantidad (+/-)
POST   /api/v1/sales/tables/{id}/items       → Agregar productos a mesa
PUT    /api/v1/sales/tables/{id}/name        → Renombrar mesa
```

**Tablas**:
- `sales.orders` (table_name, table_number, status: open/invoiced/cancelled)
- `sales.order_items` (order_id, product_id, quantity, unit_price, subtotal)
- `sales.order_payments` (order_id, method, amount, reference)

**Tests**:
- ✅ `SalesServiceTests.cs`
- ✅ E2E: `sales.spec.js`

**Deuda conocida**:
- 🟡 **MEDIUM**: Stock comprometido calculado en runtime (CTE), no materializado
- 🟡 **MEDIUM**: División de cuenta solo informativa (no split real)
- 🟢 **LOW**: Mesas arrastrables solo en desktop

**Riesgo**: 🟢 **BAJO** — Funcional y estable

**Dependencias**: Inventario (productos, stock), Caja (facturación)

---

### 1.6 INVENTARIO

**Estado**: ✅ **TERMINADO**

**Archivos principales**:
- Backend: `InventoryController.cs`, `InventoryService.cs`, `InventoryRepository.cs`
- Frontend: `InventoryPage.jsx`, `ProductFormModal.jsx`, `AddStockModal.jsx`, `StockTable.jsx`, `RecipeManager.jsx`, `ImportProductsModal.jsx`

**Endpoints**:
```
GET    /api/v1/inventory/products            → Listar productos
POST   /api/v1/inventory/products            → Crear producto
PUT    /api/v1/inventory/products/{id}       → Actualizar producto
DELETE /api/v1/inventory/products/{id}       → Eliminar (soft delete)
POST   /api/v1/inventory/products/{id}/image → Upload imagen
GET    /api/v1/inventory/stock               → Stock por sucursal
POST   /api/v1/inventory/stock/add           → Agregar stock (costo promedio ponderado)
GET    /api/v1/inventory/movements           → Historial de movimientos
GET    /api/v1/inventory/alerts              → Alertas de stock bajo
POST   /api/v1/inventory/import              → Importar productos desde Excel
```

**Tablas**:
- `inventory.products` (name, sku, barcode, product_type, cost_price, sale_price, track_stock)
- `inventory.stock` (product_id, branch_id, quantity, reserved_quantity)
- `inventory.movements` (product_id, branch_id, movement_type, quantity, cost_price, reference_type, reference_id)
- `inventory.categories`, `inventory.units`
- `inventory.recipes` (product_id, ingredient_id, quantity_required)

**Tests**:
- ✅ `InventoryServiceTests.cs`
- ✅ E2E: `inventory.spec.js`

**Deuda conocida**:
- 🟡 **MEDIUM**: `InventoryController` sobrecargado (20+ endpoints)
- 🟡 **MEDIUM**: Costo promedio ponderado sin auditoría de cambios
- 🟢 **LOW**: Importación Excel sin validación de duplicados por SKU

**Riesgo**: 🟢 **BAJO** — Funcional y robusto

**Dependencias**: Ninguna (módulo base)

---

### 1.7 RECETAS

**Estado**: ✅ **TERMINADO**

**Archivos principales**:
- Backend: `RecipesController.cs`, `RecipeService.cs`, `RecipeRepository.cs`
- Frontend: `RecipeManager.jsx` (integrado en InventoryPage)

**Endpoints**:
```
GET    /api/v1/recipes/{productId}           → Obtener receta de producto
POST   /api/v1/recipes                       → Crear/actualizar receta
DELETE /api/v1/recipes/{productId}           → Eliminar receta
```

**Tablas**:
- `inventory.recipes` (product_id, ingredient_id, quantity_required)

**Tests**:
- ⚠️ Falta: Tests unitarios de RecipeService
- ⚠️ Falta: E2E de recetas

**Deuda conocida**:
- 🟡 **MEDIUM**: No valida ciclos en recetas (producto A usa B, B usa A)
- 🟡 **MEDIUM**: No calcula costo de producto preparado automáticamente
- 🟢 **LOW**: UI de recetas básica (sin drag & drop)

**Riesgo**: 🟡 **MEDIO** — Funcional pero sin validaciones críticas

**Dependencias**: Inventario (productos)

---

### 1.8 COMPRAS

**Estado**: ✅ **TERMINADO**

**Archivos principales**:
- Backend: `PurchaseOrdersController.cs`, `PurchaseOrderService.cs`, `PurchaseOrderRepository.cs`
- Frontend: `PurchaseOrderModal.jsx`, `PurchaseOrderDetailPanel.jsx`, `ReceiveOrderModal.jsx`

**Endpoints**:
```
GET    /api/v1/purchase-orders               → Listar órdenes de compra
POST   /api/v1/purchase-orders               → Crear orden
PUT    /api/v1/purchase-orders/{id}          → Actualizar orden
POST   /api/v1/purchase-orders/{id}/receive  → Recibir orden (genera movimiento stock)
DELETE /api/v1/purchase-orders/{id}          → Cancelar orden
```

**Tablas**:
- `suppliers.purchase_orders` (supplier_id, order_number, status, total_amount)
- `suppliers.purchase_order_items` (purchase_order_id, product_id, quantity, unit_price)

**Tests**:
- ⚠️ Falta: Tests unitarios
- ⚠️ Falta: E2E

**Deuda conocida**:
- 🟡 **MEDIUM**: No valida stock disponible al crear orden
- 🟡 **MEDIUM**: Recepción parcial no implementada (solo recepción total)
- 🟢 **LOW**: No genera PDF de orden de compra

**Riesgo**: 🟡 **MEDIO** — Funcional pero incompleto

**Dependencias**: Proveedores, Inventario

---

### 1.9 PROVEEDORES

**Estado**: ✅ **TERMINADO**

**Archivos principales**:
- Backend: `SuppliersController.cs`, `SupplierService.cs`, `SupplierRepository.cs`
- Frontend: `SuppliersPage.jsx`, `SupplierFormModal.jsx`, `SupplierDetailPanel.jsx`, `ContactActions.jsx`, `SuggestedOrderPanel.jsx`

**Endpoints**:
```
GET    /api/v1/suppliers                     → Listar proveedores
POST   /api/v1/suppliers                     → Crear proveedor
PUT    /api/v1/suppliers/{id}                → Actualizar proveedor
DELETE /api/v1/suppliers/{id}                → Eliminar (soft delete)
GET    /api/v1/suppliers/{id}/products       → Productos del proveedor
POST   /api/v1/suppliers/{id}/products       → Asociar producto
DELETE /api/v1/suppliers/{id}/products/{pid} → Desasociar producto
GET    /api/v1/suppliers/{id}/suggested-order → Pedido sugerido por IA
```

**Tablas**:
- `suppliers.suppliers` (name, contact_name, phone, email, address)
- `suppliers.supplier_products` (supplier_id, product_id, supplier_sku, cost_price)

**Tests**:
- ⚠️ Falta: Tests unitarios
- ⚠️ Falta: E2E

**Deuda conocida**:
- 🟡 **MEDIUM**: Contacto WhatsApp genera URL pero no integra API real
- 🟡 **MEDIUM**: Pedido sugerido por IA usa orquestador genérico (no optimizado)
- 🟢 **LOW**: No valida formato de email/teléfono

**Riesgo**: 🟢 **BAJO** — Funcional para gestión básica

**Dependencias**: Inventario (productos)

---

### 1.10 CAJA (CASH REGISTER)

**Estado**: ✅ **FUNCIONAL CON DEUDA**

**Archivos principales**:
- Backend: `CashRegisterController.cs`, `CashRegisterService.cs`, `CashRegisterRepository.cs`
- Frontend: `CashRegisterBar.jsx`, `OpenCashRegisterModal.jsx`, `CloseCashRegisterModal.jsx`, `CashMovementModal.jsx`, `CashRegisterHistory.jsx`, `ZReportPrint.jsx`

**Endpoints**:
```
POST /api/v1/sales/cash-register/open        → Abrir caja
GET  /api/v1/sales/cash-register/active      → Caja activa del usuario
POST /api/v1/sales/cash-register/close       → Cerrar caja
POST /api/v1/sales/cash-register/movement    → Entrada/salida manual
GET  /api/v1/sales/cash-register/{id}/summary → Reporte Z
GET  /api/v1/sales/cash-register/history     → Historial de turnos
```

**Tablas**:
- `sales.cash_registers` (company_id, branch_id, opened_by, status, opening_amount, closing_amount, expected_cash, difference, totales por método de pago)
- `sales.cash_movements` (cash_register_id, type: in/out, amount, reason)

**Tests**:
- ✅ Backend: Lógica de cierre y arqueo verificada
- ⚠️ Falta: E2E de flujo completo

**Deuda conocida**:
- 🟠 **HIGH**: Frontend NO implementado (modales existen pero no integrados en flujo)
- 🟡 **MEDIUM**: `InvoicePanel` no valida caja abierta antes de facturar
- 🟡 **MEDIUM**: Reporte Z sin formato de impresión

**Riesgo**: 🟠 **MEDIO** — Backend listo, frontend incompleto

**Dependencias**: Ventas (facturación), Usuarios (opened_by)

---

### 1.11 CRÉDITOS

**Estado**: ✅ **TERMINADO**

**Archivos principales**:
- Backend: `CreditController.cs`, `CreditService.cs`, `CreditRepository.cs`
- Frontend: `CreditsPanel.jsx` (integrado en SalesPage)

**Endpoints**:
```
GET    /api/v1/credits                       → Listar créditos activos
POST   /api/v1/credits                       → Crear crédito (al facturar mesa)
POST   /api/v1/credits/{id}/payment          → Registrar abono
GET    /api/v1/credits/{id}/payments         → Historial de pagos
```

**Tablas**:
- `sales.credits` (order_id, customer_name, customer_phone, total_amount, paid_amount, balance, status: active/paid/cancelled)
- `sales.credit_payments` (credit_id, amount, payment_method, reference, notes)

**Tests**:
- ✅ Backend: Atomicidad crédito-refund verificada en migración `017_credit_refund_atomicity.sql`
- ⚠️ Falta: E2E

**Deuda conocida**:
- 🟡 **MEDIUM**: No valida límite de crédito por cliente
- 🟡 **MEDIUM**: No genera recordatorios de pago
- 🟢 **LOW**: UI de créditos básica (sin filtros avanzados)

**Riesgo**: 🟢 **BAJO** — Funcional y estable

**Dependencias**: Ventas (orders)

---

### 1.12 DEVOLUCIONES

**Estado**: ✅ **FUNCIONAL CON DEUDA**

**Archivos principales**:
- Backend: `RefundController.cs`, `RefundService.cs`, `RefundRepository.cs`
- Frontend: `RefundModal.jsx` (integrado en SalesPage)

**Endpoints**:
```
POST /api/v1/sales/refunds                   → Crear devolución (total/parcial)
GET  /api/v1/sales/refunds                   → Listar devoluciones
GET  /api/v1/sales/refunds/{id}              → Detalle de devolución
GET  /api/v1/sales/orders/{orderId}/refunds  → Devoluciones de una orden
```

**Tablas**:
- `sales.refunds` (order_id, refund_type: full/partial, refund_amount, reason, status, approved_by)
- `sales.refund_items` (refund_id, order_item_id, quantity, unit_price, subtotal)

**Tests**:
- ✅ `RefundModal.test.jsx`
- ⚠️ Falta: Tests backend

**Deuda conocida**:
- 🟡 **MEDIUM**: Reversión de stock no implementada (solo registra devolución)
- 🟡 **MEDIUM**: No ajusta caja cerrada (devolución va a caja activa)
- 🟡 **MEDIUM**: Aprobación de devoluciones no implementada (siempre `completed`)

**Riesgo**: 🟡 **MEDIO** — Funcional pero incompleto

**Dependencias**: Ventas (orders), Inventario (reversión stock pendiente)

---

### 1.13 DELIVERY

**Estado**: ✅ **TERMINADO**

**Archivos principales**:
- Backend: `DeliveryController.cs`, `DeliveryService.cs`, `DeliveryRepository.cs`
- Frontend: `DeliveryOrdersPage.jsx`, `DeliveryBoard.jsx`, `DeliveryOrderCard.jsx`, `CreateDeliveryOrderPanel.jsx`, `DeliveryOrderDetailsPanel.jsx`, `StatusActionModal.jsx`

**Endpoints**:
```
GET    /api/v1/delivery/orders               → Listar pedidos delivery
POST   /api/v1/delivery/orders               → Crear pedido
PUT    /api/v1/delivery/orders/{id}          → Actualizar pedido
POST   /api/v1/delivery/orders/{id}/status   → Cambiar estado (+ historial)
DELETE /api/v1/delivery/orders/{id}          → Cancelar pedido
GET    /api/v1/delivery/orders/{id}/history  → Historial de estados
```

**Tablas**:
- `delivery.orders` (customer_name, customer_phone, delivery_address, status: new/confirmed/preparing/dispatched/delivered/cancelled, total_amount)
- `delivery.order_items` (order_id, product_id, quantity, unit_price)
- `delivery.order_status_history` (order_id, status, changed_by, notes)

**Tests**:
- ⚠️ Falta: Tests unitarios
- ⚠️ Falta: E2E

**Deuda conocida**:
- 🟡 **MEDIUM**: No integra con plataformas externas (Rappi, Uber Eats)
- 🟡 **MEDIUM**: No calcula costo de envío
- 🟡 **MEDIUM**: Origen del pedido hardcodeado a "WhatsApp" (no real)
- 🟢 **LOW**: No valida dirección de entrega

**Riesgo**: 🟢 **BAJO** — Funcional para gestión manual

**Dependencias**: Inventario (productos)

---

### 1.14 CLIENTES

**Estado**: ❌ **NO IMPLEMENTADO**

**Archivos principales**: Ninguno

**Endpoints**: Ninguno

**Tablas**: Ninguna (clientes solo como campos en `sales.credits` y `delivery.orders`)

**Tests**: N/A

**Deuda conocida**:
- 🔴 **BLOCKER**: No existe módulo de clientes
- 🔴 **BLOCKER**: Datos de cliente duplicados en créditos y delivery
- 🔴 **BLOCKER**: No hay historial de compras por cliente

**Riesgo**: 🔴 **ALTO** — Funcionalidad crítica ausente

**Dependencias**: Ninguna

---

### 1.15 FINANZAS

**Estado**: ✅ **TERMINADO**

**Archivos principales**:
- Backend: `FinanceController.cs`, `FinanceService.cs`, `FinanceRepository.cs`
- Frontend: `FinancePage.jsx`, `FinancialEntryFormModal.jsx`, `FinancialCategoryModal.jsx`, `FinancialSummaryCards.jsx`, `MonthInitPanelModal.jsx`

**Endpoints**:
```
GET    /api/v1/finance/categories            → Categorías financieras
POST   /api/v1/finance/categories            → Crear categoría
PUT    /api/v1/finance/categories/{id}       → Actualizar categoría
DELETE /api/v1/finance/categories/{id}       → Eliminar categoría
GET    /api/v1/finance/entries               → Movimientos financieros
POST   /api/v1/finance/entries               → Crear movimiento
PUT    /api/v1/finance/entries/{id}          → Actualizar movimiento
DELETE /api/v1/finance/entries/{id}          → Eliminar movimiento
GET    /api/v1/finance/summary               → Resumen financiero
POST   /api/v1/finance/month/init            → Inicializar mes desde templates
```

**Tablas**:
- `finance.categories` (name, type: income/expense, is_recurring, frequency, auto_include)
- `finance.entries` (category_id, amount, entry_date, status: pending/posted/skipped, notes)
- `finance.recurring_templates` (category_id, amount, frequency, day_of_month)

**Tests**:
- ✅ Backend: Lógica de resumen verificada
- ⚠️ Falta: E2E

**Deuda conocida**:
- 🟡 **MEDIUM**: Inicialización mensual manual (no automática)
- 🟡 **MEDIUM**: No integra con contabilidad externa
- 🟢 **LOW**: Resumen sin gráficas

**Riesgo**: 🟢 **BAJO** — Funcional y estable

**Dependencias**: Ventas (ventas facturadas integradas automáticamente)

---

### 1.16 REPORTES

**Estado**: ⚠️ **PARCIAL**

**Archivos principales**:
- Backend: Endpoints dispersos en `InventoryController`, `SalesController`, `FinanceController`
- Frontend: Componentes de resumen en cada módulo

**Endpoints**:
```
GET /api/v1/inventory/reports/profit         → Reporte de ganancias por producto
GET /api/v1/finance/summary                  → Resumen financiero
GET /api/v1/sales/cash-register/{id}/summary → Reporte Z
```

**Tablas**: Ninguna (reportes calculados en runtime)

**Tests**: ⚠️ Falta

**Deuda conocida**:
- 🟠 **HIGH**: No existe módulo centralizado de reportes
- 🟠 **HIGH**: No hay exportación a PDF/Excel
- 🟡 **MEDIUM**: Reportes sin filtros avanzados (solo fecha)
- 🟡 **MEDIUM**: No hay dashboard ejecutivo

**Riesgo**: 🟠 **MEDIO** — Funcionalidad dispersa y limitada

**Dependencias**: Todos los módulos transaccionales

---

### 1.17 BRANDING

**Estado**: ✅ **FUNCIONAL CON DEUDA**

**Archivos principales**:
- Backend: `CompanyController.cs`, `MediaController.cs`, `SupabaseFileStorage.cs`
- Frontend: `BrandingForm.jsx`, `ThemeSelector.jsx`

**Endpoints**:
```
GET  /api/v1/company/settings                → Incluye logo_url, theme_preference
POST /api/v1/company/settings/logo           → Upload logo
GET  /api/v1/media/branding/{filename}       → Servir logo legacy
```

**Tablas**:
- `core.companies` (logo_url, display_name, theme_preference, primary_color)

**Tests**:
- ✅ `CompanyControllerTests.cs`
- ⚠️ Falta: Tests de upload

**Deuda conocida**:
- 🟡 **MEDIUM**: Logos legacy en `wwwroot/uploads/branding/` sin migración a Supabase
- 🟡 **MEDIUM**: Archivo faltante (company 16) sin fallback a default
- 🟢 **LOW**: 6 temas hardcodeados (no personalizables)

**Riesgo**: 🟡 **MEDIO** — Funcional pero con archivos huérfanos

**Dependencias**: Storage (Supabase)

---

### 1.18 PWA

**Estado**: ✅ **FUNCIONAL CON DEUDA**

**Archivos principales**:
- Backend: `PwaController.cs`
- Frontend: `vite.config.js` (vite-plugin-pwa), `manifest.webmanifest` (generado dinámicamente)

**Endpoints**:
```
GET /api/v1/pwa/manifest.webmanifest?tenantId={id} → Manifest dinámico
GET /api/v1/pwa/icon/{tenantId}/{size}.png         → Iconos redimensionados
```

**Tablas**: Ninguna (usa `core.companies.logo_url`)

**Tests**:
- ✅ `PwaControllerTests.cs`

**Deuda conocida**:
- 🟡 **MEDIUM**: Iconos fallan si logo no existe (404 en lugar de fallback)
- 🟡 **MEDIUM**: Service Worker con `NetworkOnly` (no offline real)
- 🟢 **LOW**: Precaching solo de assets estáticos

**Riesgo**: 🟡 **MEDIO** — Instalable pero sin funcionalidad offline

**Dependencias**: Branding (logo_url)

---

### 1.19 STORAGE

**Estado**: ✅ **FUNCIONAL CON DEUDA**

**Archivos principales**:
- Backend: `SupabaseFileStorage.cs`, `MediaController.cs`
- Config: `SupabaseStorage` en `appsettings.json`

**Endpoints**:
```
POST /api/v1/media/upload                    → Upload genérico
GET  /api/v1/media/branding/{filename}       → Servir archivo legacy
```

**Tablas**: Ninguna (Supabase Storage externo)

**Tests**:
- ✅ `SupabaseFileStorageTests.cs`

**Deuda conocida**:
- 🟡 **MEDIUM**: Archivos legacy en `wwwroot/` sin migración
- 🟡 **MEDIUM**: No hay limpieza de archivos huérfanos
- 🟢 **LOW**: Validación de tipo de archivo básica (solo extensión)

**Riesgo**: 🟢 **BAJO** — Funcional con deuda de migración

**Dependencias**: Supabase (servicio externo)

---

### 1.20 IA (ASISTENTE CONVERSACIONAL)

**Estado**: ✅ **TERMINADO**

**Archivos principales**:
- Backend: `AiController.cs`, `OrchestratorService.cs`, `OpenAiService.cs`
- Frontend: `AiAssistantPage.jsx`, `AIChat.jsx`

**Endpoints**:
```
POST /api/v1/ai/chat                         → Procesar mensaje (orquestador genérico)
POST /api/v1/inventory/ai/process            → Procesar entrada inventario
POST /api/v1/inventory/ai/confirm/{id}       → Confirmar acción IA
GET  /api/v1/inventory/ai/sessions           → Historial de sesiones
```

**Tablas**:
- `inventory.ai_sessions` (company_id, user_id, session_id, started_at, ended_at)
- `inventory.ai_interactions` (session_id, user_input, ai_response, action, status, created_at)

**Tests**:
- ✅ Backend: Lógica de orquestador verificada
- ⚠️ Falta: E2E

**Deuda conocida**:
- 🟡 **MEDIUM**: Orquestador genérico poco optimizado para casos específicos
- 🟡 **MEDIUM**: No valida límites de uso de API OpenAI por tenant
- 🟡 **MEDIUM**: Historial de sesión sin límite (puede crecer indefinidamente)
- 🟢 **LOW**: No soporta imágenes en chat

**Riesgo**: 🟢 **BAJO** — Funcional y robusto

**Dependencias**: OpenAI API (servicio externo), Inventario

---

### 1.21 WHATSAPP

**Estado**: ❌ **NO IMPLEMENTADO**

**Archivos principales**:
- Frontend: `ContactActions.jsx` (genera URL `wa.me` pero no integra API)
- Backend: Menciones en `OrchestratorService.cs` (preparado pero no implementado)

**Endpoints**: Ninguno

**Tablas**: Ninguna

**Tests**: N/A

**Deuda conocida**:
- 🔴 **BLOCKER**: No existe integración real con WhatsApp Business API
- 🔴 **BLOCKER**: No hay recepción de mensajes
- 🔴 **BLOCKER**: No hay flujo de pedidos por WhatsApp
- 🔴 **BLOCKER**: Catálogo no se envía por WhatsApp

**Riesgo**: 🔴 **CRÍTICO** — Funcionalidad clave ausente para V2

**Dependencias**: Delivery (pedidos), Catálogo (productos)

---

### 1.22 FACTURACIÓN ELECTRÓNICA

**Estado**: ❌ **NO IMPLEMENTADO**

**Archivos principales**: Ninguno

**Endpoints**: Ninguno

**Tablas**: Ninguna

**Tests**: N/A

**Deuda conocida**:
- 🔴 **BLOCKER**: No existe integración con proveedor de facturación electrónica
- 🔴 **BLOCKER**: No hay emisión de facturas fiscales
- 🔴 **BLOCKER**: No hay generación de XML/PDF DIAN
- 🔴 **BLOCKER**: No hay notas crédito

**Riesgo**: 🔴 **CRÍTICO** — Funcionalidad legal obligatoria ausente

**Dependencias**: Ventas (orders), Company (NIT, razón social)

---

### 1.23 IMPRESIÓN

**Estado**: ⚠️ **PARCIAL**

**Archivos principales**:
- Backend: Ninguno (solo DTOs preparados en `fase3-execution-guide.md`)
- Frontend: `ReceiptPreview.jsx`, `KitchenTicket.jsx`, `ZReportPrint.jsx` (componentes básicos)

**Endpoints**: Ninguno (preparados pero no implementados)

**Tablas**: Ninguna

**Tests**:
- ✅ `ReceiptPreview.test.jsx`
- ⚠️ Falta: Tests backend

**Deuda conocida**:
- 🟠 **HIGH**: Componentes de impresión existen pero no integrados en flujo
- 🟠 **HIGH**: No hay endpoints backend para datos de recibo/comanda
- 🟠 **HIGH**: Impresión usa `window.print()` (no impresoras térmicas reales)
- 🔴 **BLOCKER**: No existe Print Agent para Windows

**Riesgo**: 🔴 **ALTO** — Funcionalidad crítica para POS ausente

**Dependencias**: Ventas (orders), Caja (reporte Z)

---

### 1.24 CAJÓN

**Estado**: ❌ **NO IMPLEMENTADO**

**Archivos principales**: Ninguno

**Endpoints**: Ninguno

**Tablas**: Ninguna

**Tests**: N/A

**Deuda conocida**:
- 🔴 **BLOCKER**: No existe integración con cajón de dinero
- 🔴 **BLOCKER**: No hay comando ESC/POS para abrir cajón
- 🔴 **BLOCKER**: Apertura de cajón no vinculada a facturación

**Riesgo**: 🔴 **ALTO** — Funcionalidad crítica para POS ausente

**Dependencias**: Impresión (Print Agent), Ventas (facturación)

---

### 1.25 BÁSCULA

**Estado**: ⚠️ **EXPERIMENTAL**

**Archivos principales**:
- Frontend: `ScaleIndicator.jsx`, `WeightInputModal.jsx` (POS-Deli)
- Backend: Ninguno

**Endpoints**: Ninguno

**Tablas**: Ninguna

**Tests**: N/A

**Deuda conocida**:
- 🟠 **HIGH**: Báscula simulada (no integración real con hardware)
- 🟠 **HIGH**: `ScaleIndicator` solo muestra estado mock
- 🟠 **HIGH**: No hay protocolo de comunicación con báscula serial/USB

**Riesgo**: 🟠 **MEDIO** — Funcionalidad preparada pero no real

**Dependencias**: POS-Deli (productos pesables)

---

### 1.26 CONFIGURACIÓN DE DISPOSITIVOS

**Estado**: ⚠️ **PARCIAL**

**Archivos principales**:
- Frontend: `DevicesSettings.jsx`, `PrinterSettings.jsx` (Settings)
- Backend: Ninguno

**Endpoints**: Ninguno

**Tablas**: Ninguna

**Tests**:
- ✅ `DevicesSettings.test.jsx`
- ✅ `PrinterSettings.test.jsx`

**Deuda conocida**:
- 🟠 **HIGH**: UI de configuración existe pero no persiste datos
- 🟠 **HIGH**: No hay tabla `device_configurations` en DB
- 🟠 **HIGH**: No hay endpoints backend para guardar configuración

**Riesgo**: 🟠 **MEDIO** — UI preparada pero no funcional

**Dependencias**: Impresión, Báscula

---

### 1.27 DOCUMENTAL

**Estado**: ✅ **FUNCIONAL CON DEUDA**

**Archivos principales**:
- `README.md`, `docs/architecture.md`, `docs/STYLE_GUIDE.md`, `docs/pending-pos-gaps.md`, `PENDING.md`

**Deuda conocida**:
- 🟡 **MEDIUM**: Documentación desactualizada (menciona features no implementadas como completas)
- 🟡 **MEDIUM**: `docs/pending-pos-gaps.md` marca P1/P2 como completos cuando solo backend está listo
- 🟡 **MEDIUM**: `PENDING.md` mezcla backlog con historial implementado
- 🟢 **LOW**: Falta documentación de API (Swagger incompleto)

**Riesgo**: 🟡 **MEDIO** — Documentación confusa para nuevos desarrolladores

**Dependencias**: Ninguna

---

### 1.28 EMPLEADOS

**Estado**: ❌ **NO IMPLEMENTADO**

**Archivos principales**: Ninguno (solo `core.users` que mezcla empleados con usuarios del sistema)

**Endpoints**: Ninguno específico

**Tablas**: `core.users` (no diferencia empleados de usuarios administrativos)

**Tests**: N/A

**Deuda conocida**:
- 🟠 **HIGH**: No existe módulo de empleados separado
- 🟠 **HIGH**: No hay gestión de horarios, turnos, asistencia
- 🟠 **HIGH**: No hay cálculo de nómina
- 🟡 **MEDIUM**: `core.users` mezcla roles operativos con administrativos

**Riesgo**: 🟡 **MEDIO** — Funcionalidad deseable pero no crítica para B1

**Dependencias**: Usuarios, Caja (turnos)

---

## RESUMEN DE AUDITORÍA

### Por Estado

| Estado | Cantidad | Módulos |
|--------|----------|---------|
| ✅ **TERMINADO** | 10 | Auth, Tenants, Usuarios/Roles, Restaurante/Mesas, Inventario, Recetas, Compras, Proveedores, Delivery, Finanzas, IA |
| ✅ **FUNCIONAL CON DEUDA** | 6 | POS-Deli, Caja, Devoluciones, Branding, PWA, Storage, Documental |
| ⚠️ **PARCIAL** | 4 | Reportes, Impresión, Configuración Dispositivos, Empleados |
| ⚠️ **EXPERIMENTAL** | 1 | Báscula |
| ❌ **NO IMPLEMENTADO** | 4 | Clientes, WhatsApp, Facturación Electrónica, Cajón |

### Por Riesgo

| Riesgo | Cantidad | Módulos |
|--------|----------|---------|
| 🔴 **CRÍTICO** | 2 | WhatsApp, Facturación Electrónica |
| 🔴 **ALTO** | 3 | Auth (validación), Clientes, Impresión, Cajón |
| 🟠 **MEDIO** | 9 | Usuarios (políticas), Caja (frontend), Recetas, Compras, Devoluciones, Reportes, Báscula, Dispositivos, Empleados |
| 🟡 **MEDIO** | 7 | Tenants (branding), Proveedores, Créditos, Branding, PWA, Storage, IA, Documental |
| 🟢 **BAJO** | 6 | POS-Deli, Restaurante, Inventario, Delivery, Finanzas |

---

## PARTE 2 — CIERRE DE WALOS B1

### B1 INCLUDED (Funcionalidades que entran en B1)

**Criterios de inclusión**:
- ✅ Funciona end-to-end
- ✅ Tiene persistencia correcta
- ✅ Tiene autorización adecuada
- ✅ Tiene tests razonables
- ✅ No depende de features incompletas

**Lista B1 INCLUDED**:

1. ✅ **Autenticación** (con fix de validación aplicado)
2. ✅ **Multi-tenant** (companies, branches, tenant context)
3. ✅ **Usuarios y Roles** (con fix de políticas aplicado)
4. ✅ **Inventario** (CRUD, stock, movimientos, alertas, costo promedio)
5. ✅ **Recetas** (BOM de productos preparados)
6. ✅ **Restaurante / Mesas** (POS completo, facturación, descuentos)
7. ✅ **POS-Deli** (ventas directas, idempotencia)
8. ✅ **Créditos** (pago parcial, abonos)
9. ✅ **Proveedores** (CRUD, contacto, productos asociados)
10. ✅ **Compras** (órdenes de compra, recepción)
11. ✅ **Delivery** (pedidos, estados, historial)
12. ✅ **Finanzas** (gastos, ingresos, categorías, resumen)
13. ✅ **Asistente IA** (conversacional, creación de productos, stock)
14. ✅ **Branding** (logo, temas, configuración)
15. ✅ **PWA** (manifest, iconos, service worker básico)
16. ✅ **Storage** (Supabase, upload de archivos)
17. ✅ **Dashboard** (métricas básicas)

**Total B1 INCLUDED**: 17 módulos

---

### B1 EXCLUDED / MOVED TO V2

**Criterios de exclusión**:
- ❌ Backend listo pero frontend incompleto
- ❌ Funcionalidad parcial sin flujo completo
- ❌ Dependencias críticas ausentes
- ❌ Riesgo alto de regresión

**Lista B1 EXCLUDED**:

1. ❌ **Caja (Cash Register)** — Backend completo, frontend NO integrado en flujo
2. ❌ **Devoluciones** — No revierte stock, no ajusta caja cerrada
3. ❌ **Reportes** — Funcionalidad dispersa, sin exportación
4. ❌ **Impresión** — Componentes básicos, sin endpoints backend, sin Print Agent
5. ❌ **Cajón** — No implementado
6. ❌ **Báscula** — Solo simulación, no hardware real
7. ❌ **Configuración Dispositivos** — UI sin persistencia
8. ❌ **Clientes** — No existe módulo
9. ❌ **WhatsApp** — No implementado
10. ❌ **Facturación Electrónica** — No implementado
11. ❌ **Empleados** — No existe módulo separado

**Total B1 EXCLUDED**: 11 módulos

---

## PARTE 3 — DEUDA TÉCNICA B1

### 3.1 BLOCKERS (Impiden uso en producción)

| ID | Categoría | Descripción | Archivos Afectados | Esfuerzo |
|----|-----------|-------------|-------------------|----------|
| **B1-BLK-01** | Seguridad | FluentValidation sin validator para `LoginRequest` → 422 en producción | `AuthController.cs`, `Program.cs` | 30 min |
| **B1-BLK-02** | Seguridad | Deserialización JSON case-sensitive (frontend `username` vs backend `Username`) | `Program.cs` | 5 min |
| **B1-BLK-03** | Funcional | Rol `dev` bloqueado en Finance, Settings, Users por políticas | `WalosAuthorizationExtensions.cs` | 15 min |
| **B1-BLK-04** | Funcional | Logo faltante (company 16) sin fallback → 404 en manifest PWA | `PwaController.cs` | 1 hora |

**Total Blockers**: 4  
**Esfuerzo total**: ~2 horas

---

### 3.2 HIGH PRIORITY (Afectan funcionalidad crítica)

| ID | Categoría | Descripción | Archivos Afectados | Esfuerzo |
|----|-----------|-------------|-------------------|----------|
| **B1-HIGH-01** | Funcional | Caja: Frontend NO integrado en flujo de facturación | `InvoicePanel.jsx`, `SalesPage.jsx` | 4 horas |
| **B1-HIGH-02** | Funcional | Devoluciones: No revierte stock | `RefundService.cs` | 3 horas |
| **B1-HIGH-03** | Funcional | Reportes: No existe módulo centralizado | Crear `ReportsController.cs` | 8 horas |
| **B1-HIGH-04** | Funcional | Impresión: Sin endpoints backend | Crear endpoints en `SalesController.cs` | 4 horas |
| **B1-HIGH-05** | Seguridad | Permisos JSONB sin validación de estructura | `UsersService.cs` | 2 horas |
| **B1-HIGH-06** | UX | `InventoryController` sobrecargado (20+ endpoints) | Refactor a `ProductsController`, `StockController` | 6 horas |
| **B1-HIGH-07** | Infraestructura | Archivos legacy en `wwwroot/uploads/` sin migración | Script de migración a Supabase | 4 horas |

**Total High**: 7  
**Esfuerzo total**: ~31 horas

---

### 3.3 MEDIUM PRIORITY (Mejoras importantes)

| ID | Categoría | Descripción | Esfuerzo |
|----|-----------|-------------|----------|
| **B1-MED-01** | Funcional | Stock comprometido calculado en runtime (no materializado) | 4 horas |
| **B1-MED-02** | Funcional | Recetas: No valida ciclos | 2 horas |
| **B1-MED-03** | Funcional | Compras: Recepción parcial no implementada | 3 horas |
| **B1-MED-04** | Funcional | Créditos: No valida límite por cliente | 2 horas |
| **B1-MED-05** | Funcional | Delivery: No calcula costo de envío | 2 horas |
| **B1-MED-06** | Funcional | Finanzas: Inicialización mensual manual | 3 horas |
| **B1-MED-07** | UX | División de cuenta solo informativa | 4 horas |
| **B1-MED-08** | UX | Importación Excel sin validación de duplicados | 2 horas |
| **B1-MED-09** | Seguridad | IA: No valida límites de uso de API OpenAI | 3 horas |
| **B1-MED-10** | Datos | Costo promedio ponderado sin auditoría de cambios | 2 horas |
| **B1-MED-11** | Documental | Documentación desactualizada | 4 horas |
| **B1-MED-12** | Testing | Falta tests E2E para suppliers, purchase orders, delivery | 6 horas |

**Total Medium**: 12  
**Esfuerzo total**: ~37 horas

---

### 3.4 LOW PRIORITY (Mejoras deseables)

| ID | Categoría | Descripción | Esfuerzo |
|----|-----------|-------------|----------|
| **B1-LOW-01** | UX | Mesas arrastrables solo en desktop | 2 horas |
| **B1-LOW-02** | UX | Recetas: UI básica sin drag & drop | 3 horas |
| **B1-LOW-03** | UX | Créditos: UI sin filtros avanzados | 2 horas |
| **B1-LOW-04** | UX | Finanzas: Resumen sin gráficas | 4 horas |
| **B1-LOW-05** | Funcional | Proveedores: No valida formato email/teléfono | 1 hora |
| **B1-LOW-06** | Funcional | Delivery: No valida dirección | 2 horas |
| **B1-LOW-07** | Funcional | IA: No soporta imágenes en chat | 6 horas |
| **B1-LOW-08** | Infraestructura | PWA: Service Worker con NetworkOnly (no offline real) | 8 horas |
| **B1-LOW-09** | Infraestructura | Storage: No limpieza de archivos huérfanos | 3 horas |
| **B1-LOW-10** | Documental | Swagger incompleto | 4 horas |

**Total Low**: 10  
**Esfuerzo total**: ~35 horas

---

### RESUMEN DE DEUDA TÉCNICA

| Prioridad | Cantidad | Esfuerzo Total |
|-----------|----------|----------------|
| 🔴 **BLOCKER** | 4 | ~2 horas |
| 🟠 **HIGH** | 7 | ~31 horas |
| 🟡 **MEDIUM** | 12 | ~37 horas |
| 🟢 **LOW** | 10 | ~35 horas |
| **TOTAL** | **33** | **~105 horas** |

---

## RECOMENDACIÓN FINAL

### B1 CLOSURE PROPOSAL

**Walos B1** debe incluir **SOLO** los 17 módulos funcionales y estables, **EXCLUYENDO** los 11 módulos incompletos.

**Antes de cerrar B1, DEBEN corregirse los 4 BLOCKERS**:
1. ✅ Agregar validator para `LoginRequest`
2. ✅ Configurar `PropertyNameCaseInsensitive = true`
3. ✅ Agregar rol `dev` a todas las políticas
4. ✅ Implementar fallback a logo default en `PwaController`

**Esfuerzo para cerrar B1**: ~2 horas

**Fecha propuesta de cierre B1**: 2026-09-15 (mañana)

---

**FIN DE AUDITORÍA B1**
