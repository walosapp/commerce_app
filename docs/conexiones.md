# Configuración de Conexiones - Walos

## Backend (.env)

El backend lee variables de entorno desde `backend-dotnet/src/Walos.API/.env`. Copiar `.env.example` como `.env` y configurar:

```env
# Base de Datos (PostgreSQL / Supabase)
DB_CONNECTION_STRING=Host=db.<project-ref>.supabase.co;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<your-password>;SSL Mode=Require;Trust Server Certificate=true

# JWT
JWT_SECRET=tu-clave-secreta-jwt-de-al-menos-32-caracteres
JWT_EXPIRES_MINUTES=60
JWT_REFRESH_SECRET=tu-clave-secreta-refresh-token
JWT_REFRESH_EXPIRES_DAYS=7

# OpenAI
OPENAI_API_KEY=sk-tu-api-key-de-openai
OPENAI_MODEL=gpt-4
OPENAI_MAX_TOKENS=1000
OPENAI_TEMPERATURE=0.7

# CORS (separar origenes por coma, o * para todos)
CORS_ORIGINS=http://localhost:5173

# Rate Limiting
RATE_LIMIT_WINDOW_MS=900000
RATE_LIMIT_MAX_REQUESTS=100

# Server
PORT=3000
```

> **Nota**: La base de datos migró de SQL Server a PostgreSQL (Supabase). Todos los scripts están en `supabase/migrations/`.

## Frontend (.env)

Archivo: `frontend/.env`

```env
VITE_API_URL=http://localhost:3000
VITE_API_VERSION=v1
```

## Puertos

| Servicio | Puerto | URL |
|---|---|---|
| Backend API | 3000 | `http://localhost:3000/api/v1` |
| Swagger | 3000 | `http://localhost:3000/swagger` |
| Health Check | 3000 | `http://localhost:3000/health` |
| Frontend (Vite) | 5173 | `http://localhost:5173` |

## Headers Requeridos

Todas las peticiones autenticadas deben incluir:

```
Authorization: Bearer <jwt_token>
X-Tenant-ID: <company_id>      (respaldo, se extrae de JWT primero)
X-Branch-ID: <branch_id>       (respaldo, se extrae de JWT primero)
Content-Type: application/json
```

El frontend configura estos headers automáticamente en `src/config/api.js` usando interceptores de Axios, leyendo `token`, `tenantId` y `branchId` de `localStorage`.

## Autenticación para Desarrollo

El frontend tiene login UI funcional en `/login`. Credenciales de seed:
- **Email**: `admin@mibar.com`
- **Password**: `admin123`

```bash
# Login via API (obtener token)
curl -X POST http://localhost:3000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin@mibar.com","password":"admin123"}'

# Usar token en peticiones
curl http://localhost:3000/api/v1/inventory/products \
  -H "Authorization: Bearer <token>"
```

## Base de Datos

- **Motor**: PostgreSQL (Supabase)
- **Esquemas**: `core` (empresas, usuarios, roles), `inventory` (productos, stock, IA), `sales` (mesas, pedidos), `finance` (gastos/ingresos, templates recurrentes), `suppliers`, `delivery`, `audit`
- **Migraciones**: `supabase/migrations/` (ejecutar en orden numérico 001→800)

## Comandos Rápidos

```bash
# Backend
cd backend-dotnet
dotnet run --project src/Walos.API

# Frontend
cd frontend
npm run dev
```
# para cambiar usuario git ejecutar este comando: git config user.name "Walos App"
git config user.email "walosaap@gmail.com"