# Proposal: Módulo POS-Deli (Punto de Venta para Salsamentaría)

> **Change ID**: pos-deli
> **Status**: proposed
> **Author**: Equipo Walos
> **Created**: 2026-08-01

---

## Intent

Crear un módulo de punto de venta optimizado para comercios tipo salsamentaría, que integre una báscula digital (BBG SHOP POLE, RS-232) para productos vendidos por peso y un lector de código de barras para productos por unidad. El módulo debe permitir transacciones extremadamente rápidas con mínima interacción del cajero.

## Problem Statement

El módulo de ventas actual (`sales/`) está diseñado para bares/restaurantes con concepto de mesas, meseros, y flujos multi-paso. Una salsamentaría necesita:

1. **Sin mesas**: venta directa mostrador → factura → cobro.
2. **Integración con báscula**: leer peso en tiempo real y calcular precio automáticamente.
3. **Barcode nativo**: escanear → agregar al ticket sin clicks adicionales.
4. **Velocidad extrema**: el cliente está de pie esperando, cada segundo cuenta.

El módulo actual no puede adaptarse sin contaminar su lógica con condicionales por tipo de negocio.

## Scope

### In Scope

- Nueva vista POS fullscreen optimizada para velocidad
- Integración con báscula BBG SHOP POLE vía Web Serial API (RS-232)
- Listener de código de barras (HID keyboard emulation)
- Panel de factura en tiempo real (agregar/quitar items)
- Grid de productos frecuentes (favoritos/pesables)
- Cobro con mismos métodos de pago del módulo ventas (efectivo, Nequi, tarjeta)
- Cálculo automático: peso × precio/kg = total del item
- Descuento de stock en inventario al facturar
- Backend endpoint para registrar venta rápida
- Atajos de teclado (F-keys) para operaciones frecuentes
- Impresión de ticket (reutilizar infraestructura de `printService`)

### Out of Scope (futuro)

- Sincronización de PLU con la báscula
- Múltiples básculas simultáneas
- Modo offline/PWA para este módulo
- Facturación electrónica DIAN
- Generalización multi-tenant (se construye específico primero)

## Approach

### Arquitectura

```
frontend/src/modules/pos-deli/     ← Nuevo módulo frontend
backend-dotnet/src/.../Controllers/PosDeliController.cs  ← Endpoint de venta
supabase/migrations/8XX_pos_deli.sql  ← Si necesita tablas propias (evaluar)
```

### Decisiones clave

| Decisión | Elección | Razón |
|---|---|---|
| ¿Módulo nuevo o extender Sales? | Nuevo módulo | Evita contaminar Sales con condicionales; flujo radicalmente distinto |
| ¿Cómo leer la báscula? | Web Serial API | Nativa en Chrome/Edge; no requiere instalar software extra |
| ¿Dónde vive el precio? | Inventario (nuestro DB) | No usamos PLU de la báscula; un solo source of truth |
| ¿Backend nuevo o reutilizar? | Endpoint nuevo + reutilizar services existentes | La facturación necesita su propio controller pero reutiliza `IInventoryRepository`, `ISalesRepository` |
| ¿Métodos de pago? | Mismos que Sales | Reutilizar `OrderPayment` entity y flujo de pagos |
| ¿Impresión? | Reutilizar `printService.js` | Ya existe infra de impresión de tickets |

### Conexión con báscula — Especificaciones técnicas

| Parámetro | Valor |
|---|---|
| Marca/Modelo | BBG SHOP POLE |
| Protocolo | RS-232 (serial) |
| Baud rate | 9600 |
| Data bits | 8 |
| Stop bits | 1 |
| Paridad | None |
| Rango | 6kg/15kg (d=2g/5g) |
| Conexión física | Cable RS-232 (DB9) → Adaptador USB-Serial → PC |
| API del browser | Web Serial API (navigator.serial) |

### Hardware requerido

- Báscula BBG SHOP POLE (ya disponible)
- Cable RS-232 (ya disponible)
- Adaptador USB a RS-232 (chip CH340 o FTDI) — **por adquirir**
- Lector de código de barras USB (actúa como teclado) — verificar disponibilidad

## Phases

| Fase | Entregable | Estimación | Dependencias |
|---|---|---|---|
| 1 | Vista POS-Deli (layout, grid, factura, barcode, modales) | 6-8h | Ninguna |
| 2 | Hook `useScale` (Web Serial API + parser BBG) | 3-4h | Adaptador USB-Serial |
| 3 | Backend endpoint `POST /pos-deli/sale` | 3-4h | Ninguna |
| 4 | Integración completa (peso → factura → cobro → stock) | 2-3h | Fases 1-3 |
| 5 | Impresión ticket + atajos teclado | 2-3h | Fase 4 |

**Total estimado**: 18-22 horas.

## Risks

| Riesgo | Probabilidad | Impacto | Mitigación |
|---|---|---|---|
| Formato serial de la báscula distinto al esperado | Media | Bajo | Parser configurable; ajuste en 5 min con datos reales |
| Web Serial API no disponible en el browser del cliente | Baja | Alto | Documentar requisito Chrome/Edge; fallback manual de peso |
| Adaptador USB-Serial incompatible | Baja | Medio | Recomendar modelos específicos probados |
| Latencia en lectura continua satura UI | Baja | Medio | Throttle de lectura a 200ms; solo actualizar en peso estable |

## Rollback Plan

El módulo es completamente aislado (nuevo directorio, nueva ruta, nuevo controller). Si falla:
1. Eliminar ruta en `App.jsx`
2. Eliminar directorio `frontend/src/modules/pos-deli/`
3. Eliminar `PosDeliController.cs`
4. Revertir migración SQL si existe

No afecta ningún módulo existente.

## Success Criteria

1. Cajero puede escanear un producto y verlo agregado al ticket en < 1 segundo
2. Cajero puede pesar un producto, seleccionarlo, y verlo con precio calculado en < 3 segundos
3. Flujo completo (pesar 3 productos + escanear 2 + cobrar) en < 30 segundos
4. Stock se descuenta correctamente al cerrar venta
5. Ticket se imprime con detalle de items (peso, precio/kg, subtotal)
