using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Walos.Application.DTOs.Common;
using Walos.Application.DTOs.Inventory;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Interfaces;

namespace Walos.API.Controllers;

[ApiController]
[Route("api/v1/recipes")]
[Authorize]
public class RecipesController : ControllerBase
{
    private readonly IRecipeRepository _repo;
    private readonly ITenantContext _tenant;

    public RecipesController(IRecipeRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    // GET /api/v1/recipes/{productId}
    [HttpGet("{productId:long}")]
    public async Task<IActionResult> GetRecipe(long productId)
    {
        var ingredients = (await _repo.GetByProductAsync(productId, _tenant.CompanyId)).ToList();
        return Ok(ApiResponse<IEnumerable<Recipe>>.Ok(ingredients, count: ingredients.Count));
    }

    // PUT /api/v1/recipes/{productId}/ingredients  — upsert one ingredient
    [HttpPut("{productId:long}/ingredients")]
    public async Task<IActionResult> UpsertIngredient(long productId, [FromBody] UpsertRecipeIngredientRequest request)
    {
        if (request.IngredientId <= 0)
            return BadRequest(ApiResponse.Fail("IngredientId requerido"));
        if (request.Quantity <= 0)
            return BadRequest(ApiResponse.Fail("La cantidad debe ser mayor a 0"));

        var recipe = new Recipe
        {
            CompanyId    = _tenant.CompanyId,
            ProductId    = productId,
            IngredientId = request.IngredientId,
            Quantity     = request.Quantity,
            UnitId       = request.UnitId,
            Notes        = request.Notes,
        };

        var result = await _repo.UpsertIngredientAsync(recipe);
        return Ok(ApiResponse<Recipe>.Ok(result, "Ingrediente guardado"));
    }

    // DELETE /api/v1/recipes/{productId}/ingredients/{ingredientId}
    [HttpDelete("{productId:long}/ingredients/{ingredientId:long}")]
    public async Task<IActionResult> RemoveIngredient(long productId, long ingredientId)
    {
        var ok = await _repo.RemoveIngredientAsync(productId, ingredientId, _tenant.CompanyId);
        if (!ok) return NotFound(ApiResponse.Fail("Ingrediente no encontrado en la receta"));
        return Ok(ApiResponse.Ok("Ingrediente eliminado"));
    }

    // DELETE /api/v1/recipes/{productId}  — clear entire recipe
    [HttpDelete("{productId:long}")]
    public async Task<IActionResult> ClearRecipe(long productId)
    {
        await _repo.ClearRecipeAsync(productId, _tenant.CompanyId);
        return Ok(ApiResponse.Ok("Receta eliminada"));
    }
}
