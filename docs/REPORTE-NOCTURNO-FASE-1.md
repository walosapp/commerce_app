# Reporte nocturno Fase 1

Fecha: 2026-09-07

## 1. Baseline

- HEAD inicial: `1f2d348fb1ead866eeedeaa5fd552f86f1b49f9b`
- Working tree inicial: limpio.
- Backend Release: correcto, 0 errores y 18 warnings, 23.02 s.
- Tests backend: 474 total; 261 PASS; 0 FAIL; 213 SKIPPED; 7 s.
- Motivo de los skips: no había conexión PostgreSQL de pruebas disponible mediante
  `WALOS_TEST_CONNECTION` ni una configuración cargable desde el proceso de tests.

## 2. Bloque 1.3 — Idempotencia persistente POS-Deli

### Auditoría previa

- Endpoint: `POST /api/v1/pos-deli/sale`.
- Implementación: `PosDeliController.CreateSale()`.
- Request real: `PosDeliSaleRequest` con `Items`, `Payments`, `CashReceived` y `Notes`.
- Response real: `PosDeliSaleResponse` con `SaleId`, `TicketNumber`, `Total` y `Change`.
- `UnitPrice` e `IsWeighed` llegan desde el frontend, pero el backend no los usa para
  determinar la economía de la venta. Precio, nombre y subtotal se construyen con datos
  actuales de producto y `SaleItemPolicy`.
- El flujo previo ya usaba una sola conexión y una sola transacción para mesa, orden,
  items, pagos, caja, inventario y movimientos.
- No había idempotencia. Un retry sin key sigue creando una segunda venta durante la
  ventana de compatibilidad.
- `sales.orders.order_number` no tiene restricción UNIQUE y se genera con timestamp más
  un sufijo aleatorio.
- `InventoryTransactionWriter` reutiliza la conexión/transacción del caller y no cambia
  su ownership.

### Diseño elegido

Se mantuvo `sales.orders` como fuente de idempotencia de POS-Deli, sin crear una
plataforma universal ni una tabla adicional. La key queda acotada por empresa:

```sql
UNIQUE (company_id, idempotency_key)
WHERE idempotency_key IS NOT NULL
```

La sucursal efectiva forma parte del fingerprint. Por eso, reutilizar una key en otra
sucursal de la misma empresa es conflicto; otra empresa puede usar la misma key.

`Notes` se clasificó como dato decorativo y no participa en la intención económica.

### Schema

Migración nueva: `018_pos_sale_idempotency.sql`.

Columnas nullable agregadas a `sales.orders`:

- `idempotency_key VARCHAR(100)`;
- `request_fingerprint CHAR(64)`;
- `cash_received DECIMAL(18,2)`.

`cash_received` es necesaria para reconstruir exactamente `Change` sin consultar estado
mutable ni confiar en precios actuales. No se hizo backfill. Se agregaron checks de key,
SHA-256 hexadecimal y efectivo no negativo.

La migración 018 no colisiona: la estructural anterior es
`017_credit_refund_atomicity.sql`. No se renumeraron migraciones históricas.

### Fingerprint

`PosSaleIdempotencyPolicy` genera SHA-256 hexadecimal sobre una representación canónica
de:

- company y branch efectivos;
- productos y cantidades normalizadas, agrupados por producto y ordenados;
- pagos normalizados con `PaymentPolicy`, agrupados y ordenados;
- `nequi` canonizado contablemente como `transfer`;
- referencias de pago con codificación de longitud para evitar colisiones;
- `CashReceived` redondeado con la política monetaria canónica.

No incluye timestamps, IDs generados, número de orden, stock, precios actuales,
`UnitPrice`, `IsWeighed` ni `Notes`.

### Concurrencia y replay

- Lock: `pg_advisory_xact_lock` transaction-scoped.
- Key del lock: primeros 64 bits de SHA-256 sobre `company_id + idempotency_key`.
- Defensa secundaria: índice UNIQUE parcial en PostgreSQL.
- El lock y lookup de replay ocurren antes de consultar caja, productos, precios o stock.
- Same key + same fingerprint reconstruye la respuesta desde snapshots de
  `sales.orders` y no ejecuta mutaciones.
- Same key + different fingerprint responde 409 con código estable
  `idempotency_conflict`.
- Todo permanece en la única transacción preexistente. Un rollback no consume la key.

### Frontend

- La key se genera con `crypto.randomUUID()` y se persiste con el carrito en
  `sessionStorage`.
- Una mutación económica del carrito crea una nueva intención/key.
- Un fallo de red conserva payload y key para el retry de la misma intención.
- Al confirmar y limpiar el ticket se descarta la key.
- Se congela `payload + key` durante el submit y se bloquean mutaciones/scanner mientras
  la petición está pendiente.
- `posDeliService.createSale()` envía `Idempotency-Key`.

Compatibilidad de transición:

1. DB nueva + backend viejo: compatible por columnas nullable.
2. Backend nuevo + DB nueva: diseño objetivo.
3. Frontend viejo + backend nuevo: permitido temporalmente; ausencia de key conserva el
   comportamiento anterior.
4. Frontend nuevo + backend viejo: el header es ignorado y NO ofrece protección.

Orden de despliegue futuro recomendado: migración 018, backend, frontend y, en un bloque
posterior, hacer obligatorio el header.

### Tests agregados

Unitarios backend:

