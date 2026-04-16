import api from '../config/api';

const supplierService = {
  getAll: ()                         => api.get('/suppliers').then(r => r.data),
  getById: (id)                      => api.get(`/suppliers/${id}`).then(r => r.data),
  create: (data)                     => api.post('/suppliers', data).then(r => r.data),
  update: (id, data)                 => api.put(`/suppliers/${id}`, data).then(r => r.data),
  delete: (id)                       => api.delete(`/suppliers/${id}`).then(r => r.data),
  addProduct: (id, data)             => api.post(`/suppliers/${id}/products`, data).then(r => r.data),
  removeProduct: (id, productId)     => api.delete(`/suppliers/${id}/products/${productId}`).then(r => r.data),
  getSuggestedOrder: (id)            => api.get(`/suppliers/${id}/suggested-order`).then(r => r.data),
};

export default supplierService;
