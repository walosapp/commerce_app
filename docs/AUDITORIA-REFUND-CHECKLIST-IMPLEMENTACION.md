# CHECKLIST DE IMPLEMENTACIÓN — CORRECCIÓN REFUND DE PREPARADOS

> **Auditoría**: Refund de preparados e inventario histórico  
> **Fecha**: 2025-01-15  
> **Recomendación**: Reversión de movimientos originales

---

## FASE 1: PREPARACIÓN (1 hora)

### 1.1 Revisión de documentación

- [ ] Leer `AUDITORIA-REFUND-PREPARADOS-INVENTARIO-HISTORICO.md` completo
- [ ] Revisar `AUDITORIA-REFUND-PREPARADOS-RESUMEN-EJECUTIVO.md`
- [ ] Estudiar `AUDITORIA-REFUND-FLUJO-COMPARATIVO.md`
- [ ] Familiarizarse con `AUDITORIA-REFUND-QUERIES-VALIDACION.sql`

### 1.2 Configuración de entorno

- [ ] Crear rama: `git checkout -b fix/refund-prepared-products-inventory`
- [ ] Verificar que tests de integración pasan: `dotnet test`
- [ ] Configurar conexión a base de datos de staging
- [ ] Backup de base de datos de staging

### 1.3 Análisis de impacto

- [ ] Ejecutar query #7 de validación (identificar órdenes con riesgo)
- [ ] Ejecutar query #10 (estadísticas generales)
- [ ] Documentar cantidad de órdenes afectadas
- [ ] Identificar productos preparados con recetas cambiadas

---

## FASE 2: IMPLEMENTACIÓN (4-6 horas)

### 2.1 Modificar RefundRepository.cs

**Archivo**: `backend-dotnet/src/Walos.Infrastructure/Repositories/RefundRepository.cs`

#### Paso 1: Crear clase OriginalMovementRow

- [ ] Agregar después de la línea 11:

```csharp
private sealed class OriginalMovementRow
{
    public long ProductId { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public bool TrackStock { get; init; }
}
```

#### Paso 2: Modificar RestoreInventoryAsync (líneas 670-711)

- [ ] Reemplazar query de recetas por query de movimientos originales
- [ ] Implementar cálculo de `refundRatio` para refunds parciales
- [ ] Mantener validación de `TrackStock`

**Código propuesto**:

```csharp
private static async Task RestoreInventoryAsync(
    System.Data.IDbConnection connection,
    System.Data.IDbTransaction transaction,
    RefundProcessCommand command,
    RefundOrderRow order,
    List<RefundOrderItemRow> orderItems,
    List<RefundItem> refundItems,
    long refundId)
{
    foreach (var refundItem in refundItems)
    {
        var orderItem = orderItems.Single(i => i.Id == refundItem.OrderItemId);
        
        if (orderItem.ProductType == "prepared")
        {
            // NUEVO: Consultar movimientos originales
            var originalMovements = (await connection.QueryAsync<OriginalMovementRow>(@"
                SELECT m.product_id AS ProductId,
                       ABS(m.quantity) AS Quantity,
                       m.unit_cost AS UnitCost,
                       p.track_stock AS TrackStock
                FROM inventory.movements m
                JOIN inventory.products p ON p.id = m.product_id
                WHERE m.company_id = @CompanyId
                  AND m.branch_id = @BranchId
                  AND m.reference_type = 'order'
                  AND m.reference_id = @OrderId
                  AND m.movement_type = 'recipe_consumption'
                  AND m.quantity < 0
                ORDER BY m.product_id", new
            {
                command.CompanyId,
                command.BranchId,
                OrderId = order.Id
            }, transaction)).ToList();

            if (originalMovements.Count == 0)
            {
                // Fallback para ventas legacy
                originalMovements = await GetCurrentRecipeAsFallback(
                    connection, transaction, command, orderItem.ProductId);
            }

            // Calcular proporción para refund parcial
            var refundRatio = refundItem.Quantity / orderItem.Quantity;

            foreach (var movement in originalMovements.Where(m => m.TrackStock))
            {
                await RestoreTrackedProductAsync(
                    connection, transaction, command,
                    movement.ProductId,
                    movement.Quantity * refundRatio,
                    movement.UnitCost,
                    "refund_recipe",
                    refundId,
                    order.OrderNumber);
            }
        }
        else if (orderItem.TrackStock)
        {
            // Productos simples: sin cambios
            await RestoreTrackedProductAsync(
                connection, transaction, command,
                orderItem.ProductId,
                refundItem.Quantity,
                refundItem.UnitPrice,
                "refund",
                refundId,
                order.OrderNumber);
        }
    }
}
```

