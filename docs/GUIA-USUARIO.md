# Guía de usuario de Walos

> Basada en pantallas y rutas del código local al 2026-10-04. No reemplaza UAT. Las opciones dependen de rol y funciones habilitadas para la empresa.

## Acceso y navegación

Ingresá por `/login`. El sistema dirige al área habilitada; los administradores de plataforma tienen acceso separado a tenants/empresas. No uses credenciales de ejemplo ni compartas tokens.

| Área | Ruta | Uso presente en código |
|---|---|---|
| Dashboard | `/` | Indicadores |
| Inventario | `/inventory` | Productos, existencias y movimientos |
| Alertas | `/alerts` | Alertas de inventario |
| Asistente | `/ai-assistant` | Conversación para operaciones asistidas |
| Restaurante | `/sales` | Mesas, cobro y revisión de ventas |
| POS directo | `/pos-deli` | Venta directa |
| Caja | `/cash` | Apertura, movimientos, cierre e historial |
| Finanzas | `/finance` | Movimientos y resumen financiero |
| Proveedores / compras | `/suppliers`, `/purchases` | Proveedores y órdenes |
| Delivery | `/delivery` | Gestión de pedidos |
| Usuarios / perfil | `/users`, `/profile` | Administración autorizada y perfil |
| Configuración | `/settings` | Opciones del comercio |

## Venta, caja y cobro

La pantalla de ventas integra barra de caja, apertura/cierre, panel de facturación, historial y resumen. El panel de cobro permite distribuir pagos entre métodos y manejar crédito; la disponibilidad concreta depende de las validaciones del backend y los permisos. No confundas una mesa cancelada, una devolución de venta y la cancelación de una deuda.

### Créditos

Desde ventas se abre el panel de créditos; **no existe una ruta independiente `/sales/credits` en el router**. Permite buscar por nombre, filtrar estado, consultar montos/pagos, registrar abonos y cancelar deuda. El total pendiente se calcula sobre los créditos cargados y filtrados, no es un reporte garantizado por día/semana/mes.

Cancelar un crédito marca la deuda como cancelada: **no revierte la venta ni devuelve stock**. Para una devolución se usa el flujo específico de devoluciones, sujeto a autorización.

El viejo documento `pending-credit-module.md` se retiró por duplicar especificación ya superada. Su propuesta de agregados por día/semana/mes y detalle de productos de la orden no se considera implementada: queda preservada como alcance por confirmar en [PENDING.md](../PENDING.md).

## Impresión y límites

Hay componentes de recibo, comanda y reporte Z, además del agente de impresión. La conexión/configuración del equipo debe validarse según [hardware POS](hardware-pos.md). No se verificó una impresora real en esta revisión.

- Un recibo POS no acredita emisión fiscal DIAN.
- Registrar un método de pago no demuestra procesamiento automático por Wompi.
- La PWA requiere red para llamar a la API; no se promete operación comercial offline.
- Una respuesta del asistente no sustituye la confirmación y validación de la operación.
