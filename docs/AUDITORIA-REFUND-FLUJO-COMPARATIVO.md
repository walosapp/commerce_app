# DIAGRAMA COMPARATIVO — FLUJO DE REFUND

> **Auditoría**: Refund de preparados e inventario histórico  
> **Fecha**: 2025-01-15

---

## FLUJO ACTUAL (INCORRECTO) ❌

```
┌─────────────────────────────────────────────────────────────────┐
│ VENTA — Día 1                                                   │
└─────────────────────────────────────────────────────────────────┘

Usuario vende 2 "Hamburguesas"
         │
         ├─► CheckoutRepository.ProcessAsync
         │
         ├─► SaleInventoryPlanBuilder.BuildAsync
         │        │
         │        ├─► Query: SELECT * FROM inventory.recipes
         │        │          WHERE product_id = 100  -- Hamburguesa
         │        │
         │        └─► Resultado (Receta V1):
         │                 - Carne (ID: 10): 150g × 2 = 300g
         │                 - Pan (ID: 20): 1u × 2 = 2u
         │
         ├─► InventoryTransactionWriter.ApplyAsync
         │        │
         │        └─► INSERT INTO inventory.movements:
         │                 (10, 'recipe_consumption', -300, 'order', 5001)
         │                 (20, 'recipe_consumption', -2,   'order', 5001)
         │
         └─► Stock actualizado:
                  Carne: 1000g → 700g
                  Pan: 50u → 48u

┌─────────────────────────────────────────────────────────────────┐
│ CAMBIO DE RECETA — Día 5                                        │
└─────────────────────────────────────────────────────────────────┘

Chef modifica receta:
         │
         └─► UPDATE inventory.recipes
                  SET quantity = 120  -- Carne (antes 150g)
                  WHERE product_id = 100 AND ingredient_id = 10;
             
             INSERT INTO inventory.recipes
                  (product_id, ingredient_id, quantity)
                  VALUES (100, 30, 50);  -- Queso (nuevo)

             Receta V2:
                  - Carne: 120g
                  - Pan: 1u
                  - Queso: 50g  ← NUEVO

┌─────────────────────────────────────────────────────────────────┐
│ REFUND — Día 10  ❌ BUG AQUÍ                                    │
└─────────────────────────────────────────────────────────────────┘

Usuario devuelve 1 hamburguesa
         │
         ├─► RefundRepository.ProcessAsync
         │
         ├─► RestoreInventoryAsync
         │        │
         │        ├─► Query: SELECT * FROM inventory.recipes
         │        │          WHERE product_id = 100  -- ❌ RECETA ACTUAL (V2)
         │        │
         │        └─► Resultado (Receta V2):  ❌ INCORRECTO
         │                 - Carne: 120g
         │                 - Pan: 1u
         │                 - Queso: 50g
         │
         └─► INSERT INTO inventory.movements:
                  (10, 'refund_recipe', +120, 'refund', 8001)  ❌ Debería ser +150
                  (20, 'refund_recipe', +1,   'refund', 8001)  ✅ Correcto
                  (30, 'refund_recipe', +50,  'refund', 8001)  ❌ NO se consumió

Stock final (INCORRECTO):
         Carne: 700g + 120g = 820g  ❌ Debería ser 850g (falta 30g)
         Pan: 48u + 1u = 49u        ✅ Correcto
         Queso: 200g + 50g = 250g   ❌ Debería ser 200g (sobra 50g)
```

---

## FLUJO PROPUESTO (CORRECTO) ✅

```
┌─────────────────────────────────────────────────────────────────┐
│ VENTA — Día 1  (sin cambios)                                    │
└─────────────────────────────────────────────────────────────────┘

Usuario vende 2 "Hamburguesas"
         │
         ├─► CheckoutRepository.ProcessAsync
         │
         ├─► SaleInventoryPlanBuilder.BuildAsync
         │        │
         │        ├─► Query: SELECT * FROM inventory.recipes
         │        │          WHERE product_id = 100
         │        │
         │        └─► Resultado (Receta V1):
         │                 - Carne: 150g × 2 = 300g
         │                 - Pan: 1u × 2 = 2u
         │
         ├─► InventoryTransactionWriter.ApplyAsync
         │        │
         │        └─► INSERT INTO inventory.movements:
         │                 (10, 'recipe_consumption', -300, 'order', 5001)
         │                 (20, 'recipe_consumption', -2,   'order', 5001)
         │                      ▲                              ▲
         │                      │                              │
         │                      └──────────────────────────────┴─ ⭐ CLAVE: reference_id
         │
         └─► Stock actualizado:
                  Carne: 1000g → 700g
                  Pan: 50u → 48u

┌─────────────────────────────────────────────────────────────────┐
│ CAMBIO DE RECETA — Día 5  (sin cambios)                         │
└─────────────────────────────────────────────────────────────────┘

Chef modifica receta:
         │
         └─► UPDATE inventory.recipes ...
             
             Receta V2:
                  - Carne: 120g
                  - Pan: 1u
                  - Queso: 50g

┌─────────────────────────────────────────────────────────────────┐
│ REFUND — Día 10  ✅ SOLUCIÓN PROPUESTA                          │
└─────────────────────────────────────────────────────────────────┘

Usuario devuelve 1 hamburguesa
         │
         ├─► RefundRepository.ProcessAsync
         │
         ├─► RestoreInventoryAsync (MODIFICADO)
         │        │
         │        ├─► Query: SELECT product_id, ABS(quantity)
         │        │          FROM inventory.movements
         │        │          WHERE reference_type = 'order'
         │        │            AND reference_id = 5001  ← ⭐ Orden original
         │        │            AND movement_type = 'recipe_consumption'
         │        │
         │        ├─► Resultado (Movimientos originales):  ✅ CORRECTO
         │        │        - Carne (10): 300g
         │        │        - Pan (20): 2u
         │        │
         │        └─► Calcular proporción:
         │                 refundRatio = 1 / 2 = 0.5
         │                 Carne: 300g × 0.5 = 150g
         │                 Pan: 2u × 0.5 = 1u
         │
         └─► INSERT INTO inventory.movements:
                  (10, 'refund_recipe', +150, 'refund', 8001)  ✅ Correcto
                  (20, 'refund_recipe', +1,   'refund', 8001)  ✅ Correcto

Stock final (CORRECTO):
         Carne: 700g + 150g = 850g  ✅ Correcto
         Pan: 48u + 1u = 49u        ✅ Correcto
         Queso: 200g                ✅ Sin cambios (correcto)
```

