# Walos API

API REST **ASP.NET Core 8**, Dapper y **PostgreSQL/Npgsql**. No usa SQL Server como motor actual.

## Inicio local

Requiere SDK .NET 8 y una base PostgreSQL preparada de manera autorizada. La configuración se carga en `src/Walos.API/Program.cs`: busca `.env` junto al binario y, si no existe allí, en el directorio de trabajo. No se publican credenciales.

Desde `backend-dotnet/src/Walos.API`, configurá variables seguras según `.env.example` y ejecutá, solo cuando quieras iniciar el servicio:

```powershell
dotnet run
```

Este comando compila si hace falta; **no fue ejecutado en la revisión documental**. El puerto por defecto es 3000. Swagger: `/swagger`, solo con ambiente `Development`. Salud: `/health`.

## Estructura

- API: controladores, middleware y autorización.
- Application: servicios, DTOs y validadores.
- Domain: entidades e interfaces.
- Infrastructure: repositorios SQL e integraciones.
- `tests/Walos.Tests`: xUnit, Moq y pruebas de integración PostgreSQL.

Los contratos exactos están en `src/Walos.API/Controllers` y sus DTOs, no en listas de endpoints copiadas de auditorías. Incluyen auth, inventario, IA, ventas/POS, caja, créditos, devoluciones, finanzas, proveedores/compras, delivery, usuarios y plataforma.

## Seguridad de pruebas

`IntegrationTestBase` usa `ConnectionStrings:TestConnection` (incluido `appsettings.Test.json`) antes de `WALOS_TEST_CONNECTION`. Sin conexión, omite las pruebas de integración; esto NO es un pase de integración. Su preparación/limpieza escribe y elimina datos, por lo que requiere una base aislada y descartable verificada antes de ejecutar.

El comando de suite, desde este directorio, es `dotnet test tests/Walos.Tests/Walos.Tests.csproj`; compila por defecto. No se ejecutó y no hay resultado nuevo de tests o compilación.

Ver [guía técnica](../docs/GUIA-TECNICA.md), [arquitectura](../docs/architecture.md) y [migraciones](../supabase/migrations/README.md).
