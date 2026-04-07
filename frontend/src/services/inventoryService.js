/**
 * Servicio de Inventario
 * ¿Qué es? Funciones para comunicarse con la API de inventario
 * ¿Para qué? Abstraer llamadas HTTP del backend
 */

import api from '../config/api';

export const inventoryService = {
  getProducts: async (filters = {}) => {
    const response = await api.get('/inventory/products', { params: filters });
    return response.data;
  },

  getProductById: async (id) => {
    const response = await api.get(`/inventory/products/${id}`);
    return response.data;
  },

  createProduct: async (productData) => {
    const response = await api.post('/inventory/products', productData);
    return response.data;
  },

  getStock: async (branchId) => {
    const response = await api.get('/inventory/stock', {
      params: { branchId },
    });
    return response.data;
  },

  getLowStock: async (branchId) => {
    const response = await api.get('/inventory/stock/low', {
      params: { branchId },
    });
    return response.data;
  },

  processAIInput: async (userInput, inputType = 'text', sessionId = null) => {
    const response = await api.post('/inventory/ai/process', {
      userInput,
      inputType,
      sessionId,
    });
    return response.data;
  },

  confirmAIAction: async (interactionId) => {
    const response = await api.post(`/inventory/ai/confirm/${interactionId}`);
    return response.data;
  },

  getAlerts: async (branchId = null) => {
    const response = await api.get('/inventory/alerts', {
      params: { branchId },
    });
    return response.data;
  },

  addStock: async (payload) => {
    const response = await api.post('/inventory/stock/add', payload);
    return response.data;
  },

  getProfitsReport: async (branchId, startDate = null, endDate = null) => {
    const response = await api.get('/inventory/reports/profits', {
      params: { branchId, startDate, endDate },
    });
    return response.data;
  },

  updateProduct: async (id, productData) => {
    const response = await api.put(`/inventory/products/${id}`, productData);
    return response.data;
  },

  deleteProduct: async (id) => {
    const response = await api.delete(`/inventory/products/${id}`);
    return response.data;
  },

  getCategories: async () => {
    const response = await api.get('/inventory/categories');
    return response.data;
  },

  getUnits: async () => {
    const response = await api.get('/inventory/units');
    return response.data;
  },

  uploadProductImage: async (productId, file) => {
    const formData = new FormData();
    formData.append('file', file);
    const response = await api.post(`/inventory/products/${productId}/image`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return response.data;
  },
};

export default inventoryService;
