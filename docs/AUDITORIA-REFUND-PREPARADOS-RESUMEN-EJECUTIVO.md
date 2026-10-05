# RESUMEN EJECUTIVO — AUDITORÍA REFUND DE PREPARADOS

> **Fecha**: 2025-01-15  
> **Documento completo**: `AUDITORIA-REFUND-PREPARADOS-INVENTARIO-HISTORICO.md`

---

## 🔴 BUG CONFIRMADO

El sistema **relee la receta vigente** al momento del refund, no la receta histórica usada en la venta original.

**Evidencia**:
- **Archivo**: `RefundRepository.cs`, líneas 684-695
- **Método**: `RestoreInventoryAsync`
- **Query problemático**:
  ```sql
  SELECT r.ingredient_id, r.quantity
  FROM inventory.recipes r
  WHERE r.product_id = @ProductId  -- ← Receta ACTUAL, no histórica
  ```

**Consecuencia**: Si la receta cambia entre venta y refund, se restituyen cantidades incorrectas.

---

## ✅ SOLUCIÓN RECOMENDADA

### REVERSIÓN DE MOVIMIENTOS ORIGINALES

**Fundamento**: El ledger `inventory.movements` **YA registra** todos los movimientos con:
- `product_id` (ingrediente)
- `quantity` (cantidad exacta)
- `reference_type` = `"order"`
- `reference_id` = `{order_id}`
- `movement_type` = `"recipe_consumption"`

**Cambio requerido**: Consultar movimientos originales en lugar de receta actual.

```csharp
// ANTES (INCORRECTO):
var ingredients = await connection.QueryAsync(@"
    SELECT r.ingredient_id, r.quantity
    FROM inventory.recipes r
    WHERE r.product_id = @ProductId");

// DESPUÉS (CORRECTO):
var originalMovements = await connection.QueryAsync(@"
    SELECT product_id, ABS(quantity) AS quantity
    FROM inventory.movements
    WHERE reference_type = 'order'
      AND reference_id = @OrderId
      AND movement_type = 'recipe_consumption'");
```

---

## 📊 COMPARACIÓN DE ALTERNATIVAS

| Criterio | Versionado Recetas | Snapshot Consumo | **Reversión Movimientos** ⭐ |
|----------|-------------------|------------------|------------------------------|
| **Precisión** | ✅ Alta | ✅ Alta | ✅ **Máxima** |
| **Complejidad** | ⚠️ Alta | ⚠️ Media | ✅ **Baja** |
| **Cambios schema** | ❌ Sí (2 tablas) | ❌ Sí (1 tabla) | ✅ **No** |
| **Storage adicional** | ⚠️ Alto | ⚠️ Medio | ✅ **Cero** |
| **Compatibilidad legacy** | ❌ Difícil | ⚠️ Media | ✅ **Alta** |
| **Refunds parciales** | ✅ Sí | ✅ Sí | ✅ **Sí** |
| **Mantenimiento** | ⚠️ Complejo | ⚠️ Medio | ✅ **Simple** |

---

## 🎯 VENTAJAS DE LA SOLUCIÓN RECOMENDADA

1. ✅ **Cero cambios de schema** — Sin migraciones
2. ✅ **Reutiliza infraestructura** — Ledger ya existe e indexado
3. ✅ **Compatibilidad total** — Fallback para ventas legacy
4. ✅ **Simplicidad** — ~230 líneas de código
5. ✅ **Auditoría completa** — Trazabilidad movimiento → refund
6. ✅ **Soporta todos los casos** — Parciales, múltiples, simples y preparados

---

## 📋 ESCENARIO DE REPRODUCCIÓN

### Configuración
```
Producto: "Hamburguesa" (ID: 100)
Receta V1: Carne 150g + Pan 1u
```

### Paso 1: Venta
```sql
-- Vender 2 hamburguesas
INSERT INTO inventory.movements (product_id, quantity, reference_id)
VALUES
    (10, -300, 5001),  -- Carne: 2 × 150g
    (20, -2,   5001);  -- Pan: 2 × 1u
```

### Paso 2: Cambio de receta
```
Receta V2: Carne 120g + Pan 1u + Queso 50g
```

### Paso 3: Refund (comportamiento actual — INCORRECTO)
```sql
-- Sistema actual relee receta V2 y restituye:
INSERT INTO inventory.movements (product_id, quantity, reference_id)
VALUES
    (10, +240, 8001),  -- ❌ 2 × 120g (debería ser +300)
    (20, +2,   8001),  -- ✅ 2 × 1u
    (30, +100, 8001);  -- ❌ 2 × 50g (NO se consumió en venta)
```

**Resultado**: Stock de carne queda 60g menos de lo correcto, queso 100g más.

### Paso 3: Refund (comportamiento correcto — PROPUESTO)
```sql
-- Sistema propuesto consulta movimientos originales:
SELECT product_id, ABS(quantity)
FROM inventory.movements
WHERE reference_type = 'order' AND reference_id = 5001;

-- Resultado: Carne 300g, Pan 2u

-- Refund restituye:
INSERT INTO inventory.movements (product_id, quantity, reference_id)
VALUES
    (10, +300, 8001),  -- ✅ Reversa movimiento original
    (20, +2,   8001);  -- ✅ Reversa movimiento original
```

**Resultado**: Stock correcto.

---

## 🔧 IMPLEMENTACIÓN

### Archivos a modificar
- `RefundRepository.cs` → `RestoreInventoryAsync` (~80 líneas)
- `RefundRepository.cs` → Nuevo método fallback (~30 líneas)
- `RefundAtomicityIntegrationTests.cs` → Tests (~120 líneas)

**Total**: ~230 líneas de código.

### Compatibilidad legacy
```csharp
var originalMovements = await GetOriginalMovements(orderId);

if (!originalMovements.Any())
{
    // Fallback para ventas sin movimientos
    _logger.LogWarning("No se encontraron movimientos originales. Usando receta actual.");
    originalMovements = await GetCurrentRecipeAsFallback(productId);
}
```

### Refunds parciales
```csharp
var refundRatio = refundedQuantity / originalQuantity;

foreach (var movement in originalMovements)
{
    var quantityToRestore = movement.Quantity * refundRatio;
    await RestoreTrackedProductAsync(movement.ProductId, quantityToRestore, ...);
}
```

---

## 📅 PLAN DE ROLLOUT

1. **Desarrollo** (1-2 días)
   - Implementar cambios en `RefundRepository`
   - Actualizar tests

2. **QA** (1 día)
   - Verificar refunds preparados
   - Verificar refunds parciales
   - Verificar fallback legacy

3. **Deploy** (1 hora)
   - Sin downtime
   - Sin migraciones
   - Monitoreo de logs

4. **Monitoreo** (1 semana)
   - Alertas en fallbacks
   - Validación de stock

---

## 🎯 RECOMENDACIÓN FINAL

## ⭐ RECOMENDADO: REVERSIÓN DE MOVIMIENTOS ORIGINALES

**Razón**: Máxima precisión, cero cambios de schema, compatibilidad total, simplicidad de implementación.

**Alternativas descartadas**:
- ❌ Versionado de recetas: Complejidad alta, storage adicional
- ❌ Snapshot de consumo: Duplicación de datos, migración compleja

---

**Documento completo**: `AUDITORIA-REFUND-PREPARADOS-INVENTARIO-HISTORICO.md`  
**Auditor**: Arquitectónico independiente  
**Fecha**: 2025-01-15
