# PENDING - Actividades Walos

> Cada actividad tiene contexto suficiente para que un agente de IA la ejecute de forma autonoma.
>
> **Estado**: `[ ]` pendiente | `[~]` en progreso | `[x]` completado
>
> **Prioridad**: `P0` critico | `P1` alta | `P2` media | `P3` baja
>
> **OBLIGATORIO**: Antes de implementar cualquier tarea, leer `docs/STYLE_GUIDE.md` y seguir todos los patrones definidos sin excepcion.

---

## Resumen de Progreso

| # | Tarea | Estado | Prioridad |
|---|-------|--------|-----------|
| 10 | Branding y Consistencia Visual | `[x]` Completado | P1 |
| 1 | Sidebar Colapsable y Responsivo | `[x]` Completado | P0 |
| 2 | Fix Tabla Inventario | `[x]` Completado | P0 |
| 3 | CRUD Completo de Productos | `[x]` Completado | P0 |
| 4 | Imagen de Producto | `[x]` Completado | P1 |
| 5 | Stat Cards como Filtros | `[x]` Completado | P1 |
| 6 | Modulo Ventas (mesas, pedidos, facturacion) | `[x]` Completado | P0 |
| 7 | Configuracion (logo, nombre, 6 temas) | `[x]` Completado | P1 |
| 8 | Alertas (campana, vista detallada, acciones) | `[x]` Completado | P2 |
| 11 | Stock Comprometido (ventas vs inventario) | `[x]` Completado | P1 |
| 12 | Finanzas Operativas (gastos, ingresos, control mensual) | `[x]` Completado | P1 |
| 13 | Configuracion (reglas operativas y descuentos) | `[x]` Completado | P1 |
| 9 | Proveedores (CRUD, WhatsApp/email, IA pedidos) | `[ ]` Pendiente | P2 |
| 14 | Multi-tenant SaaS por company_id | `[~]` En progreso | P0 |
| 15 | Pedidos y Domicilios (plataformas + IA + estados) | `[ ]` Pendiente | P1 |

---

## Tareas Completadas (resumen)

<details>
<summary>Click para expandir historial de tareas completadas</summary>

### #10. Branding y Consistencia Visual `[x]`
- Lucide React en todo el sidebar y la app (cero emojis)
- "WALOS" en pie de sidebar
- Formato de moneda consistente via `formatCurrency()`
- Botones con estilo uniforme (ver `docs/STYLE_GUIDE.md`)

### #1. Sidebar Colapsable y Responsivo `[x]`
- Toggle colapsar/expandir en desktop con persistencia en localStorage
- Mobile: hamburguesa + overlay animado
- Iconos Lucide en todas las secciones del menu
- Footer con info de usuario (avatar en modo colapsado)

### #2. Fix Tabla Inventario `[x]`
- Query SQL hace JOIN con categories, selecciona cost_price, sale_price, image_url
- Entidad Stock.cs tiene todas las propiedades
- Frontend muestra: Producto, SKU, Categoria, Cantidad, Unidad, Costo, Precio Venta, Estado

### #3. CRUD Completo de Productos `[x]`
- Crear/editar/eliminar productos (soft delete)
- Modal reutilizable `ProductFormModal.jsx`
- Calculo margen <-> precio venta en tiempo real
- Categorias y unidades como selects desde API
- Agregar stock rapido con costo promedio ponderado

### #4. Imagen de Producto `[x]`
- Upload `POST /api/v1/inventory/products/:id/image` (multipart, max 2MB, JPG/PNG/WebP)
- Guardado en `wwwroot/uploads/products/`
- Preview en formulario, thumbnail 40x40 en tabla
- Drag & drop + captura de camara movil

### #5. Stat Cards como Filtros `[x]`
- Cards clickeables con toggle (activa/inactiva)
- Filtran tabla por: todos, stock bajo, sin stock
- Estilo activo con ring-2 y shadow
- Fix: "Stock Bajo" no incluye items "Sin Stock"

