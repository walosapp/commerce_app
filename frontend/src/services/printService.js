import api from '../config/api';

export const printService = {
  getReceipt: async (orderId) => {
    const response = await api.get(`/sales/orders/${orderId}/receipt`);
    return response.data;
  },
  getKitchenTicket: async (orderId) => {
    const response = await api.get(`/sales/orders/${orderId}/kitchen`);
    return response.data;
  },
  getZReport: async (registerId) => {
    const response = await api.get(`/sales/cash-register/${registerId}/z-report`);
    return response.data;
  },
};

export default printService;
