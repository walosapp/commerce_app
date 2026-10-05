# AUDITORÍA V1 — REVISIÓN CRÍTICA: CORRELACIÓN ORDER_ITEM

> **Fecha**: 2025-01-15  
> **Auditor**: Arquitectónico independiente  
> **Tipo**: Revisión crítica de recomendación inicial  
> **Estado**: **RETRACTACIÓN DE CONCLUSIÓN INICIAL**

---

## ⚠️ RETRACTACIÓN

La recomendación inicial de **"REVERSIÓN DE MOVIMIENTOS ORIGINALES sin cambios de schema"** es **INCORRECTA** para el caso general.

**Documento original**: `AUDITORIA-REFUND-PREPARADOS-INVENTARIO-HISTORICO.md`  
**Conclusión original**: "CERO CAMBIOS DE SCHEMA"  
**Estado**: ❌ **INVALIDADA**

---

## 🔍 FALLO EN ANÁLISIS INICIAL

### Problema identificado

Los movimientos `recipe_consumption` **NO tienen `order_item_id`**, solo `reference_id` (orden completa).

**Evidencia** (`SaleInventoryPlanBuilder.cs`, líneas 106-122):

```csharp
plans.AddRange(recipes
    .GroupBy(recipe => new { recipe.IngredientId, ... })
    .Select(group => InventoryMovementPlanner.Create(
        ...
        quantity: group.Sum(recipe => recipe.Quantity * soldByProduct[recipe.ProductId]),
        referenceId: context.ReferenceId,  // ← ORDEN COMPLETA, no order_item_id
        ...
    ))
);
```

Los movimientos se **agrupan por ingrediente** a nivel de orden completa, perdiendo la correlación con `order_item_id`.

### Caso que invalida la solución inicial

**Orden #5001**:
- Item A: 2× "Hamburguesa Clásica" (Carne 150g + Pan 1u)
- Item B: 1× "Hamburguesa Premium" (Carne 200g + Pan 1u + Queso 50g)

**Movimientos generados**:
```sql
INSERT INTO inventory.movements (product_id, quantity, reference_id)
VALUES
    (10, -500, 5001),  -- Carne: 2×150 + 1×200 (AGREGADO)
    (20, -3,   5001),  -- Pan: 2×1 + 1×1 (AGREGADO)
    (30, -50,  5001);  -- Queso: 1×50
```

**Refund**: Devolver **solo Item B** (1 Hamburguesa Premium)

**Problema**: ¿Cómo determinar que del total de 500g de carne, 200g corresponden al Item B?

**Respuesta**: **NO ES POSIBLE** con el schema actual.

---

## 📊 MATRIZ DE CASOS

| Caso | Orden contiene | Refund | ¿Reversión sin schema funciona? |
|------|----------------|--------|--------------------------------|
| 1 | 1 preparado único | Completo | ✅ Sí |
| 2 | 1 preparado único | Parcial | ✅ Sí (proporcional) |
| 3 | Múltiples preparados sin ingredientes compartidos | Uno solo | ✅ Sí |
| 4 | **Múltiples preparados con ingredientes compartidos** | **Uno solo** | ❌ **NO** |
| 5 | **Múltiples preparados con ingredientes compartidos** | **Parcial de uno** | ❌ **NO** |
| 6 | Múltiples preparados con ingredientes compartidos | Completo | ✅ Sí |

**Casos 4 y 5 son BLOQUEANTES**.

---

## 🎯 RECOMENDACIÓN REVISADA

## ⭐ AGREGAR `source_order_item_id` A `inventory.movements`

### Cambios de schema

```sql
-- Migración: 019_add_source_order_item_to_movements.sql

ALTER TABLE inventory.movements
    ADD COLUMN source_order_item_id BIGINT;

ALTER TABLE inventory.movements
    ADD CONSTRAINT fk_movements_source_order_item
    FOREIGN KEY (source_order_item_id)
    REFERENCES sales.order_items(id)
    ON DELETE SET NULL;

CREATE INDEX idx_inv_movements_source_item 
    ON inventory.movements (source_order_item_id)
    WHERE source_order_item_id IS NOT NULL;
```

### Cambio en lógica de agrupación

**ANTES** (agrupado por ingrediente):
```csharp
plans.AddRange(recipes
    .GroupBy(recipe => new { recipe.IngredientId, ... })
    .Select(group => InventoryMovementPlanner.Create(
        quantity: group.Sum(recipe => recipe.Quantity * soldByProduct[recipe.ProductId]),
        ...
    ))
);
```

**DESPUÉS** (un movimiento por ingrediente por item):
```csharp
foreach (var preparedLine in preparedLines)
{
    var itemRecipes = recipes.Where(r => r.ProductId == preparedLine.ProductId);
    
    foreach (var recipe in itemRecipes)
    {
        plans.Add(InventoryMovementPlanner.Create(
            quantity: recipe.Quantity * preparedLine.Quantity,
            sourceOrderItemId: preparedLine.OrderItemId,  // ← NUEVO
            ...
        ));
    }
}
```

### Query de refund corregida

