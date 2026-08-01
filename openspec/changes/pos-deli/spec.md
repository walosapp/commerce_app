# Spec: Módulo POS-Deli

> **Change ID**: pos-deli
> **Status**: draft
> **Version**: 1.0

---

## Requirements

### REQ-01: Vista POS fullscreen

El módulo DEBE presentar una vista de punto de venta en pantalla completa con:

- **Panel izquierdo**: búsqueda de productos, grid de favoritos/frecuentes, indicador de peso de báscula.
- **Panel derecho**: ticket/factura activa con lista de items, subtotales, total y botón de cobro.
- La vista NO DEBE tener concepto de mesas, meseros ni pedidos pendientes.

### REQ-02: Integración con báscula (Web Serial API)

El sistema DEBE conectarse a una báscula BBG SHOP POLE vía Web Serial API (RS-232) y:

- Leer el peso en tiempo real (modo continuo o por botón).
- Mostrar el peso actual en la interfaz.
- Distinguir peso estable vs inestable (solo usar peso estable para facturar).
- Solicitar permiso de conexión al puerto serial una sola vez por sesión.
- Mostrar estado de conexión de la báscula (conectada/desconectada/error).

### REQ-03: Lectura de código de barras

El sistema DEBE detectar input de lector de código de barras (HID keyboard) y:

- Identificar automáticamente un escaneo (input rápido < 50ms entre caracteres + Enter al final).
- Buscar el producto por barcode en inventario.
- Agregar el producto al ticket automáticamente con quantity=1.
- Si el producto ya está en el ticket, incrementar quantity.
- Si el barcode no se encuentra, mostrar notificación de error.

### REQ-04: Producto por peso

Cuando el cajero selecciona un producto pesable:

- El sistema DEBE tomar el peso actual de la báscula.
- DEBE calcular: `peso × precio_por_kg = subtotal`.
- DEBE agregar el item al ticket con peso, precio/kg, y subtotal.
- NO DEBE permitir agregar si el peso es 0 o la báscula está desconectada.

### REQ-05: Producto por unidad

Cuando el cajero selecciona un producto por unidad (click o barcode):

- El sistema DEBE agregar con quantity=1.
- Si se selecciona/escanea nuevamente, DEBE incrementar quantity.
- El cajero PUEDE modificar la cantidad manualmente.

### REQ-06: Ticket/Factura activa

El panel de factura DEBE:

- Mostrar lista de items con: nombre, cantidad/peso, precio unitario, subtotal.
- Calcular y mostrar total en tiempo real.
- Permitir eliminar items individuales.
- Permitir modificar cantidad de items por unidad.
- Persistir en memoria local mientras no se cierre (para evitar pérdida por refresh accidental).

### REQ-07: Cobro

El flujo de cobro DEBE:

- Usar los mismos métodos de pago que el módulo de ventas (efectivo, Nequi, tarjeta, etc.).
- Para efectivo: permitir ingresar monto recibido y calcular cambio.
- Al confirmar pago: cerrar la venta, descontar stock, generar registro.
- Limpiar el ticket y quedar listo para la siguiente venta.

### REQ-08: Descuento de stock

Al cerrar una venta, el sistema DEBE:

- Descontar stock de cada producto vendido en la sucursal activa.
- Para productos por peso: descontar el peso exacto (en la unidad del producto).
- Para productos por unidad: descontar la cantidad.
- Registrar movimiento de tipo `sale` con trazabilidad.

### REQ-09: Atajos de teclado

El módulo DEBE soportar atajos para operaciones frecuentes:

| Atajo | Acción |
|---|---|
| F12 | Cobrar (abrir modal de pago) |
| F1 | Enfocar búsqueda de producto |
| Escape | Cancelar modal activo |
| Delete | Eliminar item seleccionado del ticket |

### REQ-10: Impresión de ticket

Al cerrar una venta, el sistema DEBE poder imprimir un ticket con:

- Nombre del comercio y sucursal.
- Fecha/hora de la venta.
- Lista de items (nombre, peso/cantidad, precio unitario, subtotal).
- Total.
- Método de pago.
- Cambio (si aplica).

---

## Scenarios

### SC-01: Escaneo de producto por barcode

