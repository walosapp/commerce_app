# Walos API - Backend .NET

API REST con **ASP.NET Core 8** para gestión comercial con asistente de IA conversacional.

## Tecnologías

| Componente | Tecnología | Versión |
|---|---|---|
| Framework | ASP.NET Core | 8.0 |
| Data Access | Dapper | micro-ORM |
| Base de datos | SQL Server | `SCM_App_Track_Me` |
| Autenticación | JWT Bearer | stateless |
| Validación | FluentValidation | request validation |
| Logging | Serilog | consola + archivos |
| IA | OpenAI API | gpt-3.5-turbo |
| Docs API | Swagger/OpenAPI | auto-generado |

## Arquitectura (Clean Architecture)

```
Walos.API              → Controllers, Middleware, Program.cs
Walos.Application      → Services, DTOs, Validators (lógica de negocio)
Walos.Domain           → Entities, Interfaces, Exceptions (sin dependencias)
Walos.Infrastructure   → Repositories (Dapper), OpenAI Service, DB Connection
Walos.Tests            → Pruebas unitarias (xUnit)

Dependencias: API → Application → Domain ← Infrastructure
```

## Estructura del Proyecto

```
backend-dotnet/
├── src/
│   ├── Walos.API/
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs           # POST /auth/login
│   │   │   ├── HealthController.cs         # GET /health
│   │   │   └── InventoryController.cs      # Productos, stock, IA (9 endpoints)
│   │   ├── Middleware/
│   │   │   ├── ExceptionHandlingMiddleware.cs  # Manejo centralizado de errores
│   │   │   └── TenantContextMiddleware.cs      # Extrae companyId/branchId de JWT
│   │   ├── Program.cs                     # DI, middleware pipeline, Kestrel config
│   │   ├── .env / .env.example            # Variables de entorno
│   │   └── appsettings.json               # Config base (se sobreescribe con .env)
│   │
│   ├── Walos.Application/
│   │   ├── DTOs/
│   │   │   ├── Common/ApiResponse.cs      # Respuesta estandarizada {success, message, data}
│   │   │   └── Inventory/
│   │   │       ├── AiInputRequest.cs      # {userInput, inputType, sessionId}
│   │   │       └── CreateProductRequest.cs
│   │   ├── Services/
│   │   │   ├── IInventoryService.cs       # Interfaz + DTOs: AiProcessResult, AiConfirmResult
│   │   │   └── InventoryService.cs        # Orquestación: contexto → IA → DB → respuesta
│   │   ├── Validators/
│   │   │   ├── AiInputValidator.cs
│   │   │   └── CreateProductValidator.cs
│   │   └── DependencyInjection.cs         # Registro de servicios de Application
│   │
│   ├── Walos.Domain/
│   │   ├── Entities/
│   │   │   ├── Product.cs                 # Nombre, SKU, CostPrice, SalePrice, CategoryId, UnitId
│   │   │   ├── Stock.cs                   # BranchId, ProductId, Quantity
│   │   │   ├── Movement.cs               # MovementType, Quantity, UnitCost, CreatedByAi
│   │   │   ├── AiInteraction.cs           # SessionId, UserInput, AiResponse, ProcessedData
│   │   │   ├── Alert.cs                   # AlertType, Severity, ProductName
│   │   │   └── BaseEntity.cs             # Id, CompanyId, CreatedAt
│   │   ├── Exceptions/
│   │   │   ├── BusinessException.cs       # → HTTP 422
│   │   │   ├── NotFoundException.cs       # → HTTP 404
│   │   │   └── ValidationException.cs     # → HTTP 400
│   │   └── Interfaces/
│   │       ├── IAiService.cs              # ProcessInventoryInputAsync + DTOs
│   │       │                              #   AiProductEntry, AiContext, AiConversationMessage
│   │       ├── IInventoryRepository.cs    # 15+ métodos de acceso a datos
│   │       └── IDbConnectionFactory.cs    # Contrato de conexión SQL Server
│   │
│   └── Walos.Infrastructure/
│       ├── Data/SqlConnectionFactory.cs   # Lee DB_CONNECTION_STRING de .env
│       ├── Repositories/
│       │   └── InventoryRepository.cs     # ~660 líneas, todas las queries SQL con Dapper
│       ├── Services/
│       │   └── OpenAiService.cs           # System prompt, llamada API, parseo JSON
│       └── DependencyInjection.cs         # Registro de repositorios y servicios externos
│
├── tests/Walos.Tests/
├── sql/                                   # Scripts SQL ordenados (001→800)
└── Walos.sln
```

## Instalación

```bash
cd backend-dotnet

# Configurar variables de entorno
cd src/Walos.API
cp .env.example .env
# Editar .env con credenciales reales
cd ../..

# Restaurar, compilar y ejecutar
dotnet restore
dotnet build
dotnet run --project src/Walos.API
# → http://localhost:3000
```

