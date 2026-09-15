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
| `POST /v1/commands/open-drawer` | Envia el pulso del cajon. | Origin permitido, Bearer, `jobId`, `companyId` y `branchId` coincidentes con el pairing. |
| `POST /v1/commands/print-receipt` | Imprime un comprobante persistido tipado; nunca abre el cajon. | Origin permitido, Bearer, contexto vinculado, fingerprint e idempotencia durable. |

Las rutas estan mapeadas en `tools/Walos.PrintAgent/Api/PrintAgentApi.cs`. El agente rechaza Origin ausente en rutas sensibles, `Origin: null` y origen no autorizado (`SecurityMiddleware.cs`); aplica Bearer a todas salvo health/pair, 60 solicitudes/minuto globales y 10 pairings/minuto. H1 conserva JSON estricto de hasta 8192 bytes; solo `print-receipt` admite hasta 128 KiB por sus 100 lineas tipadas y texto UTF-8. Los contratos no permiten HTML, comandos ESC/POS ni bytes arbitrarios.

### Pairing y secreto

1. El tray muestra un codigo aleatorio de seis digitos, valido cinco minutos (`PairingService.cs:8-19,120-134`; `PrintAgentTrayContext.cs:17-37`).
2. `POST /v1/pair` lo consume una sola vez y entrega un Bearer aleatorio de 32 bytes (`PairingService.cs:55-82`).
3. La vinculacion queda asociada a `companyId`, `branchId` y `workstationId`; cambiar la identidad elimina la seleccion de impresora previa (`AgentStateStore.cs:57-64,69-93`).
4. El token se cifra con DPAPI, alcance `CurrentUser`, antes de persistirse en el agente (`TokenProtector.cs:12-35`). El navegador conserva su copia en `localStorage` como configuración de la estación (`printAgentStore.js:457-475`), para sobrevivir al cierre del navegador y al reinicio de Windows. Esto aumenta la exposición ante XSS porque cualquier JavaScript ejecutado en el origen Walos podría leer el Bearer persistido.

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

1. **Reconciliacion entre pestañas y XSS (medio):** `localStorage` comparte el estado persistido entre pestañas, pero las instancias ya abiertas no reconcilian automáticamente todo su estado en memoria ni un fingerprint completo de configuración contra el agente (`printAgentStore.js:457-475`). Dos pestañas pueden mostrar temporalmente estados diferentes; además, un XSS en cualquier pestaña del origen Walos podría leer el Bearer persistido.
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
- ante transporte incierto o cualquier 5xx, queda en `localStorage` únicamente la metadata del intento (`documentVersion`, `jobId`, contexto, `orderId` y fingerprint), nunca el recibo completo; **Reintentar mismo trabajo** vuelve a consultar el recibo persistido y solo conserva el mismo job si company/branch/order y fingerprint siguen idénticos;
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

---

## Bloque H3: impresión postventa y apertura automática de cajón

**Estado: H3 APROBADO físicamente.** La aprobación fue confirmada por el usuario después de ejecutar la UAT real; los escenarios de abajo quedan como guía de regresión.

### Flujo y límites

Restaurante y POS-Deli confirman primero la venta en backend. Solo después de recibir el `orderId` persistido cierran la captura, muestran **Venta registrada** y encolan acciones no bloqueantes. La cola vuelve a consultar exclusivamente `GET /api/v1/sales/orders/{id}/receipt`; no deriva productos, pagos ni decisión de efectivo desde carrito o estado temporal. Un fallo de consulta, impresión, almacenamiento local o cajón no relanza checkout ni revierte orden, pagos o inventario.

