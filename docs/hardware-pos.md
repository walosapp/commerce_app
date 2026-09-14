# Hardware POS: cierre H1 e implementación H2

Fecha de cierre H1: 2026-09-14.

## Separacion de alcance

- **H1 (cerrado):** comunicacion local segura con la impresora termica Digital POS DIG-58IIA (cola Windows `POS-58`) y el cajon Digital POS DIG-4101. No interviene el cierre de venta ni la base de datos.
- **H2 (aprobado funcionalmente):** impresion manual de un recibo ya persistido desde `ReceiptPreview`, validada fisicamente con la cola Windows `POS-58`.
- **Fuera de H2:** apertura automatica del cajon, cierre POS, `InvoicePanel`, POS-Deli, impresion automatica de restaurante, comandas, reporte Z, migraciones, configuracion cloud, WebSocket y auto updater.

## H1 APROBADO

### Arquitectura implementada

| Pieza | Implementacion y evidencia |
|---|---|
| Agente Windows | `tools/Walos.PrintAgent/Walos.PrintAgent.csproj:1-11`: .NET 10 Windows, `WinExe` y WinForms. |
| Proceso | `tools/Walos.PrintAgent/Program.cs:10-37`: tray WinForms y Minimal API en el mismo proceso. |
| Red | `tools/Walos.PrintAgent/Program.cs:22-26`: Kestrel escucha solo `IPAddress.Loopback`; puerto `17831` en `PrintAgentApi.cs:11`. |
| Configuracion local | `%LOCALAPPDATA%\Walos\PrintAgent\agent-state.json`; ruta y persistencia atomica en `AgentStateStore.cs:6-12,43-47,197-214`. |
| Impresoras | Enumeracion de `PrinterSettings.InstalledPrinters` y validacion de existencia en `PrinterCatalog.cs:13-28`. |
| Spooler | Documento `RAW` mediante `OpenPrinterW`, `StartDocPrinterW`, `WritePrinter` y cierre de pagina/documento en `Win32RawPrinter.cs:30-95,108-133`. No usa dialogo de Windows. |
| ESC/POS 58 mm | CP858, inicializacion `ESC @`, 32 columnas, wrap y sanitizacion en `EscPos58Encoder.cs:7-43,63-160`. |
| Cajon | `ESC p m t1 t2`, pin 0/1 y conversion de milisegundos a unidades de 2 ms en `EscPos58Encoder.cs:46-60,143-146`. |
| UI | Disponible solo en `Configuracion -> Dispositivos`: `DevicesSettings.jsx:62-82`; operacion en `PrinterSettings.jsx`. |

### API local

Base URL fija: `http://127.0.0.1:17831` (`frontend/src/services/printAgentService.js:1`).

| Metodo y ruta | Funcion | Proteccion |
|---|---|---|
| `GET /v1/health` | Estado, version y `paired`. | Unico endpoint que permite ausencia de `Origin`; no requiere token. |
| `POST /v1/pair` | Vincula navegador con company, branch y workstation. | Origin permitido, codigo temporal y limitador especifico. |
| `GET /v1/printers` | Lista colas Windows e impresora seleccionada. | Origin permitido y Bearer. |
| `PUT /v1/config/printer` | Guarda impresora, identidad y pulso del cajon. | Origin permitido, Bearer, JSON tipado e identidad vinculada. |
| `POST /v1/commands/test-print` | Envia el ticket H1. | Origin permitido, Bearer y `jobId`. |
| `POST /v1/commands/open-drawer` | Envia el pulso del cajon. | Origin permitido, Bearer y `jobId`. |
| `POST /v1/commands/print-receipt` | Imprime un comprobante persistido tipado; nunca abre el cajon. | Origin permitido, Bearer, contexto vinculado, fingerprint e idempotencia durable. |

Las rutas estan mapeadas en `tools/Walos.PrintAgent/Api/PrintAgentApi.cs`. El agente rechaza Origin ausente en rutas sensibles, `Origin: null` y origen no autorizado (`SecurityMiddleware.cs`); aplica Bearer a todas salvo health/pair, 60 solicitudes/minuto globales y 10 pairings/minuto. H1 conserva JSON estricto de hasta 8192 bytes; solo `print-receipt` admite hasta 128 KiB por sus 100 lineas tipadas y texto UTF-8. Los contratos no permiten HTML, comandos ESC/POS ni bytes arbitrarios.

### Pairing y secreto

