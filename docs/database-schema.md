# Esquema de Base de Datos - Walos

> **Motor**: PostgreSQL (Supabase). Migraciones en `supabase/migrations/`.

## Diagrama de Relaciones

```mermaid
erDiagram
    companies ||--o{ branches : "tiene"
    companies ||--o{ roles : "define"
    companies ||--o{ users : "emplea"
    branches ||--o{ users : "asigna"
    roles ||--o{ users : "asigna"

    companies ||--o{ inventory_categories : "organiza"
    companies ||--o{ inventory_units : "define"
    companies ||--o{ inventory_products : "gestiona"
    inventory_categories ||--o{ inventory_products : "clasifica"
    inventory_units ||--o{ inventory_products : "mide"

    branches ||--o{ inventory_stock : "almacena"
    inventory_products ||--o{ inventory_stock : "tiene"
    inventory_products ||--o{ inventory_movements : "mueve"
    inventory_products ||--o{ recipes : "producto preparado"
    inventory_products ||--o{ recipes : "ingrediente"

    companies ||--o{ sales_tables : "tiene"
    sales_tables ||--o{ sales_orders : "genera"
    sales_orders ||--o{ sales_order_items : "contiene"
    sales_orders ||--o{ credits : "genera"
    credits ||--o{ credit_payments : "recibe"

    companies ||--o{ finance_categories : "define"
    finance_categories ||--o{ finance_entries : "agrupa"

    companies ||--o{ suppliers : "registra"
    suppliers ||--o{ supplier_products : "asocia"
    suppliers ||--o{ purchase_orders : "recibe"
    purchase_orders ||--o{ purchase_order_items : "contiene"

    companies ||--o{ delivery_orders : "gestiona"
    delivery_orders ||--o{ delivery_order_items : "contiene"
    delivery_orders ||--o{ delivery_status_history : "registra"

    companies {
        bigint id PK
        varchar name
        varchar tax_id UK
        varchar currency
        boolean is_active
    }

    branches {
        bigint id PK
        bigint company_id FK
        varchar name
        varchar code
        varchar branch_type
        boolean is_active
    }

    users {
        bigint id PK
        bigint company_id FK
        bigint branch_id FK
        bigint role_id FK
        varchar email UK
        varchar password_hash
        boolean is_active
    }

    inventory_products {
        bigint id PK
        bigint company_id FK
        bigint category_id FK
        bigint unit_id FK
        varchar sku UK
        varchar name
        varchar product_type
        boolean track_stock
        decimal cost_price
        decimal sale_price
        decimal margin_percentage
        decimal min_stock
        boolean is_active
    }

    sales_orders {
        bigint id PK
        bigint company_id FK
        bigint table_id FK
        varchar status
        decimal total
        decimal discount_amount
        decimal final_total_paid
    }

    finance_entries {
        bigint id PK
        bigint company_id FK
        bigint category_id FK
        varchar type
        decimal amount
        timestamptz entry_date
        varchar status
        varchar frequency
    }

    suppliers {
        bigint id PK
        bigint company_id FK
        varchar name
        varchar phone
        varchar email
    }

    delivery_orders {
        bigint id PK
        bigint company_id FK
        varchar status
        varchar customer_name
        varchar customer_phone
        text delivery_address
    }
```

## Tablas por Esquema

### Schema: `core`
| Tabla | Descripción | Registros clave |
|---|---|---|
| **companies** | Empresas multi-tenant | company_id base para aislamiento |
| **branches** | Sucursales de cada empresa | branch_type: bar, restaurant, warehouse |
| **roles** | Roles de usuario (RBAC) | permissions como JSONB |
| **users** | Usuarios del sistema | auth por email + password_hash (BCrypt) |

### Schema: `inventory`
| Tabla | Descripción | Campos especiales |
|---|---|---|
| **categories** | Categorías de productos | name, is_active |
| **units** | Unidades de medida | name, abbreviation (ej: "Botella", "Bot") |
| **products** | Catálogo de productos | product_type: simple/prepared/combo/service, track_stock |
| **stock** | Stock actual por sucursal | quantity, reserved_quantity, available_quantity |
| **movements** | Historial de movimientos | created_by_ai, ai_confidence, ai_metadata |
| **recipes** | BOM de productos preparados | product_id (preparado) + ingredient_id (insumo) |
| **ai_interactions** | Interacciones con IA | session_id para multi-turno, processed_data JSON |
| **alerts** | Alertas (stock bajo, etc.) | severity: low/medium/high/critical |

