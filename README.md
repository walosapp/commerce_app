# Walos — gestión comercial

Aplicación web React y API ASP.NET Core para inventario, ventas de restaurante/POS, caja, finanzas y administración multiempresa.

> Documentación contrastada con el árbol de trabajo el **2026-10-04** (HEAD `6d22cfa`, con cambios locales previos). Describe código disponible, NO certifica compilación, tests, despliegue ni UAT. No se ejecutaron builds, tests ni conexiones a bases de datos en esta revisión.

## Documentación vigente

- [Guía de usuario](docs/GUIA-USUARIO.md): módulos, operación y límites.
- [Guía técnica](docs/GUIA-TECNICA.md): configuración, fuentes de verdad y validaciones.
- [Arquitectura](docs/architecture.md): estructura real y límites de seguridad.
- [Backend](backend-dotnet/README.md) / [Frontend](frontend/README.md): ejecución local.
- [Migraciones](supabase/migrations/README.md): esquema PostgreSQL y bootstrap opt-in.
- [Hardware POS](docs/hardware-pos.md): impresión y dispositivos; documento con trabajo local previo, conservado.
- [Índice y clasificación documental](docs/README.md).
- [Backlog](PENDING.md): remediaciones; los estados históricos requieren contraste antes de ejecutarlos.

## Capacidades presentes en código

Inventario y recetas; asistente IA; mesas y POS directo; caja; cobros y créditos; devoluciones; finanzas; proveedores y compras; delivery; usuarios; administración de tenants y funciones por empresa; configuración y billing de la plataforma.

La presencia de pantallas, controladores o pruebas **no significa que el módulo esté certificado como completo**. Permisos y funciones habilitadas condicionan el acceso. Los recibos de venta y el billing SaaS no acreditan facturación electrónica DIAN. Los planes de QR/Wompi y DIAN siguen siendo propuestas, no contratos implementados.

## Stack verificado

- Backend: .NET 8, ASP.NET Core, Dapper, Npgsql/PostgreSQL, JWT, FluentValidation, Serilog.
- Frontend: React 18, Vite 5, TailwindCSS, Zustand, TanStack Query, React Router.
- Pruebas: xUnit/Moq, Vitest/Testing Library y Playwright.
- PWA: manifest y Workbox; API con `NetworkOnly`, sin promesa de ventas offline.
- IA: integración OpenAI configurable; no se fija un modelo universal en la documentación.

## Inicio local

Seguí los READMEs de cada aplicación. No uses credenciales productivas ni ejecutes migraciones/bootstrap sin comprobar el destino. La API usa normalmente el puerto 3000 y Vite el 5173. Swagger se habilita únicamente en `Development`.