```csharp
var originalMovements = await connection.QueryAsync<OriginalMovementRow>(@"
    SELECT m.product_id AS ProductId,
           ABS(m.quantity) AS Quantity,
           m.unit_cost AS UnitCost,
           p.track_stock AS TrackStock
    FROM inventory.movements m
    JOIN inventory.products p ON p.id = m.product_id
    WHERE m.company_id = @CompanyId
      AND m.branch_id = @BranchId
      AND m.source_order_item_id = @OrderItemId  -- ← CLAVE
      AND m.movement_type = 'recipe_consumption'
      AND m.quantity < 0
    ORDER BY m.product_id", new
{
    command.CompanyId,
    command.BranchId,
    OrderItemId = refundItem.OrderItemId
}, transaction);
```

---

## 📋 COMPARACIÓN DE ALTERNATIVAS

| Alternativa | Precisión | Complejidad | Storage | Legacy | Mantenimiento | Score |
|-------------|-----------|-------------|---------|--------|---------------|-------|
| **`source_order_item_id`** | ✅ | ⚠️ | ⚠️ | ⚠️ | ✅ | **8/10** |
| Tabla consumptions | ✅ | ⚠️ | ❌ | ❌ | ⚠️ | 6/10 |
| JSONB metadata | ✅ | ⚠️ | ✅ | ⚠️ | ⚠️ | 7/10 |
| ~~Reversión sin cambios~~ | ❌ | ✅ | ✅ | ⚠️ | ✅ | **4/10** |

---

## ⚠️ COMPATIBILIDAD LEGACY REVISADA

### Problema con fallback silencioso

La recomendación inicial aceptaba:

> "Fallback a receta actual para ventas legacy"

**Esto es INACEPTABLE** porque:
1. Produce stock incorrecto silenciosamente
2. No hay forma de auditar el error
3. Perpetúa el bug original

### Estrategia correcta

**Opción A**: Bloquear refunds de órdenes legacy con múltiples preparados

```csharp
if (!originalMovements.Any())
{
    var preparedItemsCount = orderItems.Count(i => i.ProductType == "prepared");
    
    if (preparedItemsCount > 1)
    {
        throw new BusinessException(
            "No se puede procesar el refund de esta orden legacy con múltiples productos preparados. " +
            "Contacte al administrador para ajuste manual.");
    }
    
    // Solo para órdenes con 1 preparado: usar receta actual
    _logger.LogWarning("Usando receta actual para venta legacy con 1 preparado");
    originalMovements = await GetCurrentRecipeAsFallback(productId);
}
```

**Opción B**: Bloquear todos los refunds legacy de preparados

```csharp
if (!originalMovements.Any())
{
    throw new BusinessException(
        "Esta venta no tiene movimientos de inventario detallados. " +
        "El refund debe procesarse manualmente.",
        "legacy_sale_manual_refund_required");
}
```

---

## 📊 IMPACTO REVISADO

### Estimación inicial (INCORRECTA)

- ❌ Cambios de schema: **Ninguno**
- ❌ Líneas de código: ~230
- ❌ Tiempo: 1 día

### Estimación revisada (CORRECTA)

- ✅ Cambios de schema: **1 columna + 1 FK + 1 índice**
- ✅ Líneas de código: ~400
- ✅ Archivos modificados: 5
- ✅ Tiempo: **3-4 días**

### Desglose

| Fase | Tiempo estimado |
|------|----------------|
| Desarrollo | 1-2 días |
| Tests | 1 día |
| Migración | 1 hora |
| QA en staging | 1 día |
| Deploy | 1 hora |
| **Total** | **3-4 días** |

---

## 🔄 IMPACTO EN STORAGE

### Incremento de filas

**Antes** (agrupado):
```
Orden: 2 Hamburguesas + 1 Pizza
Movimientos: 4 (ingredientes únicos agregados)
```

**Después** (por item):
```
Orden: 2 Hamburguesas + 1 Pizza
Movimientos: 6 (ingredientes × items)
Incremento: +50%
```

**Justificación**: Aceptable dado que:
1. Resuelve el problema correctamente
2. Mantiene integridad referencial
3. No duplica información (vs. tabla separada)

---

## ✅ CONCLUSIÓN FINAL

### Retractación

❌ **"CERO CAMBIOS DE SCHEMA"** — INVALIDADA

### Nueva recomendación

✅ **AGREGAR `source_order_item_id` A `inventory.movements`**

**Razón**: Única solución que:
- Resuelve el caso general (múltiples preparados con ingredientes compartidos)
- Mantiene el ledger como fuente única de verdad
- No duplica información
- Permite auditoría completa

### Compatibilidad legacy

**Estrategia**: Bloquear refunds de órdenes legacy con múltiples productos preparados.

**Justificación**: Preferible bloquear explícitamente que producir stock incorrecto silenciosamente.

---

## 📁 DOCUMENTOS RELACIONADOS

- ❌ `AUDITORIA-REFUND-PREPARADOS-INVENTARIO-HISTORICO.md` — Conclusión invalidada
- ❌ `AUDITORIA-REFUND-PREPARADOS-RESUMEN-EJECUTIVO.md` — Conclusión invalidada
- ✅ `AUDITORIA-REFUND-REVISION-CRITICA.md` — Este documento (correcto)

---

**Auditor**: Arquitectónico independiente  
**Fecha**: 2025-01-15  
**Versión**: 2.0 (Revisión crítica)