1. El tray muestra un codigo aleatorio de seis digitos, valido cinco minutos (`PairingService.cs:8-19,120-134`; `PrintAgentTrayContext.cs:17-37`).
2. `POST /v1/pair` lo consume una sola vez y entrega un Bearer aleatorio de 32 bytes (`PairingService.cs:55-82`).
3. La vinculacion queda asociada a `companyId`, `branchId` y `workstationId`; cambiar la identidad elimina la seleccion de impresora previa (`AgentStateStore.cs:57-64,69-93`).
4. El token se cifra con DPAPI, alcance `CurrentUser`, antes de persistirse (`TokenProtector.cs:12-35`). El navegador conserva su copia solo en `sessionStorage` (`printAgentStore.js:237-247`).

### Idempotencia y resultado fisico incierto

`jobId` admite entre 1 y 100 caracteres seguros (`RequestValidation.cs:40-44,64-68`). `IdempotentJobExecutor` serializa la ejecucion, valida antes de consumir el ID, persiste la reserva **antes** de tocar el spooler y nunca vuelve a ejecutar un ID reservado, completado o fallido (`IdempotentJobExecutor.cs:25-83`).

- Primera ejecucion correcta: `completed`, `executed: true`.
- Repeticion completada: `replayed`, `executed: false`.
- Reserva encontrada tras interrupcion: `uncertain`, `executed: false`.
- Fallo despues de reservar: `failed`; se consume el ID para no arriesgar un efecto fisico duplicado.
- Mismo ID para otro comando: `409 job_id_conflict`.

La garantia es **at-most-once durable**, no exactly-once fisico: el spooler puede aceptar el trabajo sin demostrar que salio papel o que el cajon se abrio.

### Ejecucion local

```powershell
cd C:\dev\Walos-app
dotnet restore .\tools\Walos.PrintAgent\Walos.PrintAgent.csproj
dotnet build .\tools\Walos.PrintAgent\Walos.PrintAgent.csproj -c Release --no-restore

$env:WALOS_PRINT_AGENT_ALLOWED_ORIGINS = "http://localhost:5173;http://127.0.0.1:5173"
dotnet run --project .\tools\Walos.PrintAgent\Walos.PrintAgent.csproj -c Release --no-build
```

Comprobacion de loopback:

```powershell
Get-NetTCPConnection -State Listen -LocalPort 17831
Invoke-RestMethod http://127.0.0.1:17831/v1/health
```

### Validacion

#### Gates automatizados previos al cierre

Estos resultados corresponden a la entrega tecnica anterior y deben distinguirse del rerun final previo al commit:

| Gate | Resultado previamente reportado |
|---|---:|
| Build Release Print Agent | Verde, 0 errores y 0 warnings |
| Tests Print Agent | 30/30 |
| Tests frontend | 46/46, 12 archivos |
| Build frontend | Verde; permanecieron advertencias preexistentes de CSS/chunks |
| `git diff --check` | Verde |
| Listener runtime | Solo `127.0.0.1:17831` |
| Health runtime | `status=ok` |

#### Rerun final previo al commit

Ejecucion realizada el 2026-09-14 entre las 13:46 y las 13:48 (UTC-05:00):

| Gate | Resultado final |
|---|---:|
| Build Release Print Agent | Verde, 0 errores y 0 warnings |
| Tests Print Agent Release | 30/30; 0 fallidos y 0 omitidos |
| Suite frontend completa | 46/46 en 12/12 archivos |
| Build frontend | Verde; permanecen warnings no bloqueantes de CSS, Browserslist y tamano de chunk |
| `git diff --check` | Verde; solo avisos de normalizacion LF/CRLF en archivos preexistentes |

Con este rerun final verde y la UAT fisica reportada a continuacion, el resultado de cierre es **H1 APROBADO**.

#### UAT fisico de aceptacion

**Evidencia aportada por el usuario el 2026-09-14; no fue ejecutada ni observada directamente por el agente de documentacion:**

- La cola `POS-58` imprimio el ticket RAW de 58 mm sin dialogo.
- El cajon DIG-4101 abrio correctamente con pin `0`.
- Primer `jobId`: `completed`, `executed: true`.
- Repeticion del mismo `jobId`: `replayed`, `executed: false`.
- El cajon no volvio a abrir fisicamente.

Con esos resultados, el estado funcional es **H1 APROBADO**.

### Riesgos tecnicos abiertos de H1

1. **Reconciliacion entre pestañas (medio):** el estado vive en `sessionStorage` por pestaña y no hay sincronizacion de un fingerprint completo de configuracion contra el agente (`printAgentStore.js:237-247`). Dos pestañas pueden mostrar estados locales diferentes.
2. **Ledger sin retencion y persistencia O(n) (medio):** cada job queda indefinidamente en `AgentState.Jobs`; cada cambio clona y vuelve a serializar toda la lista (`AgentStateStore.cs:23-30,110-169,188-214`). Debe incorporarse una politica de retencion/compactacion sin borrar jobs recientes o inciertos.