### #6. Modulo Ventas `[x]`
- **DB**: `sales.tables`, `sales.orders`, `sales.order_items` (3 scripts SQL)
- **Backend**: `SalesController` con endpoints crear mesa, facturar, cancelar, actualizar items, agregar items
- **Frontend**: SalesPage, AddTablePanel, TableCard (arrastrable desktop, grid mobile), InvoicePanel, ProductGrid
- Cards con +/- inline por item y boton agregar productos
- Division de cuenta informativa
- Auto-arrange (boton Ordenar)
- Responsive: mobile grid flow sin drag, desktop absoluto con drag
- Scroll solo en panel de mesas, no en modulo completo

### #7. Configuracion `[x]`
- **Archivos**: `SettingsPage.jsx`, `BrandingForm.jsx`, `ThemeSelector.jsx`, `SettingsSectionNav.jsx`
- Upload de logo + nombre de negocio
- 6 temas: claro, oscuro, grises, neon, pink, morado
- CSS variables + `data-theme` en html
- Persistencia en localStorage y DB

### #8. Alertas `[x]`
- **Archivo**: `AlertsPage.jsx` (306 lineas) con ruta `/alerts`
- Alertas por severidad (critica, alta, media)
- Acciones rapidas: agregar stock, ir a inventario
- Badge numerico en campana del header
- Integracion con `AddStockModal` del inventario

### #11. Stock Comprometido `[x]`
- CTE `committed` en queries de inventario calcula cantidad reservada por mesas abiertas
- Campos en Stock: `ReservedQuantity`, `AvailableQuantity`
- `StockStatus` ahora usa stock disponible (no total) para determinar "low" u "out"
- Alertas oportunas basadas en disponible real

### #12. Finanzas Operativas `[x]`
- **Backend**: `FinanceController`, `FinanceRepository` con CRUD de entries + categories + summary
- **Frontend**: `FinancePage.jsx`, `FinancialEntryFormModal`, `FinancialCategoryModal`, `FinancialEntryTable`, `FinancialSummaryCards`
- Servicio: `financeService.js`
- Ventas facturadas se leen automaticamente de `sales.orders`
- Control mensual con selector de periodo

### #13. Descuentos Operativos `[x]`
- **Backend**: `GET/PUT /api/v1/company/settings/operations`
- **Frontend**: `DiscountSettings.jsx` dentro de Configuracion
- Parametros: descuento habilitado, % maximo, monto maximo, umbral de override
- `InvoicePanel.jsx` aplica descuento fijo/porcentual con validacion

</details>

---

## Tareas Pendientes (detalladas)

---

## 9. Proveedores - Modulo Completo `P2`

**Estado**: `[ ]`

> **Estado actual del codigo**: Solo existe `frontend/src/modules/suppliers/suppliers.jsx` (archivo vacio). No hay controller, repository, service, entidades ni tablas SQL. La ruta `/suppliers` en `App.jsx` es un placeholder. Todo debe crearse desde cero.

### Objetivo
Modulo de gestion de proveedores con CRUD, contacto directo (WhatsApp/email) y apoyo de IA para sugerir pedidos basados en stock bajo.

### 9a. Base de Datos

**Scripts SQL a crear** (en `backend-dotnet/sql/`):

**`500_create_table_suppliers.sql`**:
```sql
-- Tabla: suppliers.suppliers
-- company_id, branch_id, name, contact_name, phone, email, address, notes,
-- is_active, created_by, created_at, updated_at, deleted_at
-- Indices: (company_id, branch_id), (company_id, name)
```

**`501_create_table_supplier_products.sql`**:
```sql
-- Tabla: suppliers.supplier_products (relacion proveedor-producto)
-- supplier_id, product_id, supplier_sku, unit_cost, lead_time_days, notes,
-- created_at
-- FK a suppliers.suppliers y inventory.products
-- Indice unico: (supplier_id, product_id)
```

