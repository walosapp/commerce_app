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

  getTemplates: async (filters = {}) => {
    const response = await api.get('/finance/templates', { params: filters });
    return response.data;
  },

  createTemplate: async (payload) => {
    const response = await api.post('/finance/templates', payload);
    return response.data;
  },

  updateTemplate: async (id, payload) => {
    const response = await api.put(`/finance/templates/${id}`, payload);
    return response.data;
  },

  deleteTemplate: async (id) => {
    const response = await api.delete(`/finance/templates/${id}`);
    return response.data;
  },

  initMonth: async (payload) => {
    const response = await api.post('/finance/month/init', payload);
    return response.data;
  },
};

export default financeService;