---

## COMPARACIÓN LADO A LADO

| Aspecto | Flujo Actual ❌ | Flujo Propuesto ✅ |
|---------|----------------|-------------------|
| **Query en refund** | `SELECT * FROM recipes WHERE product_id = ?` | `SELECT * FROM movements WHERE reference_id = ?` |
| **Fuente de verdad** | Receta vigente (mutable) | Movimientos históricos (inmutables) |
| **Precisión** | ❌ Incorrecta si receta cambió | ✅ Siempre correcta |
| **Cambios de schema** | Ninguno | Ninguno |
| **Complejidad** | Simple pero incorrecta | Simple y correcta |
| **Compatibilidad legacy** | N/A | ✅ Fallback a receta actual |

---

## CASOS EDGE

### Caso 1: Venta legacy sin movimientos

```
┌─────────────────────────────────────────────────────────────────┐
│ REFUND DE VENTA LEGACY                                          │
└─────────────────────────────────────────────────────────────────┘

Usuario devuelve producto de venta antigua
         │
         ├─► RestoreInventoryAsync
         │        │
         │        ├─► Query: SELECT * FROM movements
         │        │          WHERE reference_id = 1001
         │        │
         │        └─► Resultado: VACÍO (venta pre-ledger)
         │
         ├─► Fallback activado:
         │        │
         │        ├─► _logger.LogWarning("No se encontraron movimientos...")
         │        │
         │        └─► Query: SELECT * FROM recipes
         │                   WHERE product_id = 100
         │
         └─► Comportamiento: Igual al actual (receta vigente)
                             ⚠️ Puede ser incorrecto, pero no empeora
```

### Caso 2: Refund parcial

```
┌─────────────────────────────────────────────────────────────────┐
│ REFUND PARCIAL                                                  │
└─────────────────────────────────────────────────────────────────┘

Venta: 4 hamburguesas
Movimientos originales:
         - Carne: -600g (4 × 150g)
         - Pan: -4u (4 × 1u)

Refund: 1 hamburguesa
         │
         ├─► refundRatio = 1 / 4 = 0.25
         │
         └─► Restitución:
                  - Carne: 600g × 0.25 = 150g  ✅
                  - Pan: 4u × 0.25 = 1u        ✅
```

### Caso 3: Múltiples refunds parciales

```
┌─────────────────────────────────────────────────────────────────┐
│ MÚLTIPLES REFUNDS PARCIALES                                     │
└─────────────────────────────────────────────────────────────────┘

Venta: 4 hamburguesas
Movimientos originales:
         - Carne: -600g
         - Pan: -4u

Refund #1: 1 hamburguesa
         │
         ├─► refundRatio = 1 / 4 = 0.25
         └─► Restitución: Carne +150g, Pan +1u

Refund #2: 2 hamburguesas
         │
         ├─► refundRatio = 2 / 4 = 0.50
         └─► Restitución: Carne +300g, Pan +2u

Total restituido: Carne +450g, Pan +3u
Restante: Carne 150g, Pan 1u (1 hamburguesa sin devolver)  ✅
```

---

## ÍNDICE EXISTENTE QUE SOPORTA LA SOLUCIÓN

```sql
-- Archivo: 003_inventory_tables.sql, línea 221
CREATE INDEX IF NOT EXISTS idx_inv_movements_ref 
    ON inventory.movements (reference_type, reference_id);
```

**Query optimizado**:
```sql
EXPLAIN ANALYZE
SELECT product_id, ABS(quantity) AS quantity
FROM inventory.movements
WHERE reference_type = 'order'
  AND reference_id = 5001
  AND movement_type = 'recipe_consumption';

-- Resultado esperado:
-- Index Scan using idx_inv_movements_ref
-- Planning Time: 0.1ms
-- Execution Time: 0.3ms
```

---

## RESUMEN VISUAL

```
┌──────────────────────────────────────────────────────────────────┐
│                    FLUJO ACTUAL (INCORRECTO)                     │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Venta → Expande receta → Movimientos → Stock actualizado       │
│                                  │                               │
│                                  │ (tiempo pasa)                 │
│                                  │                               │
│                            Receta cambia                         │
│                                  │                               │
│                                  ▼                               │
│  Refund → Relee receta ❌ → Movimientos incorrectos             │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│                    FLUJO PROPUESTO (CORRECTO)                    │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Venta → Expande receta → Movimientos → Stock actualizado       │
│                                  │                               │
│                                  │ (tiempo pasa)                 │
│                                  │                               │
│                            Receta cambia                         │
│                                  │                               │
│                                  │                               │
│  Refund → Consulta movimientos originales ✅ → Reversa exacta   │
│                      ▲                                           │
│                      │                                           │
│                      └─────── Inmutable, siempre correcto        │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

---

**Documento completo**: `AUDITORIA-REFUND-PREPARADOS-INVENTARIO-HISTORICO.md`  
**Auditor**: Arquitectónico independiente  
**Fecha**: 2025-01-15
