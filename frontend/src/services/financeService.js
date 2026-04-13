import api from '../config/api';

export const financeService = {
  getEntries: async (filters = {}) => {
    const response = await api.get('/finance/entries', { params: filters });
    return response.data;
  },

  createEntry: async (payload) => {
    const response = await api.post('/finance/entries', payload);
    return response.data;
  },

  updateEntry: async (id, payload) => {
    const response = await api.put(`/finance/entries/${id}`, payload);
    return response.data;
  },

  deleteEntry: async (id) => {
    const response = await api.delete(`/finance/entries/${id}`);
    return response.data;
  },

  getCategories: async (type = null) => {
    const response = await api.get('/finance/categories', {
      params: type ? { type } : {},
    });
    return response.data;
  },

  createCategory: async (payload) => {
    const response = await api.post('/finance/categories', payload);
    return response.data;
  },

  updateCategory: async (id, payload) => {
    const response = await api.put(`/finance/categories/${id}`, payload);
    return response.data;
  },

  deleteCategory: async (id) => {
    const response = await api.delete(`/finance/categories/${id}`);
    return response.data;
  },

  getSummary: async (filters = {}) => {
    const response = await api.get('/finance/summary', { params: filters });
    return response.data;
  },

  initMonth: async (payload) => {
    const response = await api.post('/finance/month/init', payload);
    return response.data;
  },
};

export default financeService;