- [ ] Código implementado
- [ ] Compilación exitosa

#### Paso 3: Crear método GetCurrentRecipeAsFallback

- [ ] Agregar después de `RestoreInventoryAsync`:

```csharp
private static async Task<List<OriginalMovementRow>> GetCurrentRecipeAsFallback(
    System.Data.IDbConnection connection,
    System.Data.IDbTransaction transaction,
    RefundProcessCommand command,
    long productId)
{
    var currentRecipe = await connection.QueryAsync<OriginalMovementRow>(@"
        SELECT r.ingredient_id AS ProductId,
               r.quantity AS Quantity,
               p.cost_price AS UnitCost,
               p.track_stock AS TrackStock
        FROM inventory.recipes r
        JOIN inventory.products p
          ON p.id = r.ingredient_id AND p.company_id = r.company_id
        WHERE r.company_id = @CompanyId AND r.product_id = @ProductId", new
    {
        command.CompanyId,
        ProductId = productId
    }, transaction);

    return currentRecipe.ToList();
}
```

- [ ] Código implementado
- [ ] Compilación exitosa

#### Paso 4: Agregar logging (opcional pero recomendado)

- [ ] Agregar después de la línea con `originalMovements.Count == 0`:

```csharp
_logger.LogWarning(
    "No se encontraron movimientos originales para order {OrderId}, product {ProductId}. " +
    "Usando receta actual como fallback. Esto puede indicar una venta legacy.",
    order.Id, orderItem.ProductId);
```

- [ ] Código implementado

### 2.2 Actualizar tests existentes

**Archivo**: `backend-dotnet/tests/Walos.Tests/Integration/RefundAtomicityIntegrationTests.cs`

#### Paso 1: Actualizar test de línea 40

- [ ] Cambiar nombre del test:

```csharp
// ANTES:
public async Task Prepared_Product_Partial_Refund_Restores_Current_Recipe_Ingredient()

// DESPUÉS:
public async Task Prepared_Product_Partial_Refund_Restores_Original_Recipe_Ingredient()
```

- [ ] Test actualizado

#### Paso 2: Actualizar test de línea 53

- [ ] Cambiar nombre del test:

```csharp
// ANTES:
public async Task Prepared_Product_Full_Refund_Restores_All_Current_Recipe_Ingredients()

// DESPUÉS:
public async Task Prepared_Product_Full_Refund_Restores_All_Original_Recipe_Ingredients()
```

- [ ] Test actualizado

### 2.3 Crear nuevos tests

**Archivo**: `backend-dotnet/tests/Walos.Tests/Integration/RefundAtomicityIntegrationTests.cs`

#### Test 1: Refund después de cambio de receta

- [ ] Agregar al final del archivo:

```csharp
[SkippableFact]
public async Task Refund_After_Recipe_Change_Restores_Original_Ingredients()
{
    // Arrange: Crear producto preparado con receta V1
    var ctx = await SeedPreparedOrderAsync("Recipe change test");
    
    // Obtener ingredientes originales
    var originalMovements = await GetMovementsAsync(ctx.Order);
    
    // Cambiar receta (simular cambio en inventory.recipes)
    await ChangeRecipeAsync(ctx.PreparedProductId, new[]
    {
        (IngredientId: 999, Quantity: 50m)  // Nuevo ingrediente
    });
    
    // Act: Refund completo
    var result = await RefundAsync(ctx, "full", NewKey(), null);
    
    // Assert: Debe restituir ingredientes originales, no los nuevos
    var refundMovements = await GetRefundMovementsAsync(ctx.Order);
    Assert.Equal(originalMovements.Count, refundMovements.Count);
    Assert.DoesNotContain(refundMovements, m => m.ProductId == 999);
}
```

- [ ] Test implementado
- [ ] Test pasa

#### Test 2: Fallback para ventas legacy

- [ ] Agregar:

```csharp
[SkippableFact]
public async Task Refund_Without_Original_Movements_Uses_Current_Recipe_Fallback()
{
    // Arrange: Simular venta legacy eliminando movimientos
    var ctx = await SeedPreparedOrderAsync("Legacy test");
    await DeleteMovementsAsync(ctx.Order);
    
    // Act: Refund
    var result = await RefundAsync(ctx, "full", NewKey(), null);
    
    // Assert: Debe usar receta actual como fallback
    var refundMovements = await GetRefundMovementsAsync(ctx.Order);
    Assert.NotEmpty(refundMovements);
}
```

