import api from '../config/api';

export const refundService = {
  create: async (data) => {
    const { idempotencyKey, ...payload } = data;
    const response = await api.post('/sales/refunds', payload, {
      headers: { 'Idempotency-Key': idempotencyKey },
    });
    return response.data;
  },

  getAll: async (params = {}) => {
    const response = await api.get('/sales/refunds', { params });
    return response.data;
  },

  getById: async (id) => {
    const response = await api.get(`/sales/refunds/${id}`);
    return response.data;
  },

  getByOrder: async (orderId) => {
    const response = await api.get(`/sales/refunds/by-order/${orderId}`);
    return response.data;
  },
};

export default refundService;
