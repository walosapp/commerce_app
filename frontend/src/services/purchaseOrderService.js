import api from '../config/api';

const purchaseOrderService = {
  getAll:   (supplierId)     => api.get('/purchase-orders', { params: supplierId ? { supplierId } : {} }).then(r => r.data),
  getById:  (id)             => api.get(`/purchase-orders/${id}`).then(r => r.data),
  create:   (data)           => api.post('/purchase-orders', data).then(r => r.data),
  receive:  (id, data)       => api.post(`/purchase-orders/${id}/receive`, data).then(r => r.data),
  cancel:   (id)             => api.post(`/purchase-orders/${id}/cancel`).then(r => r.data),
};

export default purchaseOrderService;