- [ ] Test implementado
- [ ] Test pasa

---

## FASE 3: VALIDACIÓN EN STAGING (2-3 horas)

### 3.1 Deploy a staging

- [ ] Compilar solución: `dotnet build`
- [ ] Ejecutar todos los tests: `dotnet test`
- [ ] Verificar que todos los tests pasan
- [ ] Deploy a ambiente de staging

### 3.2 Casos de prueba manuales

#### Caso 1: Refund de producto preparado sin cambio de receta

- [ ] Crear producto preparado "Test Burger" con receta:
  - Ingrediente A: 100g
  - Ingrediente B: 2u
- [ ] Vender 3 unidades
- [ ] Verificar movimientos con query #1 de validación
- [ ] Hacer refund completo
- [ ] Verificar con query #4 que balance es correcto
- [ ] **Resultado**: ✅ Correcto

#### Caso 2: Refund después de cambio de receta

- [ ] Crear producto preparado "Test Pizza" con receta V1:
  - Masa: 200g
  - Queso: 50g
- [ ] Vender 2 unidades
- [ ] Anotar movimientos originales
- [ ] Cambiar receta a V2:
  - Masa: 180g
  - Queso: 60g
  - Tomate: 30g (nuevo)
- [ ] Hacer refund de 1 unidad
- [ ] Verificar con query #5 que NO hay bug
- [ ] Verificar que se restituyó receta V1, no V2
- [ ] **Resultado**: ✅ Correcto

#### Caso 3: Refund parcial

- [ ] Vender 4 unidades de producto preparado
- [ ] Hacer refund de 1 unidad
- [ ] Verificar proporción 1/4 con query #4
- [ ] Hacer refund de 2 unidades más
- [ ] Verificar proporción acumulada 3/4
- [ ] **Resultado**: ✅ Correcto

#### Caso 4: Venta legacy (simulada)

- [ ] Crear venta antigua sin movimientos detallados
- [ ] Hacer refund
- [ ] Verificar que usa fallback (revisar logs)
- [ ] Verificar que refund se completa sin errores
- [ ] **Resultado**: ✅ Correcto (con warning en logs)

### 3.3 Validación de integridad

- [ ] Ejecutar query #8 (integridad de stock)
- [ ] Verificar que no hay inconsistencias
- [ ] Ejecutar query #10 (estadísticas)
- [ ] Comparar con estadísticas pre-deploy

---

## FASE 4: DOCUMENTACIÓN (1 hora)

### 4.1 Actualizar documentación técnica

- [ ] Actualizar `README.md` con nota sobre corrección
- [ ] Agregar entrada en `CHANGELOG.md`:

```markdown
## [Unreleased]

### Fixed
- Refund de productos preparados ahora reversa movimientos originales en lugar de recalcular desde receta actual
- Corrige bug donde cambios de receta entre venta y refund causaban restitución incorrecta de inventario
- Implementa fallback a receta actual para ventas legacy sin movimientos detallados
```

- [ ] Documentación actualizada

### 4.2 Crear guía de troubleshooting

- [ ] Crear archivo `docs/REFUND-TROUBLESHOOTING.md`:

```markdown
# Troubleshooting — Refunds de Productos Preparados

## Síntomas comunes

### Stock incorrecto después de refund
**Causa**: Posible venta legacy sin movimientos detallados
**Solución**: Ejecutar query #8 de validación, ajustar stock manualmente si necesario

### Warning en logs: "No se encontraron movimientos originales"
**Causa**: Venta anterior a implementación del ledger detallado
**Solución**: Normal, el sistema usa fallback a receta actual

### Refund parcial restituye cantidades incorrectas
**Causa**: Bug en cálculo de proporción
**Solución**: Verificar con query #4, reportar si persiste
```

- [ ] Guía creada

---

## FASE 5: DEPLOY A PRODUCCIÓN (1 hora)

### 5.1 Pre-deploy

- [ ] Revisar checklist completo
- [ ] Confirmar que todos los tests pasan
- [ ] Confirmar validación en staging exitosa
- [ ] Backup de base de datos de producción
- [ ] Notificar a equipo de operaciones

### 5.2 Deploy

