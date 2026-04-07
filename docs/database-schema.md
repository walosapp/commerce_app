# Esquema de Base de Datos - Walos

> **Nota**: La base de datos en desarrollo se llama `SCM_App_Track_Me` (no `WalosDB`).

## Diagrama de Relaciones (Módulo Core + Inventario)

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
    
    branches ||--o{ inventory_movements : "registra"
    inventory_products ||--o{ inventory_movements : "mueve"
    
    branches ||--o{ inventory_alerts : "genera"
    inventory_products ||--o{ inventory_alerts : "alerta"
    
    users ||--o{ inventory_ai_interactions : "interactúa"
    branches ||--o{ inventory_ai_interactions : "procesa"

    companies {
        bigint id PK
        nvarchar name
        nvarchar tax_id UK
        nvarchar currency
        bit is_active
    }
    
    branches {
        bigint id PK
        bigint company_id FK
        nvarchar name
        nvarchar code
        nvarchar branch_type
        bit is_active
    }
    
    roles {
        bigint id PK
        bigint company_id FK
        nvarchar code
        nvarchar permissions
        int access_level
    }
    
    users {
        bigint id PK
        bigint company_id FK
        bigint branch_id FK
        bigint role_id FK
        nvarchar email UK
        nvarchar username UK
        nvarchar password_hash
        bit is_active
    }
    
    inventory_products {
        bigint id PK
        bigint company_id FK
        bigint category_id FK
        bigint unit_id FK
        nvarchar sku UK
        nvarchar name
        nvarchar description
        decimal cost_price
        decimal sale_price
        decimal margin_percentage "computed"
        decimal min_stock
        decimal max_stock
        bit is_active
    }
    
    inventory_stock {
        bigint id PK
        bigint company_id FK
        bigint branch_id FK
        bigint product_id FK
        decimal quantity
        decimal available_quantity
    }
    
    inventory_movements {
        bigint id PK
        bigint company_id FK
        bigint branch_id FK
        bigint product_id FK
        nvarchar movement_type
        decimal quantity
        decimal unit_cost
        bit created_by_ai
        decimal ai_confidence
        nvarchar ai_metadata "JSON"
    }
    
    inventory_alerts {
        bigint id PK
        bigint company_id FK
        bigint branch_id FK
        bigint product_id FK
        nvarchar alert_type
        nvarchar severity
        nvarchar message
        nvarchar status
    }
    
    inventory_ai_interactions {
        bigint id PK
        bigint company_id FK
        bigint branch_id FK
        bigint user_id FK
        nvarchar session_id
        nvarchar interaction_type
        nvarchar user_input
        nvarchar ai_response
        nvarchar ai_action
        nvarchar processed_data "JSON"
        nvarchar action_status
        decimal confidence_score
        nvarchar ai_model
        int tokens_used
        bit confirmed_by_user
        datetime confirmed_at
    }
```

## Tablas por Esquema

### Schema: `core`
| Tabla | Descripción | Registros clave |
|---|---|---|
| **companies** | Empresas multi-tenant | company_id base para aislamiento |
| **branches** | Sucursales de cada empresa | branch_type: bar, restaurant, warehouse |
| **roles** | Roles de usuario (RBAC) | permissions como JSON |
| **users** | Usuarios del sistema | auth por username + password_hash |

### Schema: `inventory`
| Tabla | Descripción | Campos especiales |
|---|---|---|
| **categories** | Categorías de productos | name, is_active |
| **units** | Unidades de medida | name, abbreviation (ej: "Botella", "Bot") |
| **products** | Catálogo de productos | cost_price se actualiza con promedio ponderado |
| **stock** | Stock actual por sucursal | quantity se incrementa con cada compra |
| **movements** | Historial de movimientos | created_by_ai, ai_confidence, ai_metadata |
| **ai_interactions** | Interacciones con IA | session_id para multi-turno, processed_data JSON |
| **alerts** | Alertas (stock bajo, etc.) | severity: low/medium/high/critical |

## Campos Estándar

Todas las tablas incluyen:
- `id`: Primary key (BIGINT IDENTITY)
- `company_id`: Multi-tenant (FK a companies)
- `created_at`: Timestamp de creación (default GETDATE())
- `updated_at`: Timestamp de actualización
- `deleted_at`: Soft delete (NULL = activo)
- `created_by`: Usuario que creó (FK a users)
- `updated_by`: Usuario que actualizó (FK a users)

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
