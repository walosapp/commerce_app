import api from '../config/api';

const catalogService = {
  // Categories
  getCategories:       ()           => api.get('/catalog/categories').then(r => r.data),
  createCategory:      (data)       => api.post('/catalog/categories', data).then(r => r.data),
  updateCategory:      (id, data)   => api.put(`/catalog/categories/${id}`, data).then(r => r.data),
  setCategoryStatus:   (id, active) => api.patch(`/catalog/categories/${id}/status`, { isActive: active }).then(r => r.data),
  deleteCategory:      (id)         => api.delete(`/catalog/categories/${id}`).then(r => r.data),

  // Units
  getUnits:            ()           => api.get('/catalog/units').then(r => r.data),
  createUnit:          (data)       => api.post('/catalog/units', data).then(r => r.data),
  updateUnit:          (id, data)   => api.put(`/catalog/units/${id}`, data).then(r => r.data),
  setUnitStatus:       (id, active) => api.patch(`/catalog/units/${id}/status`, { isActive: active }).then(r => r.data),
  deleteUnit:          (id)         => api.delete(`/catalog/units/${id}`).then(r => r.data),
};

export default catalogService;
