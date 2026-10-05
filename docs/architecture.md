# Arquitectura de Walos

> Revisión estática: 2026-10-04. Estado del código local, no validación operativa.

## Componentes y dependencias reales

- `backend-dotnet/src/Walos.API`: controladores, autenticación, autorización, middleware y composición (`Program.cs`).
- `Walos.Application`: servicios, DTOs, validadores; referencia a Domain.
- `Walos.Domain`: entidades e interfaces.
- `Walos.Infrastructure`: repositorios Dapper, Npgsql, inventario e integraciones; referencia a Domain **y Application**.
- `frontend/src`: módulos React, servicios HTTP, stores y componentes. `App.jsx` define las rutas reales.
- `supabase/migrations`: evolución SQL PostgreSQL. No hay que usar documentación antigua de SQL Server para configurar la aplicación.
- `tools`: agente de impresión e instalador; tiene su propio ciclo de release.

La separación de proyectos refleja una intención de capas; no implica que todos los controladores estén desacoplados ni que se haya certificado Clean Architecture.

## Petición y contexto de seguridad

`Program.cs` configura JWT, luego `TenantContextMiddleware`, autorización y `CompanyFeatureMiddleware`. El middleware tenant toma empresa, usuario, sucursal y rol de los claims autenticados; un header `X-Branch-ID` NO puede reemplazar la identidad de sucursal del JWT. La autorización de plataforma es distinta de la administración de un comercio.

El frontend aplica rutas protegidas y funciones por empresa. Esto mejora navegación, pero no sustituye controles del servidor. Los filtros SQL y las pruebas de aislamiento deben revisarse por operación; no se afirma aislamiento global certificado.

## Datos y ventas

`SqlConnectionFactory` conserva un nombre histórico pero construye una `NpgsqlConnection`. Los repositorios usan SQL explícito. Ventas, caja, créditos y devoluciones tienen servicios/repositorios especializados; `CheckoutRepository` y el módulo Infrastructure/Inventory participan del checkout. No debe extrapolarse atomicidad a todos los flujos sin revisar sus transacciones y pruebas.

## Frontend y PWA

React Router publica rutas de inventario, mesas, POS directo, caja, finanzas, proveedores/compras, delivery, usuarios, administración y configuración. Axios centraliza `/api/v1`, adjunta JWT y limpia sesión ante 401. Zustand mantiene estado y TanStack Query maneja consultas.

`vite.config.js` configura manifest y Workbox: recursos estáticos precacheados, API `NetworkOnly`. Instalable no equivale a operaciones de negocio offline.

## Evidencia y límites

Fuentes: `Program.cs`, middleware, `.csproj`, `SqlConnectionFactory.cs`, `frontend/src/App.jsx`, `config/api.js`, `vite.config.js`. Ver [guía técnica](GUIA-TECNICA.md) para ejecución y pruebas. Las auditorías fechadas se conservan como snapshots; sus conteos y cierres no son un gate actual.