Las preferencias son opt-in, comienzan en `false` y se guardan localmente por la combinacion `companyId + branchId`. La politica aplicable se resuelve con esos IDs del `ReceiptData` canonico, no con una preferencia global. La cola serializa ventas rapidas. Cada intent confirmado usa clave `companyId + branchId + orderId` y persiste solamente job IDs, fingerprint y estados necesarios para reconciliarlo despues de recargar la pagina. La retencion limita a 100 solamente los intents terminales (`completed`, `replayed`, `skipped` o revisados): todos los pendientes, fallidos, inciertos o desactualizados sin revisar se conservan siempre. Si superan 100, la configuracion muestra una alerta y continua fail-closed; nunca los expulsa para recuperar espacio. Un replay de la misma orden tampoco puede sobrescribir un intent bloqueante aunque la politica local haya cambiado. Un fallo del probe, lectura o escritura de `localStorage` activa una barrera global fail-closed: no se encolan nuevas acciones H3 y `ReceiptPreview` bloquea tanto un job H2 nuevo como la impresion del navegador. La alerta queda visible en Configuracion -> Dispositivos y no se limpia durante la sesion; una recarga con storage todavia inaccesible vuelve a iniciar bloqueada. Tanto el alta `queued` como la transicion de retry a `retrying` vuelven a comprobar la barrera inmediatamente despues de persistir y abortan antes de consultar el recibo o ejecutar hardware si esa escritura fue la que fallo.

Antes de cualquier efecto se exige `status=completed` y ausencia de `refundStatus`. El cajón abre solo si `ReceiptData.payments` contiene una línea con `method` exactamente `cash` y `amount` numérico positivo. Tarjeta, transferencia, crédito sin efectivo, estado no elegible y recibo no disponible fallan cerrado.

### Acciones e idempotencia

- Impresión: `post-sale.v1.c{companyId}.b{branchId}.o{orderId}.receipt`.
- Cajón: `post-sale.v1.c{companyId}.b{branchId}.o{orderId}.drawer`.
- Ambos IDs son determinísticos, distintos y menores de 100 caracteres.
- Impresión y cajón son efectos independientes: un fallo de impresión no bloquea una apertura legítima por efectivo.
- Si la impresión responde `replayed`, H3 no solicita apertura de cajón.
- El endpoint `open-drawer` ya no admite un body inseguro con solo `jobId`: exige además `companyId` y `branchId`, y el agente los valida contra el pairing antes de preparar o enviar `ESC p`.
- La garantía at-most-once es durable **por agente/usuario Windows**. No existe coordinación cloud entre dos estaciones físicas distintas.

La reimpresión humana desde `ReceiptPreview` continúa siendo H2: usa un job nuevo y nunca abre cajón. H2 manual y H3 automático mantienen canales de estado separados, pero comparten una barrera de seguridad: mientras el print H3 esté pendiente o permanezca `failed`, `uncertain` o `stale` sin revisión, `ReceiptPreview` bloquea tanto un nuevo job Walos como `window.print()`. Un trabajo activo al recargar se rehidrata como `uncertain`, nunca como disponible. **Reintentar impresión postventa** vuelve a consultar el recibo canónico, compara contexto y fingerprint, y envía exclusivamente el mismo `receiptJobId`; nunca llama al cajón. Si el documento cambió, permanece bloqueado. Solo **Marcar intento postventa como revisado**, con confirmación humana, habilita una reimpresión H2 nueva. Los comandos físicos manuales y automáticos se serializan dentro de `printAgentStore`. El botón legacy de `InvoicePanel` permanece solamente como **Imprimir borrador con navegador** y marca el documento `BORRADOR - NO VÁLIDO COMO RECIBO`; no es el comprobante oficial postventa.

### Estados UX

La venta exitosa se informa antes del hardware. Después se notifican, sin bloquear el flujo, los resultados equivalentes a: **Recibo impreso**, **No fue posible imprimir**, **Cajón abierto** y **No fue posible abrir el cajón**. `completed` significa que el spooler aceptó el trabajo; no demuestra por sí solo que salió papel o que el cajón se movió físicamente.

### Gates H3 (2026-09-14)

| Gate | Resultado |
|---|---:|
| Print Agent tests Release | 55/55 verdes |
| Frontend suite completa | 143/143 en 20/20 archivos |
| Backend Release | Verde, 0 errores; 18 warnings preexistentes de nulabilidad/ocultamiento |
| Frontend build | Verde; conserva warnings preexistentes de CSS, Browserslist y tamaño de chunk |
| `git diff --check` | Verde; solo avisos informativos LF/CRLF |