## Configuración (.env)

| Variable | Descripción | Default |
|---|---|---|
| `DB_CONNECTION_STRING` | Conexión SQL Server | requerido |
| `JWT_SECRET` | Clave JWT (min 32 chars) | requerido |
| `JWT_EXPIRES_MINUTES` | Expiración del token | `60` |
| `OPENAI_API_KEY` | API Key de OpenAI | requerido |
| `OPENAI_MODEL` | Modelo de IA | `gpt-3.5-turbo` |
| `OPENAI_MAX_TOKENS` | Máx tokens por respuesta | `1000` |
| `OPENAI_TEMPERATURE` | Creatividad (0-1) | `0.7` |
| `CORS_ORIGINS` | Orígenes permitidos | `*` |
| `PORT` | Puerto del servidor | `3000` |

> **Nota**: La base de datos en desarrollo se llama `SCM_App_Track_Me`.

## Endpoints

### Auth (`/api/v1/auth`)
| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| POST | `/login` | Login con username/password | No |

### Inventario (`/api/v1/inventory`)
| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| GET | `/products` | Listar productos (filtros opcionales) | Si |
| GET | `/products/:id` | Obtener producto por ID | Si |
| POST | `/products` | Crear producto manualmente | Si |
| GET | `/stock` | Stock por sucursal | Si |
| GET | `/stock/low` | Productos con stock bajo | Si |
| POST | `/ai/process` | **Procesar entrada con IA** | Si |
| POST | `/ai/confirm/:id` | **Confirmar acción de IA** | Si |
| GET | `/alerts` | Alertas activas | Si |
| GET | `/reports/profits` | Reporte de ganancias | Si |

### Health
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/health` | Health check |
| GET | `/api/v1` | Info de la API |

## Asistente de IA en V1

### POST `/ai/chat`

El orquestador clasifica la consulta y habilita únicamente capacidades de lectura compatibles con los módulos activos de la empresa:

- inventario: consultas de productos, stock y alertas;
- compras: preparación de una lista de reposición;
- proveedores: consultas informativas;
- delivery: consulta de pedidos;
- general: orientación sobre el negocio.

Cada conversación está aislada por empresa y usuario. Los cambios de módulos invalidan el contexto previo mediante un fingerprint de capacidades.

### Mutaciones deshabilitadas

En V1 la IA **no crea productos, no registra entradas de stock y no cambia estados operativos**. Esas acciones se realizan desde sus módulos para conservar atomicidad, idempotencia y auditoría. Los endpoints legacy `/ai/process` y `/ai/confirm/:id` mantienen esta restricción y no ejecutan escrituras de inventario.

## Clases Clave

### `OrchestratorService.cs` (Application)
- Aplica el gate de capacidades antes de consultar repositorios.
- Mantiene sesiones por empresa/usuario y despacha consultas de solo lectura.

### `AiCapabilityGuard.cs` (Application)
- Resuelve en bloque los módulos habilitados para la empresa.
- Falla cerrado cuando una capacidad requerida está deshabilitada.

### `OpenAiService.cs` (Infrastructure)
- Construye prompts de solo lectura y procesa respuestas estructuradas.

### `AiSessionRepository.cs` (Infrastructure)
- Persiste contexto y mensajes con alcance de empresa/usuario.
- Serializa conversaciones concurrentes del mismo usuario con un advisory lock transaccional.

## Multi-tenancy

- JWT Claims: `companyId`, `userId`, `branchId`
- Headers de respaldo: `X-Tenant-ID`, `X-Branch-ID`
- `TenantContextMiddleware` extrae y pone en `HttpContext.Items`
- Todas las queries SQL filtran por `company_id`

## Manejo de Errores

Formato estandarizado:
```json
{
  "success": false,
  "message": "Descripción del error",
  "code": "ERROR_CODE"
}
```

| Excepción | HTTP | Cuándo |
|---|---|---|
| `ValidationException` | 400 | Request inválido |
| `NotFoundException` | 404 | Entidad no encontrada |
| `BusinessException` | 422 | Regla de negocio violada |
| `UnauthorizedAccessException` | 403 | Sin permisos |
| Otros | 500 | Error interno |

## Logging

Serilog escribe a:
- **Consola**: todos los niveles, con colores
- `logs/combined-*.log`: rotación diaria
- `logs/error-*.log`: solo errores, rotación diaria

Logs importantes del flujo IA:
```
[INF] Orchestrator intent: inventory (last_agent: orchestrator) for session 42
```

## Pruebas

```bash
dotnet test
```

## Swagger

En desarrollo: `http://localhost:3000/swagger`
