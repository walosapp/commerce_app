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

## Flujo del Asistente de IA (detalle)

### POST `/ai/process`

**Request:**
```json
{
  "userInput": "Me llegaron 100 whisky por 8500000",
  "inputType": "text",
  "sessionId": "uuid-opcional"
}
```

**Proceso interno:**
1. Carga productos existentes, categorías y unidades de la DB
2. Si hay `sessionId`, recupera las últimas 10 interacciones para contexto
3. Construye system prompt con lista exacta de productos y reglas estrictas
4. Envía a OpenAI: `[system, ...historial, user_message]`
5. Parsea JSON de respuesta de la IA
6. Guarda interacción en `ai_interactions`
7. Devuelve resultado al frontend

**Response:**
```json
{
  "success": true,
  "data": {
    "interactionId": 18,
    "sessionId": "uuid",
    "action": "create_and_stock",
    "response": "Propongo crear Whisky: costo $85K, venta $119K (40% margen)",
    "data": {
      "products": [{
        "name": "Whisky Jack Daniels",
        "quantity": 100,
        "unit_cost": 85000,
        "sale_price": 119000,
        "profit_margin": 40,
        "category": "Bebidas Alcohólicas",
        "unit": "Botella",
        "is_new": true
      }],
      "total": 8500000
    },
    "confidence": 95
  }
}
```

### POST `/ai/confirm/:id`

**Proceso interno según acción:**

**`add_stock` (producto existente):**
1. Busca producto en DB por nombre
2. Obtiene stock actual de la sucursal
3. Calcula costo promedio ponderado: `(stockActual × costoActual + qtyNueva × costoNuevo) / total`
4. Actualiza `products.cost_price` en DB
5. Incrementa `stock.quantity`
6. Crea movimiento tipo `purchase`

**`create_and_stock` (producto nuevo):**
1. Busca producto → no existe → crea con:
   - `sale_price = unit_cost × (1 + profit_margin / 100)` (o fallback 30%)
   - SKU auto-generado: `AI-yyyyMMddHHmmss-N`
   - Categoría y unidad de las disponibles
2. Crea entrada en `stock` con qty=0
3. Incrementa stock con la cantidad
4. Crea movimiento tipo `purchase`

**Safety nets:**
- Si la IA dice `is_new: false` pero el producto no existe en DB → se trata como nuevo
- Si falta categoría/unidad → usa la primera disponible
- Si falta margen → default 30%

## Clases Clave

### `InventoryService.cs` (Application)
- `ProcessAiInventoryInputAsync`: orquesta contexto → OpenAI → guardar interacción
- `ConfirmAiActionAsync`: ejecuta la acción (crear producto, actualizar stock, movimiento)

### `OpenAiService.cs` (Infrastructure)
- Construye system prompt con productos/categorías/unidades exactas
- Envía historial de sesión como mensajes previos
- Parsea respuesta JSON estricta de la IA

### `InventoryRepository.cs` (Infrastructure)
- ~660 líneas con todas las queries SQL parametrizadas
- Métodos clave: `FindProductsByNameAsync`, `UpdateStockAsync`, `CreateProductAsync`, `UpdateProductCostAndPriceAsync`, `GetStockByProductAsync`, `GetAiInteractionsBySessionAsync`

### `IAiService.cs` (Domain)
- `AiProductEntry`: name, quantity, unitCost, salePrice, profitMargin, category, unit, isNew
- `AiContext`: companyName, existingProductNames, categories, units
- `AiConversationMessage`: role (user/assistant), content

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
[INF] IA procesó entrada de inventario. Action: add_stock, Confidence: 100, Tokens: 952
[INF] Costo promedio ponderado de Cerveza Águila: (54 × $0 + 500 × $2500) / 554 = $2256.32
```

## Pruebas

```bash
dotnet test
```

## Swagger

En desarrollo: `http://localhost:3000/swagger`