### UAT física y guía de regresión

Precondición: recompilar y reiniciar Walos Print Agent H3, vincularlo con la empresa/sucursal correcta, seleccionar `POS-58` y habilitar en `Configuración -> Dispositivos` las dos opciones postventa.

1. **Restaurante + efectivo:** abrir una mesa, agregar un producto y facturar con una línea `cash` positiva. Confirmar que la venta aparece una sola vez en historial, el ticket H2 sale automáticamente sin diálogo y DIG-4101 abre una vez.
2. **Restaurante + tarjeta:** repetir con una venta diferente pagada solo con `card`. Confirmar un ticket automático y cero apertura del cajón.
3. **POS-Deli + efectivo:** registrar una venta nueva en efectivo. Confirmar una sola orden, un solo descuento de stock, un ticket automático y una sola apertura.
4. **Reimpresión desde historial:** abrir `ReceiptPreview` de cualquiera de las órdenes y pulsar **Imprimir con Walos**. Confirmar el mismo formato H2, un nuevo ticket y ninguna apertura de cajón.

Registrar por escenario: `orderId`, método(s) persistido(s), estados print/drawer, cantidad de tickets físicos y cantidad de aperturas. No usar ventas nuevas para simular replay.

### Criterio de aprobación H3

La UAT real confirmó los criterios funcionales de H3. Para futuras versiones se deben repetir impresión automática H2 en Restaurante y POS-Deli, efectivo abre una vez, tarjeta no abre, reimpresión manual nunca abre y los fallos de hardware no deben afectar la venta persistida ni duplicar ticket o apertura.

**H3 APROBADO físicamente.**

---

## Bloque H3.1: distribución mínima Windows V1

La distribución conserva el agente WinForms/tray validado: no lo convierte en servicio, no agrega puertos ni altera contratos H1/H2/H3. `Walos.PrintAgent` sigue escuchando exclusivamente en `127.0.0.1:17831` y guarda pairing, configuración e idempotencia en `%LOCALAPPDATA%\Walos\PrintAgent`.

### Instalador y actualización

- Proyecto: `tools/Walos.PrintAgent.Installer`.
- Instalador Inno Setup `.exe`, con `AppId` estable y registro en Agregar o quitar programas.
- Instalación normal elevada en `%ProgramFiles%\Walos\PrintAgent`.
- Publicación .NET 10 `win-x64`, Release, self-contained y single-file; el comercio no instala .NET ni usa PowerShell.
- Autoarranque machine-wide en `HKLM\Software\Microsoft\Windows\CurrentVersion\Run\WalosPrintAgent`.
- El tray inicia al finalizar mediante `runasoriginaluser`.
- Un mutex global impide instancias simultáneas; el instalador solicita cerrar solamente `Walos.PrintAgent.exe` mediante Restart Manager.
- Reinstalar o actualizar no toca `%LOCALAPPDATA%`; desinstalar conserva el estado por defecto.
- La desinstalación interactiva ofrece borrar explícitamente el estado del usuario actual. En modo silencioso únicamente `/CLEANCONFIG` solicita esa limpieza.
- V1 está diseñada para una terminal POS Windows monousuario. RDS/múltiples sesiones interactivas quedan fuera del alcance.

El pairing del navegador es configuración de la estación, no una sesión de usuario Walos, y se conserva en `localStorage` para sobrevivir al cierre del navegador y al reinicio. La copia del agente continúa protegida con DPAPI `CurrentUser`. Esta persistencia aumenta el impacto potencial de un XSS en el origen Walos; se mitiga manteniendo la lista de orígenes cerrada, el binding loopback y la validación Bearer, y debe reevaluarse si Walos adopta un mecanismo de credenciales HttpOnly para localhost.

