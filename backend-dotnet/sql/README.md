# Scripts SQL - Walos Database

## Orden de Ejecución

Los scripts deben ejecutarse en el orden numérico indicado. Cada script es idempotente y puede re-ejecutarse sin problemas.

### Estructura de Numeración
- **001-099**: Configuración inicial y esquemas
- **100-199**: Tablas core (empresas, usuarios, roles)
- **200-299**: Módulo de inventario
- **300-399**: Módulo de ventas
- **400-499**: Módulo de proveedores
- **500-599**: Catálogos y configuraciones
- **600-699**: Vistas y funciones
- **700-799**: Triggers y procedimientos almacenados
- **800-899**: Datos iniciales (seeds)
- **900-999**: Índices y optimizaciones

## Ejecución

### Desarrollo
Ejecutar cada script en orden usando SQL Server Management Studio (SSMS), Azure Data Studio o `sqlcmd`:

```bash
# Ejemplo con sqlcmd
sqlcmd -S localhost -U sa -P tu_password -i 001_create_database.sql
sqlcmd -S localhost -U sa -P tu_password -d WalosDB -i 002_create_schemas.sql
# Continuar en orden numérico...
```

### Producción
Ejecutar manualmente cada script en orden, validando resultado antes de continuar.

## Convenciones

- **Nombres de tablas**: snake_case, plural (ej: `companies`, `inventory_items`)
- **Nombres de columnas**: snake_case (ej: `created_at`, `company_id`)
- **Primary Keys**: `id` (UUID o BIGINT IDENTITY)
- **Foreign Keys**: `{tabla_singular}_id` (ej: `company_id`, `user_id`)
- **Timestamps**: `created_at`, `updated_at`, `deleted_at` (soft delete)
- **Multi-tenant**: Todas las tablas incluyen `company_id` y `branch_id` donde aplique

## Campos Estándar en Todas las Tablas

```sql
id BIGINT IDENTITY(1,1) PRIMARY KEY,
company_id BIGINT NOT NULL,
created_at DATETIME2 DEFAULT GETDATE(),
updated_at DATETIME2 DEFAULT GETDATE(),
deleted_at DATETIME2 NULL,
created_by BIGINT NULL,
updated_by BIGINT NULL
```

## Backup y Migración

Antes de ejecutar en producción:
1. Backup completo de la base de datos
2. Ejecutar en ambiente de staging
3. Validar integridad de datos
4. Ejecutar en producción en ventana de mantenimiento