### Schema: `sales`
| Tabla | Descripción | Campos especiales |
|---|---|---|
| **tables** | Mesas del establecimiento | name, position_x/y, status: open/invoiced/cancelled |
| **orders** | Pedidos por mesa | total, discount_amount, final_total_paid, status |
| **order_items** | Items de cada pedido | product_id, quantity, unit_price, subtotal |
| **credits** | Créditos a clientes | customer_name, total_amount, paid_amount, status |
| **credit_payments** | Pagos de créditos | amount, payment_method, notes |

### Schema: `finance`
| Tabla | Descripción | Campos especiales |
|---|---|---|
| **categories** | Categorías financieras (ingreso/gasto) | type, frequency, default_amount, day_of_month |
| **entries** | Movimientos financieros | amount, entry_date, status: pending/posted/skipped |

### Schema: `suppliers`
| Tabla | Descripción | Campos especiales |
|---|---|---|
| **suppliers** | Proveedores | name, phone, email, contact_name |
| **supplier_products** | Productos por proveedor | supplier_id + product_id |
| **purchase_orders** | Órdenes de compra | status: draft/sent/received/cancelled, expected_date |
| **purchase_order_items** | Items de órdenes | product_id, quantity, unit_cost |

### Schema: `delivery`
| Tabla | Descripción | Campos especiales |
|---|---|---|
| **orders** | Pedidos a domicilio | customer_name, delivery_address, status Kanban |
| **order_items** | Items del pedido | product_id, quantity, unit_price |
| **status_history** | Historial de estados | old_status, new_status, notes, changed_by |

## Campos Estándar

Todas las tablas incluyen:
- `id`: Primary key (BIGSERIAL)
- `company_id`: Multi-tenant (FK a core.companies)
- `created_at`: Timestamp de creación (default `NOW()`)
- `updated_at`: Timestamp de actualización
- `deleted_at`: Soft delete (NULL = activo)

## Índices Principales

### Performance
- Todas las FK tienen índices
- `company_id` y `branch_id` indexados en todas las tablas
- `is_active` y `deleted_at` para filtros comunes
- `created_at` para ordenamiento temporal

### Búsqueda
- `email` y `username` en users (UNIQUE)
- `sku` y `barcode` en products
- `session_id` en ai_interactions (para multi-turno)

## Lógica de Negocio en Datos

### Costo Promedio Ponderado
Cuando se agrega stock a un producto existente, `products.cost_price` se recalcula:
```
nuevo_cost_price = (stock_actual × cost_price_actual + qty_nueva × cost_nuevo) / (stock_actual + qty_nueva)
```
Implementado en `InventoryService.ConfirmAiActionAsync`.

### Margen de Ganancia
Al crear un producto por IA, el `sale_price` se calcula:
```
sale_price = cost_price × (1 + profit_margin / 100)
```
El `margin_percentage` es un campo computado en DB: `(sale_price - cost_price) / cost_price * 100`.

### Tipos de Movimiento
| movement_type | Descripción |
|---|---|
| `purchase` | Compra/entrada de mercancía |
| `sale` | Venta |
| `adjustment` | Ajuste manual |
| `transfer` | Transferencia entre sucursales |
| `waste` | Merma/desperdicio |

### Estados de Interacción IA
| action_status | Significado |
|---|---|
| `pending` | Esperando confirmación del usuario |
| `success` | Confirmado y ejecutado |
| `rejected` | Rechazado por el usuario |
| `failed` | Error al ejecutar |

## Tipos de Datos JSON

### ai_interactions.processed_data
```json
{
  "products": [
    {
      "name": "Whisky Jack Daniels",
      "quantity": 100,
      "unit_cost": 85000,
      "sale_price": 119000,
      "profit_margin": 40,
      "category": "Bebidas Alcohólicas",
      "unit": "Botella",
      "min_stock": 10,
      "description": "Whisky premium",
      "is_new": true
    }
  ],
  "total": 8500000
}
```

### movements.ai_metadata
```json
{
  "interactionId": 18
}
```

### roles.permissions
```json
{
  "inventory": {"read": true, "write": true, "delete": false},
  "sales": {"read": true, "write": true},
  "reports": {"read": true}
}
```