> El esquema `suppliers` ya existe (ver `002_create_schemas.sql`).

### 9b. Backend

**Entidades a crear** (en `Domain/Entities/`):
- `Supplier.cs` — hereda `BaseEntity`, campos: `BranchId`, `Name`, `ContactName`, `Phone`, `Email`, `Address`, `Notes`, `IsActive`, `CreatedBy`
- `SupplierProduct.cs` — campos: `Id`, `SupplierId`, `ProductId`, `SupplierSku`, `UnitCost`, `LeadTimeDays`, `Notes`, `ProductName` (navigation)

**Interface** (en `Domain/Interfaces/`):
- `ISuppliersRepository.cs`:
  - `GetAllAsync(companyId, branchId)` — listar proveedores activos
  - `GetByIdAsync(supplierId, companyId)` — obtener proveedor por id
  - `CreateAsync(supplier)` — crear proveedor
  - `UpdateAsync(supplier)` — actualizar proveedor
  - `SoftDeleteAsync(supplierId, companyId)` — soft delete
  - `GetSupplierProductsAsync(supplierId)` — productos del proveedor
  - `AddSupplierProductAsync(supplierProduct)` — asociar producto
  - `RemoveSupplierProductAsync(supplierId, productId)` — desasociar producto
  - `GetSuppliersForProductAsync(productId, companyId)` — proveedores de un producto

**Repository** (en `Infrastructure/Repositories/`):
- `SuppliersRepository.cs` — implementacion con Dapper, misma estructura que `SalesRepository`

**DI**: Registrar `ISuppliersRepository` → `SuppliersRepository` en `DependencyInjection.cs`

**Controller** (en `API/Controllers/`):
- `SuppliersController.cs` con base route `api/v1/suppliers`

**Endpoints**:
| Metodo | Ruta | Descripcion |
|--------|------|-------------|
| `GET` | `/api/v1/suppliers` | Listar proveedores (query param `branchId`) |
| `GET` | `/api/v1/suppliers/:id` | Detalle de proveedor con productos asociados |
| `POST` | `/api/v1/suppliers` | Crear proveedor |
| `PUT` | `/api/v1/suppliers/:id` | Actualizar proveedor |
| `DELETE` | `/api/v1/suppliers/:id` | Soft delete |
| `POST` | `/api/v1/suppliers/:id/products` | Asociar producto(s) al proveedor |
| `DELETE` | `/api/v1/suppliers/:id/products/:productId` | Desasociar producto |
| `GET` | `/api/v1/suppliers/:id/suggested-order` | IA genera pedido sugerido (basado en stock bajo de sus productos) |

**DTOs** (en `Application/DTOs/Suppliers/`):
- `CreateSupplierRequest.cs` — Name, ContactName, Phone, Email, Address, Notes
- `UpdateSupplierRequest.cs` — mismos campos
- `AddSupplierProductRequest.cs` — ProductId, SupplierSku, UnitCost, LeadTimeDays
- `SuggestedOrderResponse.cs` — lista de items con ProductName, CurrentStock, SuggestedQty, EstimatedCost

### 9c. Frontend

**Servicio** (`frontend/src/services/supplierService.js`):
```js
// getAll(branchId), getById(id), create(data), update(id, data),
// delete(id), addProduct(supplierId, data), removeProduct(supplierId, productId),
// getSuggestedOrder(supplierId)
```

**Componentes a crear**:

