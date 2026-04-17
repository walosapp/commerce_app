import api from '../config/api';

const recipeService = {
  getByProduct: (productId)                       => api.get(`/recipes/${productId}`).then(r => r.data),
  upsertIngredient: (productId, data)             => api.put(`/recipes/${productId}/ingredients`, data).then(r => r.data),
  removeIngredient: (productId, ingredientId)     => api.delete(`/recipes/${productId}/ingredients/${ingredientId}`).then(r => r.data),
  clearRecipe: (productId)                        => api.delete(`/recipes/${productId}`).then(r => r.data),
};

export default recipeService;