---

## Bloque H2: recibo real manual desde `ReceiptPreview`

**Estado: H2 APROBADO funcionalmente. H2 no abre el cajon y no se conecta al cierre de venta.**

### Fuente canonica y alcance

`ReceiptPreview` recibe solo `orderId`; `printService.getReceipt(orderId)` consume `GET /api/v1/sales/orders/{id}/receipt` y **Imprimir con Walos** usa exclusivamente ese `ReceiptData`. No usa carrito, HTML, checkout temporal ni POS-Deli.

El backend solo amplio el comprobante existente con `companyId`, `branchId`, `currency`, `timezone`, `status`, `refundStatus`, NIT y direccion. `CompanyRepository` lee columnas existentes, sin migracion. El credito se busca por `order_id + company_id + branch_id`, no por texto. `ReceiptData` conserva `creditStatus`, `creditOriginalTotal`, `creditAmountPaid` y `creditAmount`: un credito pagado mantiene saldo cero, uno parcial muestra el saldo actual y uno cancelado conserva su contabilidad pero se rotula expresamente como **NO VIGENTE / NO EXIGIBLE**. Para ordenes anteriores a `016_cash_registers.sql` sin filas en `sales.order_payments`, `GetReceiptAsync()` reconstruye un unico pago canonico solo desde `orders.payment_method` y `orders.final_total_paid`, sin referencia ni split inventados.

No existe un mensaje final configurable en `ReceiptData`; el ticket RAW no inventa uno. `ReceiptData` tampoco es snapshot fiscal inmutable: items, pagos y orden son persistidos, pero datos comerciales son los actuales y el saldo de credito cambia con abonos. H2 es impresion operativa, no facturacion electronica fiscal.

### Contrato local v1

`POST /v1/commands/print-receipt`

```json
{
  "documentVersion": 1,
  "jobId": "uuid-del-trabajo",
  "companyId": 25,
  "branchId": 7,
  "orderId": 123,
  "receipt": {
    "companyName": "Comercio",
    "companyLegalName": null,
    "companyPhone": null,
    "companyTaxId": "900123456-1",
    "companyAddress": "Calle 1",
    "currency": "COP",
    "timezone": "America/Bogota",
    "orderId": 123,
    "orderNumber": "ORD-123",
    "status": "completed",
    "refundStatus": null,
    "tableName": "Mostrador",
    "tableNumber": 1,
    "createdAt": "2026-09-14T17:30:00.000Z",
    "cashierName": "Maria",
    "items": [{ "productName": "Cafe", "quantity": 1, "unitPrice": 5000, "subtotal": 5000 }],
    "subtotal": 5000,
    "discountType": null,
    "discountValue": 0,
    "discountAmount": 0,
    "finalTotalPaid": 5000,
    "tipAmount": 0,
    "tipIncluded": false,
    "splitCount": 1,
    "payments": [{ "method": "cash", "amount": 5000, "reference": null }],
    "hasCredit": false,
    "creditStatus": null,
    "creditOriginalTotal": null,
    "creditAmountPaid": null,
    "creditAmount": null,
    "creditCustomerName": null
  },
  "fingerprint": "sha256-hex-en-minusculas"
}
```

Se rechazan version/casing/miembros desconocidos, IDs o rangos invalidos, HTML, controles, fechas no canonicas, moneda/zona horaria invalidas, mas de 100 items, mas de 20 pagos y aritmetica inconsistente. Solo se imprime `status=completed` sin `refundStatus`; frontend y agente bloquean canceladas o devueltas porque el DTO no tiene detalle para un ticket corregido.

El limite es **128 KiB solo para `print-receipt`**; H1 conserva 8 KiB. El test de frontera usa 100 nombres de 200 caracteres no ASCII y verifica que el peor payload UTF-8 valido supera 32 KiB pero permanece bajo 128 KiB.

### Totales impresos

- `TOTAL VENTA = subtotal - discountAmount`.
- `PAGADO VENTA = finalTotalPaid`; no se trata como total de venta.
- Propina incluida/no incluida se muestra separada, sin sumarla dos veces.
- Pagos multiples salen de `order_payments`; con propina incluida suman pagado de venta mas propina.
- Una venta a credito muestra monto original, abonos y saldo persistidos; `paid` se rotula saldado y `cancelled` se rotula cancelado/no exigible, nunca como deuda vigente.
- Moneda y hora provienen de `currency` y `timezone` persistidos.

