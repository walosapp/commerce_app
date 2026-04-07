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
    const response = await api.post(`/sales/tables/${tableId}/invoice`, payload);
    return response.data;
  },

  cancelTable: async (tableId) => {
    const response = await api.delete(`/sales/tables/${tableId}`);
    return response.data;
  },

  updateItemQuantity: async (itemId, quantity, orderId) => {
    const response = await api.patch(`/sales/items/${itemId}/quantity`, { quantity, orderId });
    return response.data;
  },

  addItemsToTable: async (tableId, items) => {
    const response = await api.post(`/sales/tables/${tableId}/items`, items);
    return response.data;
  },
};

export default salesService;
