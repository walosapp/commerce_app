# Configuración de Conexiones - Walos

## Backend (.env)

El backend lee variables de entorno desde `backend-dotnet/src/Walos.API/.env`. Copiar `.env.example` como `.env` y configurar:

```env
# Base de Datos (SQL Server)
DB_CONNECTION_STRING=Server=localhost;Database=SCM_App_Track_Me;User Id=sa;Password=tu_password;TrustServerCertificate=true;Encrypt=true;

# JWT
JWT_SECRET=tu-clave-secreta-jwt-de-al-menos-32-caracteres
JWT_EXPIRES_MINUTES=60

# OpenAI
OPENAI_API_KEY=sk-tu-api-key-de-openai
OPENAI_MODEL=gpt-3.5-turbo
OPENAI_MAX_TOKENS=1000
OPENAI_TEMPERATURE=0.7

# CORS
CORS_ORIGINS=http://localhost:5173

# Server
PORT=3000
```

> **Importante**: La base de datos en desarrollo se llama `SCM_App_Track_Me`, no `WalosDB` como aparece en los scripts SQL.

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

```bash
# Login (obtener token)
curl -X POST http://localhost:3000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"dev","password":"1234"}'

# Usar token en peticiones
curl http://localhost:3000/api/v1/inventory/products \
  -H "Authorization: Bearer <token>"
```

En el frontend, configurar manualmente en la consola del navegador:
```javascript
localStorage.setItem('token', '<jwt_token>');
localStorage.setItem('tenantId', '1');
localStorage.setItem('branchId', '1');
```

## Base de Datos

- **Motor**: SQL Server
- **Nombre**: `SCM_App_Track_Me`
- **Esquemas**: `core` (empresas, usuarios), `inventory` (productos, stock, IA)
- **Scripts SQL**: `backend-dotnet/sql/` (ejecutar en orden numérico 001→800)

## Comandos Rápidos

```bash
# Backend
cd backend-dotnet
dotnet run --project src/Walos.API

# Frontend
cd frontend
npm run dev
```