### Fingerprint e idempotencia

Frontend y agente calculan SHA-256 sobre UTF-8 de JSON canonico del documento versionado: claves ordinales, arrays en orden, numeros normalizados y cadenas normalizadas a Unicode NFC. `jobId` y `fingerprint` quedan fuera. El agente recalcula y rechaza mismatch antes del spooler.

- mismo `jobId` + fingerprint: `replayed`, `executed=false`;
- mismo `jobId` + otro fingerprint/comando: `409 job_id_conflict`;
- reserva durable antes del spooler;
- reimpresion humana confirmada usa ID nuevo;
- ante transporte incierto o cualquier 5xx, el comando completo queda en `sessionStorage`; **Reintentar mismo trabajo** vuelve a consultar el recibo persistido y solo conserva el mismo job si company/branch/order y fingerprint siguen idénticos;
- si cambió estado, devolución, crédito, contenido o contexto, el payload viejo no se envía y el pendiente exige **Marcar intento como revisado**;
- solo un 4xx deterministico de preflight libera automaticamente el pendiente;
- respuestas `failed`/`uncertain` permanecen pendientes hasta replay seguro o la accion explicita **Marcar intento como revisado**, precedida por advertencia y confirmacion;
- nunca hay fallback automatico ante resultado incierto.

El SHA asegura que navegador y agente procesan igual payload. **No prueba criptograficamente que provenga del backend**: la procedencia depende de sesion Walos, Origin permitido y Bearer local. Firma fiscal queda fuera de H2.

### Seguridad y ESC/POS

Continuan loopback exclusivo, Origin permitido, rechazo de `Origin: null`, Bearer, rate limiting, JSON estricto y payload acotado. El frontend no envia HTML, plantillas, bytes ni ESC/POS. El agente genera CP858/32 columnas y `print-receipt` nunca contiene `ESC p`; reimprimir no abre cajon. Tanto la impresion directa como el fallback de navegador bloquean ordenes canceladas/devueltas, y el fallback tambien queda bloqueado mientras haya un resultado fisico incierto.

### Archivos H2

- Backend: `ReceiptDtos.cs`, `SalesService.cs`, `CompanySettings.cs`, `ICreditRepository.cs`, `CompanyRepository.cs`, `CreditRepository.cs` y tests afectados.
- Frontend: `receiptDocument.js`, `printAgentService.js`, `printAgentStore.js`, `ReceiptPreview.jsx` y tests H2.
- Agente: contratos/API/validacion, fingerprint, encoder, comando, ledger idempotente y tests H2.
- Sin migraciones, checkout, restaurante, comandas, cierre Z, WebSocket, cloud ni auto updater.

### Gates finales H2 (2026-09-14)

- Print Agent tests: 53/53 verdes.
- Frontend: 79/79 tests verdes en 15 archivos.
- Frontend build: verde; conserva avisos preexistentes de CSS, Browserslist y tamano de chunk.
- Backend afectado: 51/51 verdes: 40 `SalesServiceTests` y 11 integraciones de `CompanyRepository`/`CreditRepository`, ejecutadas contra PostgreSQL con la conexion inyectada solo en el proceso desde `.env`, sin exponer credenciales.
- Backend Release: verde, 0 errores y 0 advertencias.
- `git diff --check`: verde; solo avisos informativos de conversion LF/CRLF.

Estos resultados corresponden a los gates finales ejecutados despues de actualizar el estado de la UAT. La evidencia fisica de la seccion siguiente fue reportada por el usuario y no se confunde con la validacion automatica.

### UAT fisica H2 reportada (2026-09-14)

El usuario confirmo sobre hardware real:

- `ReceiptPreview` imprimio mediante Walos Print Agent en la cola `POS-58`;
- el ticket uso datos reales persistidos obtenidos desde el comprobante canonico del backend;
- el formato fisico de 58 mm fue valido;
- la DIG-4101 no abrio durante la impresion ni durante una reimpresion manual.

Queda pendiente como comprobacion manual menor y **no bloqueante** repetir desde DevTools exactamente el mismo request y `jobId`, y observar `replayed`, `executed=false` sin segundo ticket. La idempotencia equivalente permanece cubierta automaticamente; esta comprobacion pendiente no invalida la aprobacion funcional reportada.

### Criterio de aprobacion

H2 requiere gates verdes, impresion fisica del recibo persistido y cero aperturas de cajon durante impresion/reimpresion manual. El replay con el mismo `jobId` debe estar cubierto automaticamente; su repeticion manual desde DevTools se conserva como evidencia adicional no bloqueante.

**Estado actual: H2 APROBADO funcionalmente.**