El build usa Inno Setup 6.4.3 desde el paquete comunitario NuGet `Tools.InnoSetup`, fijado además por SHA-256; cada ejecución vuelve a extraer el compilador desde el paquete verificado y nunca confía ciegamente en un binario cacheado. Tanto `.toolchain` como `artifacts` están ignorados y no se versionan. El agente 1.0.1 siempre autoriza explícitamente `https://commerce-app-red.vercel.app`, `http://localhost:5173` y `http://127.0.0.1:5173`; no usa wildcard ni requiere configuración manual del comercio. `AllowedOrigins` es opcional y únicamente agrega orígenes HTTPS, sin reemplazar los canónicos. El archivo generado `appsettings.json` incorpora esa misma lista cerrada.

```powershell
.\tools\Walos.PrintAgent.Installer\Build-Installer.ps1 `
  -Version 1.0.1
```

El frontend obtiene el enlace público desde `VITE_WALOS_AGENT_DOWNLOAD_URL`; el instalador debe publicarse como asset HTTPS estable. H3.1 no agrega backend, updater ni infraestructura de releases nueva.

La estrategia V1 usa el repositorio existente `walosapp/commerce_app`: cada release publica el artefacto con el nombre estable `Walos-Agent-Setup.exe`, de modo que el frontend pueda apuntar a `https://github.com/walosapp/commerce_app/releases/latest/download/Walos-Agent-Setup.exe` sin incorporar una versión ni un backend nuevos.

### Validación de distribución

El smoke automatizado tiene una variante per-user separada y **no distribuible**. Se niega a correr si ya existe estado o autoarranque y debe usarse solo en cuenta/VM descartable. Valida instalación silenciosa y arranque postinstall, versión, `health`, binding loopback, reinstalación, preservación de estado centinela, comando de inicio registrado y desinstalación normal, pero no pretende demostrar UAC/Program Files/HKLM ni un logoff real.

La UAT elevada en VM limpia debe verificar por separado: instalación normal en Program Files; tray; detección desde Walos; preservación de pairing/configuración al reinstalar y al desinstalar sin limpieza; arranque después de logoff/login o reinicio; impresión; cajón; y limpieza únicamente cuando el usuario la elige.

### Gates H3.1 (2026-09-14)

| Gate | Resultado |
|---|---:|
| Print Agent tests Release | 72/72 verdes; CORS/origin focal 27/27 |
| Publish Print Agent | Verde: .NET 10, `win-x64`, self-contained y single-file |
| Frontend suite completa | 151/151 en 22/22 archivos |
| Frontend build | Verde; conserva warnings preexistentes de CSS, Browserslist y tamaño de chunk |
| Build del instalador | Verde con Inno Setup 6.4.3 verificado por SHA-256 |
| Parser PowerShell 5.1 | Verde para build y smoke |
| Instalación/reinstalación/desinstalación real | No ejecutada: la sesión no está elevada y el perfil contiene estado operativo |
| Logoff/reinicio real | Pendiente en VM/cuenta descartable |
| Authenticode | `NotSigned` |

Artefacto interno regenerado tras la corrección CORS: `tools/Walos.PrintAgent.Installer/artifacts/installer/Walos-Agent-Setup.exe`, 42.274.478 bytes, SHA-256 `EF7AB79E4C4969FBEB319E2921E9EB54C7CDBE6667B2D5AB1B7B74CF6F066AA3`. La versión de producto del instalador, binario y `health` es `1.0.1` (Windows representa `FileVersion` del agente como `1.0.1.0`). Su configuración embebida contiene el origin oficial y los dos origins locales, sin `*` ni el origin nominal obsoleto.

No se ejecutó el smoke contra el perfil operativo: `%LOCALAPPDATA%\Walos\PrintAgent\agent-state.json` ya contiene pairing/configuración real y la sesión no tiene privilegios para validar Program Files/HKLM. El estado se inspeccionó únicamente mediante hash y permaneció sin cambios. Tampoco se simuló un logoff/reinicio real.

El artefacto local queda `NotSigned`. Antes de distribución pública es obligatorio firmarlo con Authenticode y comprobar `Get-AuthenticodeSignature` con estado `Valid`. Hasta completar firma y UAT elevada real en una cuenta/VM limpia, el instalador sirve únicamente para validación interna.

**Estado: H3.1 implementado para corte de prueba; UAT del instalador en segundo equipo en curso y validación comercial final pendiente.**
