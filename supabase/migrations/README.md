# Migraciones Supabase (PostgreSQL)

## Orden de ejecucion

```
001_create_schemas.sql          -- Esquemas: core, inventory, sales, finance, suppliers, delivery, audit
002_core_tables.sql             -- companies, branches, roles, users
003_inventory_tables.sql        -- categories, units, products, stock, movements, ai_interactions, alerts
004_sales_tables.sql            -- tables, orders, order_items
005_finance_tables.sql          -- categories, entries
006_suppliers_tables.sql        -- suppliers, supplier_products
007_delivery_tables.sql         -- orders, order_items, status_history
800_seed_initial_data.sql       -- Datos demo para desarrollo
```

## Ejecutar en Supabase

### Opcion A: SQL Editor de Supabase Dashboard
1. Ir a SQL Editor en el dashboard de Supabase
2. Copiar y ejecutar cada script en orden

### Opcion B: Supabase CLI
```bash
supabase db reset    # Ejecuta todas las migraciones en orden
```

## Diferencias vs SQL Server anterior

| SQL Server | PostgreSQL |
|-----------|-----------|
| `BIGINT IDENTITY(1,1)` | `BIGSERIAL` |
| `NVARCHAR(n)` | `VARCHAR(n)` |
| `NVARCHAR(MAX)` | `TEXT` o `JSONB` |
| `BIT` | `BOOLEAN` |
| `DATETIME2` | `TIMESTAMPTZ` |
| `GETDATE()` | `NOW()` |
| `ISNULL()` | `COALESCE()` |
| `[schema].[table]` | `schema.table` |
| `AS (...) PERSISTED` | `GENERATED ALWAYS AS (...) STORED` |

## Multi-tenant

**Todas** las tablas de negocio tienen `company_id` como columna obligatoria con FK a `core.companies`.
Los indices incluyen `company_id` como primera columna para queries eficientes.
