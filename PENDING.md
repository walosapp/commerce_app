# PENDING — Backlog Operativo Walos

> **Fuente de verdad operativa actual** para trabajo pendiente.
>
> Este archivo NO documenta historia completa de implementación.
> La auditoría y evidencia viven en:
>
> - `docs/diagnostico-auditoria-walos.md`
> - `plan.md`
>
> **Estado**: `[ ]` pendiente | `[~]` en progreso | `[x]` completado
>
> **Prioridad**: `P0` crítico | `P1` alta | `P2` media | `P3` baja

---

## Estado actual del producto

### Módulos funcionales verificados en código
- [x] Auth con JWT + refresh
- [x] Dashboard
- [x] Inventario
- [x] Ventas / mesas / facturación
- [x] Finanzas
- [x] Configuración
- [x] Alertas
- [x] Proveedores
- [x] Delivery
- [x] Usuarios
- [x] Admin tenants / companies
- [x] Platform / billing / AI keys
- [x] Créditos
- [x] Caja / pagos / devoluciones

### Fuente de detalle técnico
Para estado real, riesgos y deuda usar:
- `docs/diagnostico-auditoria-walos.md`

---

## Backlog activo de remediación

> Ordenado para ejecución real. Primero seguridad, después confiabilidad, después consistencia estructural.

| Ref | Tarea | Estado | Prioridad | Fuente |
|-----|-------|--------|-----------|--------|
| R-01 | Restringir administración de plataforma por rol explícito | `[x]` | P0 | F4 / C-29 |
| R-02 | Eliminar override inseguro de branch desde `X-Branch-ID` | `[x]` | P0 | F4 / C-30 |
| R-03 | Reescribir `PENDING.md` como backlog confiable | `[~]` | P0 | F7 / C-33 |
| R-04 | Agregar tests críticos de autorización y tenant context | `[x]` | P0 | F6 / C-29 / C-30 |
| R-05 | Expandir tests de aislamiento company/branch | `[ ]` | P1 | F6 / C-31 |
| R-06 | Definir baseline mínima de coverage en áreas críticas | `[ ]` | P1 | F6 / C-32 |
| R-07 | Corregir `userService` admin hacia endpoints correctos | `[ ]` | P1 | F3 |
| R-08 | Normalizar naming camelCase en frontend (`firstName`, `lastName`) | `[ ]` | P1 | F5 / C-24 |
| R-09 | Mover componentes que llaman `api` directo hacia `services/` | `[ ]` | P1 | F5 / C-23 |
| R-10 | Reducir acoplamiento de UI al envelope bruto (`data.data`) | `[ ]` | P1 | F5 / C-25 |
| R-11 | Descargar responsabilidades de `InventoryController` | `[ ]` | P1 | F2 |
| R-12 | Limpiar ownership DI entre Application e Infrastructure | `[ ]` | P1 | F2 |
| R-13 | Reducir carga de `App.jsx` y entrypoints frontend | `[ ]` | P1 | F3 |
| R-14 | Resolver destino de `AdminUsersPage` y módulos huérfanos | `[ ]` | P1 | F3 |
| R-15 | Actualizar `backend-dotnet/README.md` | `[ ]` | P1 | F7 / C-34 |
| R-16 | Actualizar `frontend/README.md` | `[ ]` | P1 | F7 / C-35 |
| R-17 | Corregir `docs/architecture.md` separando estado actual vs ideal | `[ ]` | P1 | F7 / C-36 |
| R-18 | Reordenar docs `pending-*` que ya son historial implementado | `[ ]` | P2 | F7 / C-37 |
| R-19 | Marcar `docs/CODE_AUDIT_REPORT.md` como snapshot histórico | `[ ]` | P2 | F7 / C-38 |

---

## Fase actual

### Fase 2 — Fuente única de verdad / backlog

**Objetivo**
Recuperar confianza operativa en la documentación base para que el equipo no tome decisiones sobre información falsa.

### Alcance de esta fase
- [x] Limpiar contradicciones estructurales de `PENDING.md`
- [x] Convertir `PENDING.md` en backlog operativo real
- [ ] Verificación manual final de contenido
- [ ] Commit de fase al cierre

---

## Backlog de producto posterior a remediación

> Estas líneas NO son prioridad inmediata mientras existan P0/P1 de auditoría abiertos.

### Plataforma / arquitectura
- [ ] Logs estructurados por `companyId`, `branchId`, `userId` `P1`
- [ ] Auditoría de acciones sensibles `P1`
- [ ] Rate limit por tenant `P1`
- [ ] Estrategia de backup / restore por tenant `P2`

### Testing / calidad
- [ ] Más E2E para users, suppliers, settings, delivery `P1`
- [ ] Tests explícitos de permisos por rol `P1`

### Documentación
- [ ] Taxonomía clara entre backlog, historia implementada y reportes históricos `P2`

---

## Regla de uso

Si un estado o afirmación de este archivo contradice al código:

1. manda el código
2. corregí este archivo
3. documentá la diferencia en `docs/diagnostico-auditoria-walos.md` si corresponde

Ese es el estándar. No al revés.
