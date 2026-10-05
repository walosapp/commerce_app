# Documentación: vigencia y alcance

Revisión documental: **2026-10-04**. Prevalecen código/contratos actuales sobre planes o snapshots. No hay certificación nueva de build, tests ni producción.

## Entrada vigente

- [Usuario](GUIA-USUARIO.md), [técnica](GUIA-TECNICA.md), [arquitectura](architecture.md).
- [Backend](../backend-dotnet/README.md), [frontend](../frontend/README.md).
- [Migraciones](../supabase/migrations/README.md): fuente de esquema, junto con SQL real.
- [Estilo](STYLE_GUIDE.md) y [temas](THEME_SYSTEM_IMPLEMENTATION_GUIDE.md): referencias de diseño, no evidencia de cobertura completa.
- [Hardware POS](hardware-pos.md): preservado por contener cambios locales anteriores a esta tarea.

## Referencias parciales, no contratos exhaustivos

`database-schema.md`, `ai-assistant-flow.md`, `finance-module.md` y `conexiones.md` son documentos heredados de alcance parcial. Para contratos exactos prevalecen migraciones, controladores, DTOs y configuración del código. `conexiones.md` no fue inspeccionado por precaución ante posibles credenciales: no se certifica su vigencia ni debe copiarse configuración sensible de allí.

## Planes y snapshots conservados

- `pending-payments-qr.md` y `pending-electronic-invoicing.md`: propuestas, no integraciones verificadas. Costos, condiciones de proveedores y requisitos externos requieren revisión independiente antes de usarlos.
- `pending-pos-gaps.md`, `fase3-execution-guide.md`, `pending-billing-ai-keys.md`: diseño/estado histórico; contienen tareas ya presentes y propuestas que no se deben ejecutar ciegamente.
- Auditorías, revisiones, cierres, contratos V1, `plan.md` y [PENDING.md](../PENDING.md): conservar evidencia y decisiones; sus casillas y conteos no prueban el estado actual. Los contratos siguen siendo referencias de intención; un snapshot no certifica implementación.
- Documentos no rastreados al iniciar esta tarea (refund, bloque nocturno y V2): trabajo previo preservado sin editar, borrar ni declarar aprobado.

## Documento retirado

`pending-credit-module.md` mezclaba un encabezado «implementado» con instrucciones futuras, un DTO ilustrativo y una ruta UI inexistente. Se eliminó tras consolidar el comportamiento observado en [guía de usuario](GUIA-USUARIO.md). El alcance no confirmado (agregados temporales y detalle de productos) quedó en el backlog. Git conserva su versión histórica; las auditorías que lo citaban incluyen una nota de retiro, sin alterar sus conclusiones originales.