```gherkin
Given el POS-Deli está abierto y el ticket está vacío
When el cajero escanea un barcode "7702004003218" con el lector
Then el sistema busca el producto con ese barcode en inventario
And el producto "Coca-Cola 350ml" se agrega al ticket con quantity=1
And el total se actualiza a $3,200

Given el ticket ya tiene "Coca-Cola 350ml" con quantity=1
When el cajero escanea el mismo barcode "7702004003218"
Then la quantity se incrementa a 2
And el total se actualiza a $6,400
```

### SC-02: Producto pesado con báscula

```gherkin
Given la báscula está conectada y muestra peso estable "0.350 kg"
And el POS-Deli está abierto con ticket vacío
When el cajero hace click en el producto "Jamón Pietran" (precio: $28,500/kg)
Then el sistema lee el peso actual de la báscula (0.350 kg)
And agrega al ticket: "Jamón Pietran 0.350kg × $28,500/kg = $9,975"
And el total se actualiza a $9,975
```

### SC-03: Producto pesado con báscula en 0

```gherkin
Given la báscula está conectada y muestra peso "0.000 kg"
When el cajero hace click en un producto pesable "Queso Holandés"
Then el sistema muestra error "Coloca el producto en la báscula antes de agregar"
And NO se agrega nada al ticket
```

### SC-04: Báscula desconectada

```gherkin
Given la báscula NO está conectada (estado: desconectada)
When el cajero hace click en un producto pesable
Then el sistema muestra notificación "Báscula no conectada. Conecta la báscula para pesar productos."
And muestra opción "Ingresar peso manual" como fallback
```

### SC-05: Cobro en efectivo con cambio

```gherkin
Given el ticket tiene items con total $16,575
When el cajero presiona F12 (o click en COBRAR)
Then se abre el modal de pago

When el cajero selecciona "Efectivo" e ingresa $20,000
Then el sistema muestra "Cambio: $3,425"

When el cajero confirma el pago
Then la venta se registra en el backend
And el stock se descuenta por cada item
And el ticket se limpia
And el POS queda listo para la siguiente venta
```

### SC-06: Barcode no encontrado

```gherkin
Given el POS-Deli está abierto
When el cajero escanea un barcode "9999999999999" que no existe en inventario
Then el sistema muestra notificación "Producto no encontrado: 9999999999999"
And NO se agrega nada al ticket
```

### SC-07: Eliminar item del ticket

```gherkin
Given el ticket tiene 3 items: Jamón ($9,975), Coca-Cola ($3,200), Queso ($9,000)
And el total es $22,175
When el cajero selecciona "Coca-Cola" y presiona Delete
Then "Coca-Cola" se elimina del ticket
And el total se actualiza a $18,975
```

### SC-08: Conexión inicial de báscula

```gherkin
Given el usuario abre el módulo POS-Deli por primera vez
And nunca ha conectado la báscula en este browser
When el sistema solicita permiso para acceder al puerto serial
And el usuario selecciona el puerto de la báscula y concede acceso
Then el indicador de báscula cambia a "Conectada ✓"
And el peso en tiempo real comienza a mostrarse
```

### SC-09: Venta mixta (peso + unidad) completa

```gherkin
Given la báscula está conectada y estable
And el POS-Deli está abierto con ticket vacío

When el cajero coloca jamón en la báscula (peso: 0.350kg)
And hace click en "Jamón Pietran" ($28,500/kg)
Then se agrega: Jamón 0.350kg = $9,975

When el cajero coloca queso en la báscula (peso: 0.500kg)
And hace click en "Queso Holandés" ($18,000/kg)
Then se agrega: Queso 0.500kg = $9,000

When el cajero escanea barcode de "Coca-Cola 350ml" ($3,200)
Then se agrega: Coca-Cola x1 = $3,200

When el cajero presiona F12
And selecciona "Efectivo", ingresa $25,000
And confirma
Then la venta se registra con total $22,175
And cambio = $2,825
And stock de Jamón se descuenta 0.350kg
And stock de Queso se descuenta 0.500kg
And stock de Coca-Cola se descuenta 1 unidad
```

### SC-10: Peso manual como fallback

```gherkin
Given la báscula está desconectada
And el cajero necesita vender un producto pesable
When el cajero selecciona "Ingresar peso manual"
And escribe "0.250" en el campo de peso
And selecciona "Jamón Pietran"
Then se agrega: Jamón 0.250kg × $28,500/kg = $7,125
```
