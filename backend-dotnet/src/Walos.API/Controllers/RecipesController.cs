using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.API.Authorization;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Inventory;
using Walos.Application.Security;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Features;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/recipes")]
[Authorize(Policy = WalosPolicies.Recipes)]
[RequireFeature(WalosFeatures.Inventory)]
public class RecipesController : ControllerBase
{
    private readonly IRecipeService _service;
    private readonly ITenantContext _tenant;

    public RecipesController(IRecipeService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    [HttpGet("{productId:long}")]
    public async Task<IActionResult> GetRecipe(long productId)
    {
        var ingredients = (await _service.GetByProductAsync(productId, _tenant.CompanyId)).ToList();
        return Ok(ApiResponse<IEnumerable<Recipe>>.Ok(ingredients, count: ingredients.Count));
    }

    [HttpPut("{productId:long}/ingredients")]
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
    public async Task<IActionResult> UpsertIngredient(long productId, [FromBody] UpsertRecipeIngredientRequest request)
    {
        var result = await _service.UpsertIngredientAsync(productId, _tenant.CompanyId, request);
        return Ok(ApiResponse<Recipe>.Ok(result, "Ingrediente guardado"));
    }

    [HttpDelete("{productId:long}/ingredients/{ingredientId:long}")]
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
    public async Task<IActionResult> RemoveIngredient(long productId, long ingredientId)
    {
        var ok = await _service.RemoveIngredientAsync(productId, ingredientId, _tenant.CompanyId);
        if (!ok) return NotFound(ApiResponse.Fail("Ingrediente no encontrado en la receta"));
        return Ok(ApiResponse.Ok("Ingrediente eliminado"));
    }

    [HttpDelete("{productId:long}")]
    [Authorize(Policy = WalosPolicies.InventoryWrite)]
    public async Task<IActionResult> ClearRecipe(long productId)
    {
        await _service.ClearRecipeAsync(productId, _tenant.CompanyId);
        return Ok(ApiResponse.Ok("Receta eliminada"));
    }
}