| Archivo | Descripcion |
|---------|-------------|
| `modules/suppliers/SuppliersPage.jsx` | Pagina principal: tabla de proveedores + boton agregar. Altura `h-[calc(100vh-7rem)]` |
| `modules/suppliers/components/SupplierFormModal.jsx` | Modal crear/editar proveedor (patron estandar de `docs/STYLE_GUIDE.md` seccion 5) |
| `modules/suppliers/components/SupplierDetailPanel.jsx` | Panel lateral slide-in con info del proveedor + productos asociados + acciones |
| `modules/suppliers/components/SupplierProductsManager.jsx` | Dentro del detail panel: lista de productos asociados con +/- y buscador |
| `modules/suppliers/components/SuggestedOrderPanel.jsx` | Panel que muestra pedido sugerido por IA con opcion de copiar/enviar |
| `modules/suppliers/components/ContactActions.jsx` | Botones WhatsApp + Email con mensajes prearmados |

**Ruta**: Reemplazar placeholder en `App.jsx` linea 62 con `<SuppliersPage />`

**UX esperada**:
- Tabla con columnas: Nombre, Contacto, Telefono, Email, Productos (#), Acciones
- Click en fila abre `SupplierDetailPanel` (slide-in derecha)
- Dentro del panel: info del proveedor, productos que suministra, botones de contacto
- Boton "Pedir por WhatsApp" abre `https://wa.me/{phone}?text={mensaje}` con mensaje generado
- Boton "Pedir por Email" abre `mailto:{email}?subject=Pedido&body={mensaje}`
- Boton "Pedido sugerido IA" llama al endpoint y muestra productos con stock bajo + cantidades sugeridas
- El mensaje de WhatsApp/email incluye la lista de productos y cantidades

### 9d. Flujo de Contacto con IA

1. Usuario click "Generar pedido IA" en un proveedor
2. Backend consulta productos asociados a ese proveedor que tengan stock bajo o agotado
3. IA sugiere cantidades basadas en: stock minimo, consumo reciente, lead time
4. Se muestra lista editable: producto, stock actual, sugerido, costo estimado
5. Usuario puede ajustar cantidades
6. Boton "Enviar por WhatsApp" o "Enviar por Email" genera mensaje con la lista final
7. Cuando llega el pedido, el usuario dice al agente "llego el pedido de X" y el agente registra el ingreso de stock reutilizando `add_stock`

### Criterios de aceptacion
- [ ] CRUD completo de proveedores con tabla, formulario y soft delete
- [ ] Asociacion proveedor-producto funciona (agregar/quitar)
- [ ] Boton WhatsApp abre link con mensaje prearmado
- [ ] Boton Email abre mailto con mensaje prearmado
- [ ] Pedido sugerido por IA muestra productos con stock bajo del proveedor
- [ ] El agente existente puede registrar llegada de pedido como ingreso de stock

---

## 14. Multi-tenant SaaS por `company_id` `P0`

**Estado**: `[~]` En progreso

> **Migracion a Supabase (PostgreSQL) completada**. La app ahora usa Npgsql + PostgreSQL. Todas las tablas tienen `company_id`. Todos los repositories usan sintaxis PostgreSQL. Falta: middleware de tenant, hardening frontend, onboarding.

### Objetivo
Convertir Walos en un SaaS multi-tenant robusto usando Supabase (PostgreSQL), con aislamiento por `company_id` en toda la aplicacion.

### Decision arquitectonica
- **Modelo**: single database + multi-tenant por `company_id`
- **Base de datos**: Supabase (PostgreSQL) — migrado desde SQL Server
- Cada comercio vive en la misma base, datos aislados por tenant
- El backend nunca debe devolver ni modificar registros de otra empresa
- Preparado para que un cliente enterprise pueda migrarse a base propia en el futuro

### Fase 1 — Auditoria de tenant `[x]` Completada

Se auditaron todas las tablas, queries y repositories. Se identifico que `sales.order_items` no tenia `company_id` propio.

### Fase 2 — Migracion a Supabase + Modelo de datos `[x]` Completada

**Completado**:
- Todos los scripts SQL reescritos a PostgreSQL en `supabase/migrations/`
- Todas las tablas tienen `company_id` con FK a `core.companies`
- Indices compuestos multi-tenant creados
- `sales.order_items` ahora tiene `company_id` directo
- Scripts idempotentes con `CREATE TABLE IF NOT EXISTS` y `ON CONFLICT DO NOTHING`
- Seed data adaptado para PostgreSQL

**Archivos creados**:
| Script | Contenido |
|--------|-----------|
| `supabase/migrations/001_create_schemas.sql` | Esquemas: core, inventory, sales, finance, suppliers, delivery, audit |
| `supabase/migrations/002_core_tables.sql` | companies, branches, roles, users |
| `supabase/migrations/003_inventory_tables.sql` | categories, units, products, stock, movements, ai_interactions, alerts |
| `supabase/migrations/004_sales_tables.sql` | tables, orders, order_items (con company_id) |
| `supabase/migrations/005_finance_tables.sql` | categories, entries |
| `supabase/migrations/006_suppliers_tables.sql` | suppliers, supplier_products |
| `supabase/migrations/007_delivery_tables.sql` | orders, order_items, status_history |
| `supabase/migrations/800_seed_initial_data.sql` | Datos demo para desarrollo |
| `supabase/migrations/README.md` | Documentacion de migraciones |

### Fase 2b — Backend Npgsql + Repositories `[x]` Completada

**Completado**:
- `Walos.Infrastructure.csproj`: `Microsoft.Data.SqlClient` reemplazado por `Npgsql 8.0.3`
- `SqlConnectionFactory.cs`: usa `NpgsqlConnection`
- `InventoryRepository.cs`: toda sintaxis PostgreSQL (`COALESCE`, `NOW()`, `ILIKE`, `RETURNING`, `LIMIT`, `= ANY()`)
- `SalesRepository.cs`: toda sintaxis PostgreSQL + `company_id` en `order_items`
- `FinanceRepository.cs`: toda sintaxis PostgreSQL + `RETURNING` en INSERTs
- `CompanyRepository.cs`: toda sintaxis PostgreSQL
- `OrderItem.cs`: agregado `CompanyId` para multi-tenant
- `.env.example` creado con formato de connection string Supabase
- `appsettings.json` actualizado con template de connection string PostgreSQL
- **Build exitoso**: 0 errores, 0 warnings en proyecto principal

**Cambios de sintaxis aplicados**:
| SQL Server | PostgreSQL |
|------------|------------|
| `[schema].[table]` | `schema.table` |
| `ISNULL()` | `COALESCE()` |
| `GETDATE()` | `NOW()` |
| `OUTPUT INSERTED... VALUES` | `VALUES... RETURNING` |
| `SCOPE_IDENTITY()` | `RETURNING id` |
| `SELECT TOP N` | `LIMIT N` |
| `LIKE '%' + @X + '%'` | `ILIKE '%' \|\| @X \|\| '%'` |
| `IN @List` | `= ANY(@List)` |
| `is_active = 1/0` | `is_active = TRUE/FALSE` |
| `CAST(0 AS DECIMAL)` | `0::DECIMAL` |

### Fase 3 — Contexto de tenant en backend `[ ]` Pendiente

**Meta**: Tenant seguro en todo request.

**Que hacer**:
1. Crear `TenantContextMiddleware.cs` que:
   - Extrae `companyId` del claim JWT
   - Valida que `branchId` (si viene en header/query) pertenezca a la empresa
   - Bloquea request si no hay tenant valido
   - Expone `ITenantContext` inyectable con `CompanyId`, `BranchId`, `UserId`
2. Registrar middleware en `Program.cs` despues de auth
3. Refactorizar controllers para usar `ITenantContext` en vez de `GetCompanyId()` manual

**Archivos a crear**:
- `Walos.API/Middleware/TenantContextMiddleware.cs`
- `Walos.Domain/Interfaces/ITenantContext.cs`
- `Walos.API/Services/TenantContext.cs`

**Criterios**: Todo request resuelve tenant consistentemente, no se puede operar sucursal ajena

### Fase 4 — Hardening de repositorios

**Meta**: Cero fugas por queries mal filtradas.

**Que hacer**:
1. Revisar **cada query** en cada repository
2. Asegurar `WHERE company_id = @CompanyId` en todos los SELECT, UPDATE, DELETE
3. Queries por `id` deben tambien validar `company_id`
4. JOINs no deben mezclar entidades de empresas distintas
5. Crear tests de regresion por modulo

**Checklist por repositorio**:
- [ ] `InventoryRepository.cs` — todas las queries
- [ ] `SalesRepository.cs` — todas las queries
- [ ] `FinanceRepository.cs` — todas las queries
- [ ] `CompanyRepository.cs` — todas las queries
- [ ] Futuro `SuppliersRepository.cs`

**Criterios**: Ningun query opera sin company_id, tests validan aislamiento

### Fase 5 — Frontend y sesion

**Meta**: UI consistente con tenant activo.

**Que hacer**:
1. Revisar `authStore.js` — login hidrata `companyId`, branding, permisos
2. Query keys de React Query incluyen `companyId` donde aplique
3. Cache se invalida al cambiar de empresa (futuro)
4. Uploads segmentados por empresa: `wwwroot/uploads/{companyId}/products/`
5. Branding, logo, temas: aislados por company

**Criterios**: Frontend siempre opera con tenant activo, no hay caches compartidos

### Fase 6 — Seguridad y observabilidad

**Meta**: Multi-tenant seguro y mantenible en produccion.

**Que hacer**:
1. Logs estructurados con `companyId`, `branchId`, `userId`
2. Auditoria para acciones sensibles (eliminar, facturar, cambiar config)
3. Rate limit por tenant
4. Estrategia de backups y restore por tenant
5. Seed de datos por empresa nueva (onboarding)

**Archivo a crear**: `docs/multitenant-onboarding.md`

**Criterios**: Logs con contexto, proceso de onboarding documentado, estrategia de backup

### Fase 7 — Preparacion para escalar

**Meta**: No cerrarse el camino.

**Que hacer**:
1. Centralizar acceso a datos y resolucion de tenant
2. Documentar como promover tenant a base dedicada
3. Mantener separacion: identidad del tenant vs ubicacion fisica

**Criterios**: Documento tecnico de estrategia de promocion

### Archivos clave a revisar
- `backend-dotnet/src/Walos.API/Controllers/*` — todos
- `backend-dotnet/src/Walos.Infrastructure/Repositories/*` — todos
- `backend-dotnet/sql/*` — todas las tablas
- `frontend/src/stores/authStore.js`
- `frontend/src/config/api.js`
- `frontend/src/services/*`

### Riesgos principales
- Queries por `id` sin `company_id`
- `branch_id` valido pero de otra empresa
- Caches frontend reutilizados entre tenants
- Uploads sin segmentacion por empresa
- Seeds globales que mezclen datos

### Criterios globales
- [ ] Toda entidad funcional aislada por `company_id`
- [ ] Ningun endpoint permite leer/modificar datos ajenos
- [ ] Indices multi-tenant para buen rendimiento
- [ ] Checklist de onboarding para nuevo comercio
- [ ] Documentacion tecnica del modelo multi-tenant

---

## 15. Pedidos y Domicilios `P1`

**Estado**: `[ ]`

> **Estado actual del codigo**: No existe nada implementado. No hay carpeta delivery, no hay controller, no hay tablas. Todo debe crearse desde cero. **Depende de #14 (multi-tenant)** para nacer con aislamiento correcto.

### Objetivo
Modulo operativo de pedidos a domicilio: recibir ordenes (manuales o externas), operar un tablero de estados, y apoyarse en IA para toma y gestion de pedidos.

### Fase 1 — Operacion interna (MVP)

**Meta**: Crear/gestionar pedidos internos sin integraciones externas.

#### 15a. Base de Datos

**Esquema**: `delivery` (agregar en `002_create_schemas.sql` si no existe)

**Scripts SQL a crear**:

**`700_create_table_delivery_orders.sql`**:
```
delivery.orders:
  id, company_id, branch_id, source (manual|web|whatsapp|rappi|didi_food|uber_eats|other),
  external_order_id, order_number, status, customer_name, customer_phone, customer_address,
  notes, subtotal, delivery_fee, discount_amount, total,
  accepted_at, prepared_at, dispatched_at, delivered_at,
  rejected_reason, returned_reason, created_by, created_at, updated_at, deleted_at
```

**`701_create_table_delivery_order_items.sql`**:
```
delivery.order_items:
  id, order_id (FK), product_id (FK), product_name, quantity, unit_price,
  subtotal (computed), notes, created_at
```

**`702_create_table_delivery_status_history.sql`**:
```
delivery.status_history:
  id, order_id (FK), from_status, to_status, comment, changed_by, created_at
```

#### 15b. Backend

**Entidades**:
- `DeliveryOrder.cs` — hereda BaseEntity
- `DeliveryOrderItem.cs`
- `DeliveryStatusHistory.cs`

**Interface**: `IDeliveryRepository.cs`
- `GetOrdersAsync(companyId, branchId, status?, dateFrom?, dateTo?)`
- `GetOrderByIdAsync(orderId, companyId)`
- `CreateOrderAsync(order, items)`
- `UpdateOrderStatusAsync(orderId, companyId, newStatus, comment, changedBy)`
- `GetStatusHistoryAsync(orderId)`

**Repository**: `DeliveryRepository.cs`

**Controller**: `DeliveryController.cs` (route: `api/v1/delivery`)

**Endpoints**:
| Metodo | Ruta | Descripcion |
|--------|------|-------------|
| `GET` | `/delivery/orders` | Listar pedidos con filtros (status, fecha) |
| `GET` | `/delivery/orders/:id` | Detalle con items e historial |
| `POST` | `/delivery/orders` | Crear pedido manual |
| `POST` | `/delivery/orders/:id/accept` | Aceptar (status → accepted) |
| `POST` | `/delivery/orders/:id/reject` | Rechazar con comentario obligatorio |
| `POST` | `/delivery/orders/:id/prepare` | Marcar en preparacion |
| `POST` | `/delivery/orders/:id/ready` | Marcar listo para despacho |
| `POST` | `/delivery/orders/:id/dispatch` | Marcar despachado |
| `POST` | `/delivery/orders/:id/deliver` | Marcar entregado |
| `POST` | `/delivery/orders/:id/cancel` | Cancelar con comentario |
| `POST` | `/delivery/orders/:id/return` | Devolver con comentario |

**DTOs**:
- `CreateDeliveryOrderRequest.cs` — CustomerName, Phone, Address, Notes, Items[]
- `ChangeStatusRequest.cs` — Comment (obligatorio en reject/return/cancel)

**Logica de negocio**:
- Al crear pedido → reservar stock (stock comprometido, como ventas)
- Al entregar → facturar: descontar stock real, crear movimiento tipo `delivery_sale`
- Al cancelar/rechazar → liberar stock comprometido
- Cada cambio de estado crea registro en `status_history`

#### 15c. Frontend

**Servicio**: `frontend/src/services/deliveryService.js`

**Componentes**:
| Archivo | Descripcion |
|---------|-------------|
| `modules/delivery/DeliveryOrdersPage.jsx` | Pagina principal con tablero |
| `modules/delivery/components/DeliveryBoard.jsx` | Vista Kanban: columnas por estado con cards arrastrables |
| `modules/delivery/components/DeliveryOrderCard.jsx` | Card de pedido: cliente, total, tiempo, canal, estado badge |
| `modules/delivery/components/DeliveryOrderDetailsPanel.jsx` | Panel lateral con detalle completo + historial de estados |
| `modules/delivery/components/CreateDeliveryOrderPanel.jsx` | Panel para crear pedido manual (similar a AddTablePanel) |
| `modules/delivery/components/StatusActionModal.jsx` | Modal para rechazar/devolver/cancelar con textarea obligatorio |

**Ruta**: Agregar `/delivery` en `App.jsx` con `<DeliveryOrdersPage />`

**Sidebar**: Agregar item "Pedidos" con icono `Bike` o `Package` de Lucide

**UX del tablero**:
- Columnas: Nuevos | En preparacion | Listos | En camino | Entregados
- Cada card muestra: #orden, cliente, tiempo transcurrido (badge rojo si >30min), total, canal (badge)
- Click en card abre `DeliveryOrderDetailsPanel`
- Acciones rapidas en card: aceptar, preparar, despachar (segun estado actual)
- Rechazar/devolver/cancelar abre modal con comentario obligatorio
- Filtros rapidos por canal y por fecha

**Flujo de estados visual**:
```
new → accepted → preparing → ready_for_dispatch → out_for_delivery → delivered
 ↘ rejected                                                        ↗ cancelled
                                                                   ↗ returned
```

### Fase 2 — IA de toma de pedidos (posterior)

- Panel/chat para convertir texto libre a pedido estructurado
- Validacion de stock antes de confirmar
- Endpoint: `POST /api/v1/delivery/ai/intake`
- Componente: `AiOrderIntakePanel.jsx`

### Fase 3 — Integraciones externas (posterior)

- Capa de adaptadores por plataforma (Rappi, Didi, etc.)
- Tabla `delivery.integrations` para tokens/config por empresa
- Tabla `delivery.integration_logs` para trazabilidad
- Webhooks que resuelven tenant antes de procesar

### Fase 4 — Domiciliario propio (posterior)

- Tabla `delivery.driver_assignments`
- Asignacion y seguimiento de repartidor

### Relaciones con otros modulos
- **Inventario**: stock comprometido al crear pedido, liberado al cancelar
- **Ventas/Finanzas**: al entregar, impacta ventas y finanzas del periodo
- **Multi-tenant**: todo aislado por `company_id` desde el dia 1

### Criterios de aceptacion (Fase 1)
- [ ] Existe modulo "Pedidos y Domicilios" visible en sidebar y funcional
- [ ] Se pueden crear pedidos manuales con cliente, direccion e items
- [ ] Tablero Kanban muestra pedidos por estado
- [ ] Se pueden mover pedidos por estados con acciones
- [ ] Rechazar/devolver/cancelar obliga comentario
- [ ] Historial de estados visible en detalle del pedido
- [ ] Stock se compromete al crear y se libera al cancelar
- [ ] Entrega descuenta stock real y genera movimiento financiero
- [ ] Todo filtrado por `company_id` y `branch_id`

---

## Orden de Ejecucion

| Orden | Tarea | Dependencia | Complejidad |
|-------|-------|-------------|-------------|
| 1 | **#14 Multi-tenant** (fases 1-4) | Ninguna — base transversal | Alta |
| 2 | **#9 Proveedores** | Mejor despues de #14 | Media |
| 3 | **#14 Multi-tenant** (fases 5-7) | Proveedores listo | Media |
| 4 | **#15 Pedidos Fase 1** | #14 completo | Alta |
| 5 | **#15 Pedidos Fase 2** (IA) | Fase 1 funcional | Media |
| 6 | **#15 Pedidos Fase 3** (integraciones) | Fase 1 funcional | Alta |

> **Nota para agentes**: Proveedores (#9) puede ejecutarse en paralelo con las fases tempranas de multi-tenant si se sigue el patron existente. Pedidos (#15) debe esperar a que multi-tenant este razonablemente solido.
