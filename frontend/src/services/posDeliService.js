import api from '../config/api';

const posDeliService = {
  getProducts: async ({ search, barcode, categoryId } = {}) => {
    const response = await api.get('/pos-deli/products', {
      params: {
        search: search || undefined,
        barcode: barcode || undefined,
        categoryId: categoryId || undefined,
      },
    });
    return response.data;
  },

  getFavorites: async () => {
    const response = await api.get('/pos-deli/favorites');
    return response.data;
  },

  createSale: async (payload, idempotencyKey) => {
    const response = await api.post('/pos-deli/sale', payload, {
      headers: idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : undefined,
    });
    return response.data;
  },
};

export default posDeliService;
