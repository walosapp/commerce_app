# Hardware POS: cierre H1 y plan H2

Fecha de cierre H1: 2026-09-14.

## Separacion de alcance

- **H1 (cerrado):** comunicacion local segura con la impresora termica Digital POS DIG-58IIA (cola Windows `POS-58`) y el cajon Digital POS DIG-4101. No interviene el cierre de venta ni la base de datos.
- **H2 (solo plan en este documento):** impresion manual de un recibo ya persistido desde `ReceiptPreview`.
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

Las rutas estan mapeadas en `tools/Walos.PrintAgent/Api/PrintAgentApi.cs:77-145`. El agente rechaza Origin ausente en rutas sensibles, `Origin: null` y origen no autorizado (`SecurityMiddleware.cs:9-39`); aplica Bearer a todas salvo health/pair (`SecurityMiddleware.cs:96-129`), 60 solicitudes/minuto globales y 10 pairings/minuto (`PrintAgentApi.cs:35-58`), y JSON estricto de hasta 8192 bytes (`PrintAgentApi.cs:12,23-27`; `SecurityMiddleware.cs:42-94`). Los contratos no permiten HTML, comandos ESC/POS ni bytes arbitrarios (`Contracts.cs:5-32`).

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

## Bloque H2 propuesto: recibo real manual desde `ReceiptPreview`

**Estado: preparado, no implementado.** H2 no abre el cajon y no se conecta al cierre de venta.

### Fuente canonica existente

No hace falta crear otro endpoint en el backend Walos:

1. `ReceiptPreview` recibe solamente `orderId` y ejecuta una query `['receipt', orderId]` (`frontend/src/modules/sales/components/ReceiptPreview.jsx:19-26`).
2. `printService.getReceipt(orderId)` ya consume `GET /api/v1/sales/orders/{orderId}/receipt` (`frontend/src/services/printService.js:3-8`).
3. `SalesController.GetReceipt` resuelve la sede en el tenant autenticado y llama `SalesService.GetReceiptAsync` (`backend-dotnet/src/Walos.API/Controllers/SalesController.cs:161-168`).
4. `SalesService.GetReceiptAsync` reconstruye el comprobante desde la orden, configuracion de empresa, items, pagos, mesa, cajero y credito persistidos (`backend-dotnet/src/Walos.Application/Services/SalesService.cs:303-357`).
5. El contrato canonico ya incluye `OrderId`, numero, fecha, items, descuentos, total pagado, propina, division, pagos y credito (`backend-dotnet/src/Walos.Application/DTOs/Sales/ReceiptDtos.cs:4-52`).
6. La consulta de orden esta aislada por `company_id` y, cuando existe, `branch_id`; los items tambien se limitan por esas claves (`SalesRepository.cs:246-299`).

Hoy `ReceiptPreview.handlePrint()` convierte ese DTO a HTML y abre el dialogo del navegador (`ReceiptPreview.jsx:28-87`). Se abre desde IDs persistidos del historial y del resumen (`OrderHistoryTab.jsx:237-280`; `SalesSummaryTab.jsx:230-235`). En cambio, el cierre inmediato no es un buen punto H2: `SalesPage.handleInvoice` descarta la respuesta (`SalesPage.jsx:255-259`) e `InvoiceResult` no expone `OrderId` (`ISalesService.cs:37-53`). Por eso H2 debe limitarse al boton manual de `ReceiptPreview`; no debe modificar `InvoicePanel`, `SalesPage` ni el checkout.

### Contrato local nuevo necesario

Los endpoints H1 no sirven para un recibo real: `test-print` genera un documento fijo y solo recibe `{ jobId }` (`PrintAgentApi.cs:110-125`; `PrintCommandService.cs:14-21`; `Contracts.cs:27`). H2 requiere exactamente un endpoint tipado nuevo:

`POST /v1/commands/print-receipt`

```json
{
  "jobId": "uuid-generado-por-click",
  "receipt": {
    "orderId": 123,
    "orderNumber": "ORD-001",
    "companyName": "Comercio",
    "companyLegalName": null,
    "companyPhone": null,
    "tableName": "Mesa 1",
    "tableNumber": 1,
    "createdAt": "2026-09-14T12:00:00Z",
    "cashierName": "Cajero",
    "items": [
      { "productName": "Producto", "quantity": 1, "unitPrice": 10000, "subtotal": 10000 }
    ],
    "subtotal": 10000,
    "discountType": null,
    "discountValue": 0,
    "discountAmount": 0,
    "finalTotalPaid": 10000,
    "tipAmount": 0,
    "tipIncluded": false,
    "splitCount": 1,
    "payments": [
      { "method": "cash", "amount": 10000, "reference": null }
    ],
    "hasCredit": false,
    "creditAmount": null,
    "creditCustomerName": null
  }
}
```

El frontend debe enviar el objeto `receipt` obtenido por la query actual, no datos del carrito ni de la respuesta de checkout. El agente debe aceptar solo este JSON tipado, con limites por campo/cantidad, montos finitos y no negativos, relaciones coherentes para credito/propina y miembros desconocidos rechazados. No debe aceptar `companyLogoUrl`, HTML, plantillas, comandos, code pages ni arreglos de bytes; el agente conserva control total del ESC/POS.

El limite de 8192 bytes de H1 puede ser insuficiente para una venta con muchos items. H2 debe definir un maximo todavia estricto (propuesta: **32 KiB solo para `print-receipt`**, manteniendo 8 KiB para pair/config/comandos simples) y limites adicionales de cantidad y longitud. No debe elevarse el limite sin tests de payload y memoria.

### Idempotencia H2

