# Migraciones Supabase (PostgreSQL)

## Orden de ejecucion

Todo archivo `.sql` en este directorio forma parte de la secuencia automatica.
El orden real se obtiene lexicograficamente por nombre:

```text
001_create_schemas.sql
002_core_tables.sql
003_inventory_tables.sql
004_sales_tables.sql
005_finance_tables.sql
006_suppliers_tables.sql
007_delivery_tables.sql
008_finance_recurring_templates.sql
009_finance_entries_status_and_frequency.sql
009_product_track_stock.sql
010_finance_template_auto_include.sql
010_recipes_table.sql
011_purchase_orders.sql
012_credits.sql
013_platform_billing.sql
014_business_hours.sql
015_ai_sessions.sql
016_cash_registers.sql
017_credit_refund_atomicity.sql
019_company_features.sql
800_seed_initial_data.sql        # marcador de compatibilidad, no crea datos
900_seed_dev_user.sql            # marcador de compatibilidad, no crea usuarios
999_cleanup_data_keep_inventory.sql
```

`999_cleanup_data_keep_inventory.sql` conserva la version del historial remoto,
pero es solo un marcador no destructivo y **no elimina datos**.

Los marcadores `800` y `900` tambien conservan versiones historicas, pero ya
no crean tenants, cuentas ni contrasenas. Esto evita que un reset o despliegue
publique credenciales conocidas.

## Bootstrap manual opt-in

Los datos de demostracion y la cuenta tecnica solo pueden crearse mediante:

- `supabase/scripts/bootstrap_demo_data.sql`;
- `supabase/scripts/bootstrap_walos_system.sql`.

Ambos scripts requieren que el operador suministre un hash BCrypt propio en la
misma sesion mediante el parametro indicado en el encabezado del archivo. El
hash no debe persistirse en `.env`, documentacion ni Git. Estos scripts no
forman parte de `supabase db reset`, CI/CD ni de las migraciones automaticas.

## Ejecutar migraciones

### SQL Editor de Supabase Dashboard

Copiar y ejecutar cada migracion en el orden anterior. No ejecutar sentencias
sueltas dentro de una migracion transaccional.

### Supabase CLI

```bash
supabase db reset
```

Este comando ejecuta automaticamente las migraciones. Nunca ejecuta el
mantenimiento destructivo, porque ese script vive fuera de `migrations/`.

## Limpieza destructiva manual

`supabase/scripts/cleanup_data_keep_inventory.sql` es una herramienta de
recuperacion/mantenimiento **opt-in**. No es una migracion ni un seed. Solo debe
copiarse y ejecutarse manualmente en SQL Editor despues de:

1. confirmar por escrito el proyecto y ambiente objetivo;
2. verificar que no sea produccion;
3. disponer de un respaldo comprobado;
4. revisar el script completo en la misma sesion.

Antes de la primera sentencia destructiva, el script exige
`walos.bootstrap_dev_password_hash`. Si falta o no tiene formato BCrypt, aborta
la transaccion. La credencial se suministra en la sesion y nunca se guarda en
el repositorio.

No automatizarlo en CI/CD ni moverlo nuevamente a `supabase/migrations`.

### Compatibilidad del historial remoto

El marcador `999_cleanup_data_keep_inventory.sql` permanece con el mismo nombre
para que instalaciones que ya registraron esa version no queden con una version
local ausente. Esto no puede deshacer una limpieza ejecutada en el pasado; solo
garantiza que instalaciones y resets futuros no la repitan.

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
