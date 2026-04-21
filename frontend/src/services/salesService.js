/**
 * Servicio de Ventas
 * ¿Qué es? Funciones para comunicarse con la API de ventas
 * ¿Para qué? Abstraer llamadas HTTP del modulo de ventas
 */

import api from '../config/api';

export const salesService = {
  getTables: async (branchId) => {
    const response = await api.get('/sales/tables', { params: { branchId } });
    return response.data;
  },

  createTable: async (data) => {
    const response = await api.post('/sales/tables', data);
    return response.data;
  },

  invoiceTable: async (tableId, payload = {}) => {
    const response = await api.post(/sales/tables//invoice, payload);
    return response.data;
  },

  cancelTable: async (tableId) => {
    const response = await api.delete(/sales/tables/);
    return response.data;
  },

  updateItemQuantity: async (itemId, quantity, orderId) => {
    const response = await api.patch(/sales/items//quantity, { quantity, orderId });
    return response.data;
  },

  addItemsToTable: async (tableId, items) => {
    const response = await api.post(/sales/tables//items, items);
    return response.data;
  },

  renameTable: async (tableId, name) => {
    const response = await api.patch(/sales/tables//name, { name });
    return response.data;
  },
};

export default salesService;