- Cada click manual crea un `jobId` nuevo; un reintento de transporte reutiliza exactamente ese mismo ID, como ya hace `printAgentStore.executeCommand` (`printAgentStore.js:195-230`).
- Una reimpresion humana posterior es un nuevo trabajo y usa otro ID.
- Antes de reservar, el agente calcula SHA-256 sobre una serializacion canonica del contrato tipado y guarda el fingerprint junto al job.
- Mismo `jobId` + mismo comando + mismo fingerprint: `replayed`, sin imprimir.
- Mismo `jobId` con otro payload, incluso si sigue siendo `print-receipt`: `409 job_id_conflict`.
- Se conserva la reserva durable antes de `WritePrinter` y la semantica at-most-once de H1.

El fingerprint es necesario porque el ledger H1 compara solo `jobId` y nombre del comando (`IdempotentJobExecutor.cs:35-50`); sin el hash, dos recibos diferentes con el mismo ID se tratarian incorrectamente como replay valido.

### Cambios H2 previstos

| Archivo | Cambio acotado |
|---|---|
| `tools/Walos.PrintAgent/Api/Contracts.cs` | DTOs locales tipados de recibo; sin HTML/bytes. |
| `tools/Walos.PrintAgent/Api/RequestValidation.cs` | Limites, coherencia y validacion profunda del comprobante. |
| `tools/Walos.PrintAgent/Api/PrintAgentApi.cs` | Mapear `POST /v1/commands/print-receipt` con las protecciones existentes y limite especifico. |
| `tools/Walos.PrintAgent/Printing/EscPos58Encoder.cs` | `EncodeReceipt(...)`: layout determinista de 32 columnas, items, descuentos, propina, pagos y credito. |
| `tools/Walos.PrintAgent/Commands/PrintCommandService.cs` | Preparar el ticket real usando la impresora local validada. No agregar drawer pulse. |
| `tools/Walos.PrintAgent/Commands/IdempotentJobExecutor.cs` | Asociar el job al fingerprint del payload. |
| `tools/Walos.PrintAgent/Storage/AgentStateStore.cs` | Persistir fingerprint compatible con jobs H1 ya guardados. No DB ni cloud. |
| `frontend/src/services/printAgentService.js` | `printReceipt(token, jobId, receipt)` con timeout explicito. |
| `frontend/src/stores/printAgentStore.js` | Accion manual `printReceipt(receipt)` y retry con el mismo ID. |
| `frontend/src/modules/sales/components/ReceiptPreview.jsx` | Boton de impresion directa usando el `receipt` cargado; conservar impresion por navegador como fallback manual separado. |

No se modifica `printService.getReceipt`, el backend Walos ni su base de datos: el endpoint persistido existente ya es la fuente. Tampoco se toca `SalesPage`, `InvoicePanel`, POS-Deli, cocina/comandas o Z.

### Comportamiento UI H2

1. Mostrar **Imprimir directo** solo sobre el recibo ya cargado.
2. Si el agente no esta vinculado o no tiene configuracion guardada, indicar `Configuracion -> Dispositivos`; no enviar nada.
3. Mantener **Imprimir con dialogo** como alternativa explicita.
4. No ejecutar automaticamente el fallback cuando haya timeout o resultado `uncertain`: el trabajo podria haber llegado al spooler y hacerlo causaria un duplicado.
5. Mostrar claramente `completed`, `replayed`, `failed` o `uncertain`. No cerrar el modal ante fallo incierto.
6. Nunca llamar `open-drawer`, ni siquiera cuando `payments` contenga efectivo.

### Tests H2 y gates

Tests del Print Agent:

- bytes/layout dorados para ticket real de 58 mm;
- español, moneda, cantidades decimales, descuentos, propina, pagos mixtos, credito y textos largos;
- sanitizacion de todos los campos externos;
- rechazo de HTML/control characters como instrucciones, bytes y miembros JSON desconocidos;
- recibo vacio, demasiados items/campos, montos invalidos y payload mayor al limite;
- impresora inexistente antes de consumir `jobId`;
- replay concurrente y despues de reinicio sin segundo `WritePrinter`;
- mismo `jobId` con payload diferente devuelve conflicto;
- `print-receipt` nunca produce bytes `ESC p` del cajon.

Tests frontend:

- `printAgentService` envia solo el DTO tipado y Bearer;
- el store reutiliza `jobId` solo en retry de transporte;
- `ReceiptPreview` envia el `receipt` devuelto por `printService.getReceipt(orderId)`;
- agente ausente/no vinculado/configuracion pendiente;
- estados completed/replayed/failed/uncertain;
- fallback de navegador es manual y un timeout no lo dispara;
- ninguna llamada a `openDrawer`.

Gates de H2:

```powershell
dotnet test .\tools\Walos.PrintAgent.Tests\Walos.PrintAgent.Tests.csproj -c Release
dotnet build .\tools\Walos.PrintAgent\Walos.PrintAgent.csproj -c Release
cd .\frontend
npm test -- --run
npm run build
cd ..
git diff --check
```

UAT H2: desde un `ReceiptPreview` de una orden persistida, imprimir una vez sin dialogo; repetir el mismo request/job y confirmar que no sale un segundo ticket; ejecutar una reimpresion manual con ID nuevo y confirmar que si sale; verificar fisicamente que el cajon permanece cerrado en todos los casos.

### Criterio de aprobacion H2

H2 sera aprobable solo cuando `ReceiptPreview` imprima el DTO persistido del endpoint existente, el agente mantenga control exclusivo de los bytes ESC/POS, retry no duplique, la reimpresion manual sea explicita, el cajon nunca se active y todos los gates mas la UAT fisica esten verdes.