- [ ] Merge a rama principal: `git merge fix/refund-prepared-products-inventory`
- [ ] Tag de versión: `git tag -a v1.x.x -m "Fix refund prepared products"`
- [ ] Push: `git push origin main --tags`
- [ ] Deploy automático o manual según proceso
- [ ] Verificar que aplicación inicia correctamente

### 5.3 Smoke tests en producción

- [ ] Verificar que endpoint de refund responde
- [ ] Crear refund de prueba en orden de test
- [ ] Verificar logs (no debe haber errores)
- [ ] Rollback plan preparado en caso de problemas

---

## FASE 6: MONITOREO POST-DEPLOY (1 semana)

### 6.1 Monitoreo diario (primeros 3 días)

- [ ] **Día 1**: Revisar logs cada 4 horas
  - [ ] Mañana (8am)
  - [ ] Mediodía (12pm)
  - [ ] Tarde (4pm)
  - [ ] Noche (8pm)
- [ ] **Día 2**: Revisar logs cada 8 horas
  - [ ] Mañana (8am)
  - [ ] Tarde (4pm)
- [ ] **Día 3**: Revisar logs cada 12 horas
  - [ ] Mañana (8am)
  - [ ] Noche (8pm)

### 6.2 Métricas a monitorear

- [ ] Cantidad de refunds procesados
- [ ] Cantidad de warnings de fallback
- [ ] Errores en endpoint de refund
- [ ] Tiempo de respuesta de refund
- [ ] Inconsistencias de stock reportadas

### 6.3 Queries de monitoreo

- [ ] Ejecutar query #10 (estadísticas) diariamente
- [ ] Ejecutar query #8 (integridad) cada 3 días
- [ ] Ejecutar query #7 (órdenes con riesgo) semanalmente

### 6.4 Alertas

- [ ] Configurar alerta si tasa de fallback > 10%
- [ ] Configurar alerta si errores de refund > 1%
- [ ] Configurar alerta si inconsistencias de stock > 0

---

## FASE 7: CIERRE (1 hora)

### 7.1 Reporte final

- [ ] Crear documento `REFUND-FIX-DEPLOYMENT-REPORT.md`:

```markdown
# Reporte de Deploy — Corrección Refund de Preparados

## Resumen
- **Fecha de deploy**: YYYY-MM-DD
- **Versión**: v1.x.x
- **Duración total**: X horas
- **Downtime**: 0 minutos

## Métricas pre-deploy
- Órdenes con riesgo: X
- Refunds procesados (último mes): X
- Inconsistencias de stock: X

## Métricas post-deploy (1 semana)
- Refunds procesados: X
- Fallbacks ejecutados: X (X%)
- Errores: 0
- Inconsistencias nuevas: 0

## Conclusión
✅ Deploy exitoso, sin incidentes
```

- [ ] Reporte creado

### 7.2 Retrospectiva

- [ ] Reunión con equipo de desarrollo
- [ ] Documentar lecciones aprendidas
- [ ] Identificar mejoras para próximos deploys

### 7.3 Limpieza

- [ ] Eliminar ramas de feature
- [ ] Archivar documentos de auditoría
- [ ] Actualizar wiki/documentación interna

---

## ROLLBACK PLAN (en caso de problemas)

### Síntomas que requieren rollback

- [ ] Tasa de errores en refund > 5%
- [ ] Inconsistencias de stock > 10 productos
- [ ] Quejas de usuarios sobre refunds incorrectos
- [ ] Performance degradada (tiempo de respuesta > 5s)

### Procedimiento de rollback

1. [ ] Notificar a equipo
2. [ ] Revertir deploy a versión anterior
3. [ ] Verificar que aplicación funciona
4. [ ] Ejecutar query #8 para identificar inconsistencias
5. [ ] Ajustar stock manualmente si necesario
6. [ ] Analizar causa raíz
7. [ ] Planificar nuevo intento de deploy

---

## CONTACTOS DE EMERGENCIA

- **Desarrollador responsable**: [Nombre]
- **DevOps**: [Nombre]
- **Product Owner**: [Nombre]
- **On-call**: [Teléfono]

---

## NOTAS ADICIONALES

- Este checklist asume que el equipo tiene experiencia con el stack tecnológico
- Los tiempos son estimados y pueden variar según complejidad del entorno
- Se recomienda realizar deploy en horario de baja actividad (ej: domingo noche)
- Mantener comunicación constante con stakeholders durante todo el proceso

---

**Última actualización**: 2025-01-15  
**Versión del checklist**: 1.0
