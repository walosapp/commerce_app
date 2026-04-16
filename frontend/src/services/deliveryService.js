import api from '../config/api';

const deliveryService = {
  getOrders: (params = {}) =>
    api.get('/delivery/orders', { params }).then(r => r.data),

  getOrder: (id) =>
    api.get(`/delivery/orders/${id}`).then(r => r.data),

  createOrder: (data) =>
    api.post('/delivery/orders', data).then(r => r.data),

  accept: (id, comment) =>
    api.post(`/delivery/orders/${id}/accept`, { comment }).then(r => r.data),

  reject: (id, comment) =>
    api.post(`/delivery/orders/${id}/reject`, { comment }).then(r => r.data),

  prepare: (id, comment) =>
    api.post(`/delivery/orders/${id}/prepare`, { comment }).then(r => r.data),

  ready: (id, comment) =>
    api.post(`/delivery/orders/${id}/ready`, { comment }).then(r => r.data),

  dispatch: (id, comment) =>
    api.post(`/delivery/orders/${id}/dispatch`, { comment }).then(r => r.data),

  deliver: (id, comment) =>
    api.post(`/delivery/orders/${id}/deliver`, { comment }).then(r => r.data),

  cancel: (id, comment) =>
    api.post(`/delivery/orders/${id}/cancel`, { comment }).then(r => r.data),

  return: (id, comment) =>
    api.post(`/delivery/orders/${id}/return`, { comment }).then(r => r.data),
};

export default deliveryService;