- estabilidad ante reordenamiento;
- agrupación de productos repetidos;
- alias `nequi -> transfer`;
- cambios de cantidad, producto, método, monto y efectivo;
- precisión inválida;
- key de advisory lock estable y tenant-scoped;
- validación de `Idempotency-Key`.

Integración PostgreSQL:

- creación y replay exacto de `SaleId`, `TicketNumber`, `Total` y `Change`;
- no repetición de order, items, payments, movements, stock ni caja;
- conflictos por cantidad, producto, método y monto;
- key distinta con payload igual;
- canonicalización por orden y alias;
- aislamiento company/branch;
- reutilización de key tras rollback de inventario, movement, payment y cash;
- concurrencia real coordinada con advisory lock, sin `Thread.Sleep`.

Frontend:

- lifecycle de key ante retry, cambio de intención y venta confirmada;
- envío del header y compatibilidad sin key.

### Resultado del checkpoint

| Suite | Total | Pass | Fail | Skip | Resultado |
|---|---:|---:|---:|---:|---|
| Backend sin PostgreSQL | 497 | 272 | 0 | 225 | Verde con integración omitida |
| Frontend Vitest | 37 | 37 | 0 | 0 | Verde |
| POS PostgreSQL explícito | 42 | 0 | 42 | 0 | BLOCKER de infraestructura |

Duraciones:

- `dotnet test --no-restore`: 13.38 s de pared; 4 s reportados por runner.
- `npm test -- --run`: 14.15 s de pared; 11.36 s reportados por runner.
- Intento POS PostgreSQL: 15.00 s; ninguna prueba alcanzó el cuerpo porque la conexión
  configurada para `walos_test` fue rechazada por autenticación.
- `dotnet build -c Release --no-restore`: 8.73 s; 0 errores, 18 warnings.
- `npm run build`: 16.66 s; correcto con warnings preexistentes de CSS, Browserslist y
  tamaño de chunk.
- `git diff --check`: correcto; solo avisos de normalización LF/CRLF.

La máquina tiene servicios PostgreSQL locales, pero la única credencial explícita de
tests no autentica. No hay Docker, Podman ni `psql` para aprovisionar de forma segura una
instancia efímera. No se apuntaron tests a otra base ni se modificaron secretos.

### Commit

**No creado.** El bloque no cumple el gate de integración/concurrencia PostgreSQL real.
La implementación queda en working tree para diagnóstico y validación posterior.

## 3. Bloque 1.4

No ejecutado. El Bloque 1.3 no quedó completamente verde por falta de PostgreSQL de
pruebas autenticable.

## 4. Bloque 1.4B

No ejecutado.

## 5. Build

- Backend Release: correcto; 0 errores, 18 warnings ya presentes en el baseline.
- Frontend: correcto.
- Warnings frontend: dato Browserslist desactualizado, error CSS preexistente `-: TZ.;`
  y bundle principal superior a 500 kB.

## 6. Migraciones

- `018_pos_sale_idempotency.sql` — creada, no ejecutada en producción.
- Sintaxis y comportamiento real no validados en PostgreSQL por el blocker de conexión.
- Persisten duplicados históricos 009 y 010; no se tocaron.

## 7. Archivos modificados — Bloque 1.3

- `backend-dotnet/src/Walos.API/Controllers/PosDeliController.cs`
- `backend-dotnet/src/Walos.Domain/Policies/PosSaleIdempotencyPolicy.cs`
- `backend-dotnet/tests/Walos.Tests/Policies/PosSaleIdempotencyPolicyTests.cs`
- `backend-dotnet/tests/Walos.Tests/Integration/PosDeliCashRegisterIntegrationTests.cs`
- `frontend/src/modules/pos-deli/PosDeliPage.jsx`
- `frontend/src/modules/pos-deli/stores/posDeliStore.js`
- `frontend/src/services/posDeliService.js`
- `frontend/src/test/stores/posDeliStore.test.js`
- `frontend/src/test/services/posDeliService.test.js`
- `supabase/migrations/018_pos_sale_idempotency.sql`
- `docs/REPORTE-NOCTURNO-FASE-1.md`

## 8. Riesgos

### BLOCKER

- No se pudo validar migración, replay, rollback ni concurrencia contra PostgreSQL real:
  la conexión local de tests falla por autenticación y no hay runtime de contenedores.

### HIGH

- Frontend nuevo + backend viejo acepta la venta, pero el backend viejo ignora la key.
- Backend nuevo requiere que la migración 018 se aplique antes del despliegue.

### MEDIUM

- El header sigue opcional durante la transición; clientes viejos continúan sin
  protección idempotente.
- `order_number` y el número aleatorio de mesa siguen sin UNIQUE; la idempotencia no
  depende de ellos, pero conservan riesgo propio de colisión.

### LOW

- Warnings preexistentes de backend y frontend no se atendieron por estar fuera del
  alcance del bloque.

## 9. Deuda técnica comprobada

- Hacer obligatorio `Idempotency-Key` después de completar el rollout compatible.
- Proveer un PostgreSQL efímero reproducible para ejecutar la suite de integración.
- Resolver en otro bloque la ausencia de UNIQUE para `order_number` si el contrato lo
  requiere.

## 10. Estado final

**SESIÓN NOCTURNA NO APROBABLE**

La implementación compila y las suites ejecutables están verdes, pero el requisito
central de concurrencia/rollback PostgreSQL no pudo ejecutarse. Conforme a los STOP
CONDITIONS, no se avanzó a 1.4, no se creó commit y no se hizo push.
