# Plan de Remediación por Fases — Walos

## Estado

La auditoría integral ya fue completada y quedó documentada en:

- `docs/diagnostico-auditoria-walos.md`

Este `plan.md` deja de ser plan de auditoría y pasa a ser el **roadmap de ejecución** para corregir hallazgos reales, por fases y con prioridad técnica.

---

## Objetivo

Resolver los hallazgos de auditoría de forma ordenada, empezando por lo que puede:

1. romper aislamiento o seguridad
2. permitir regresiones críticas
3. dañar mantenibilidad estructural
4. seguir propagando documentación falsa

---

## Regla de ejecución

No resolver “por comodidad visual” ni por lo más fácil.

El orden correcto es:

- primero **P0**
- después **P1 estructurales**
- después **P1 de consistencia**
- al final **P2/documentación residual**

---

## Fase 1 — Contención de seguridad crítica

**Objetivo:** cerrar los riesgos más peligrosos detectados en multi-tenant y autorización.

### Hallazgos que resuelve
- `F4-01` / `C-29` — `PlatformAdminController` sin restricción de rol suficiente
- `F4-02` / `C-30` — override de `X-Branch-ID` sin validación fuerte
- parte de `F6-03` — ausencia de tests sobre esos puntos críticos

### Trabajo
- Restringir `PlatformAdminController` por rol explícito
- Endurecer resolución de branch en `TenantContextMiddleware`
- Definir política clara para `X-Branch-ID`:
  - o se elimina el override
  - o se valida contra pertenencia/autorización real
- Agregar tests automáticos para:
  - acceso no autorizado a endpoints de plataforma
  - branch override inválido
  - comportamiento esperado del tenant context

### Criterio de cierre
- ningún usuario autenticado común puede tocar endpoints de plataforma
- no se puede inyectar branch arbitraria desde cliente sin validación
- existen tests que fallen si esto se rompe

### Prioridad
**P0**

---

## Fase 2 — Backlog y fuente única de verdad

**Objetivo:** evitar que el equipo tome decisiones sobre documentación falsa.

### Hallazgos que resuelve
- `F7-01` / `C-33` — `PENDING.md` no confiable

### Trabajo
- Reescribir o reemplazar `PENDING.md`
- Separar claramente:
  - backlog vigente
  - trabajo implementado
  - historial técnico
- Alinear backlog con el código real y con el diagnóstico de auditoría

### Criterio de cierre
- `PENDING.md` vuelve a ser utilizable como documento operativo real
- no hay módulos marcados como “pendientes” o “completados” de forma contradictoria

### Prioridad
**P0**

---

## Fase 3 — Red mínima de seguridad para cambios críticos

**Objetivo:** ampliar verificabilidad donde hoy más duele.

### Hallazgos que resuelve
- `F6-02` / `C-28` — cobertura frontend angosta
- `F6-04` / `C-31` — tenant tests demasiado estáticos
- `F6-05` / `C-32` — sin política clara de coverage/gates

### Trabajo
- Agregar tests de integración/comportamiento para company + branch isolation
- Expandir tests frontend en:
  - users
  - settings
  - inventory
  - suppliers
- Definir baseline mínima de coverage o al menos paquetes críticos obligatorios

### Criterio de cierre
- los flujos sensibles tienen protección automatizada real
- la suite deja de depender solo de validación manual para áreas críticas

### Prioridad
**P1**

---

## Fase 4 — Saneamiento de contratos y bugs funcionales concretos

**Objetivo:** corregir inconsistencias reales entre frontend y backend.

### Hallazgos que resuelve
- `F3-04` — `userService` admin usa endpoint tenant-scoped incorrecto
- `F5-02` / `C-24` — mismatch `firstName` vs `first_name`
- `F5-01` / `C-23` — componentes llamando `api` directo
- `F5-03` / `C-25` — UI demasiado acoplada al envelope bruto

### Trabajo
- Corregir `userService` admin para usar endpoints correctos
- Normalizar naming camelCase en frontend
- Mover accesos directos a `api` hacia `services/`
- Reducir lectura repetitiva de `data.data` desde componentes cuando tenga sentido

### Criterio de cierre
- no hay inconsistencias funcionales conocidas en servicios admin
- el contrato principal queda más centralizado y menos frágil

### Prioridad
**P1**

---

## Fase 5 — Consistencia arquitectónica backend

**Objetivo:** reducir mezcla de estilos y deuda estructural en backend.

### Hallazgos que resuelve
- `F2-*` principales
- `InventoryController` sobrecargado
- fuga de ownership en DI entre Application e Infrastructure

### Trabajo
- Reducir responsabilidades de `InventoryController`
- Mover contratos/repositorios a boundaries más coherentes cuando aplique
- Limpiar registro DI para que ownership arquitectónico sea más claro
- Revisar módulos donde controller aún habla demasiado directo con repositorios

### Criterio de cierre
- los módulos críticos siguen una convención más homogénea
- baja la mezcla entre estilos service-driven y repository-driven

### Prioridad
**P1**

---

## Fase 6 — Modularidad y deuda del frontend

**Objetivo:** preparar el frontend para seguir creciendo sin colapsar componentes principales.

### Hallazgos que resuelve
- `App.jsx` cargado
- páginas/componentes grandes
- estado/UI acoplados en zonas sensibles
- módulos huérfanos o mal rutados como `AdminUsersPage`

### Trabajo
- descargar `App.jsx`
- identificar y dividir páginas/components críticos
- decidir destino de `AdminUsersPage`
- endurecer convención de servicios y composición por módulo

### Criterio de cierre
- menos concentración de responsabilidades en entrypoints y pantallas grandes
- módulos mejor delimitados

### Prioridad
**P1**

---

## Fase 7 — Saneamiento documental estructural

**Objetivo:** alinear docs principales con el sistema real.

### Hallazgos que resuelve
- `C-34` — backend README stale
- `C-35` — frontend README stale
- `C-36` — `architecture.md` con drift
- `C-37` — docs `pending-*` mal nombrados
- `C-38` — `CODE_AUDIT_REPORT.md` sin framing histórico claro

### Trabajo
- actualizar READMEs principales
- corregir `docs/architecture.md`
- reorganizar docs “pending” vs docs históricos/implementados
- marcar explícitamente reportes históricos

### Criterio de cierre
- una persona nueva puede leer docs principales y entender el sistema sin llevarse una idea falsa

### Prioridad
**P1/P2**

---

## Orden de ejecución efectivo

1. **Fase 1 — Seguridad crítica**
2. **Fase 2 — Fuente única de verdad / backlog**
3. **Fase 3 — Testing crítico**
4. **Fase 4 — Contratos y bugs funcionales**
5. **Fase 5 — Backend estructural**
6. **Fase 6 — Frontend estructural**
7. **Fase 7 — Saneamiento documental**

---

## Estado actual

### Fase activa
**Fase 1 — Contención de seguridad crítica**

### Primer lote concreto
- endurecer `PlatformAdminController`
- revisar y corregir política de `X-Branch-ID`
- agregar tests que blinden ambos puntos

---

## Criterio global de cierre

Este plan se considera cerrado cuando:

- todos los **P0** estén resueltos y cubiertos por tests razonables
- los **P1** estructurales tengan remediación ejecutada o decisión explícita de arquitectura
- la documentación principal deje de contradecir al código
- el diagnóstico y el backlog queden alineados
