import api from '../config/api';

export const cashRegisterService = {
  // Abrir caja
  open: async (data) => {
    const response = await api.post('/sales/cash-register/open', data);
    return response.data;
  },

  // Obtener caja activa del usuario
  getActive: async () => {
    const response = await api.get('/sales/cash-register/active');
    return response.data;
  },

  // Cerrar caja
  close: async (id, data) => {
    const response = await api.post(`/sales/cash-register/${id}/close`, data);
    return response.data;
  },

  // Agregar movimiento (entrada/salida)
  addMovement: async (id, data) => {
    const response = await api.post(`/sales/cash-register/${id}/movement`, data);
    return response.data;
  },

  // Obtener movimientos de una caja
  getMovements: async (id) => {
    const response = await api.get(`/sales/cash-register/${id}/movements`);
    return response.data;
  },

  // Obtener resumen (reporte Z)
  getSummary: async (id) => {
    const response = await api.get(`/sales/cash-register/${id}/summary`);
    return response.data;
  },

  // Historial de cajas
  getHistory: async (params = {}) => {
    const { dateFrom, dateTo, page = 1, limit = 20 } = params;
    const query = new URLSearchParams();
    if (dateFrom) query.append('dateFrom', dateFrom);
    if (dateTo) query.append('dateTo', dateTo);
    query.append('page', page);
    query.append('limit', limit);

    const response = await api.get(`/sales/cash-register/history?${query.toString()}`);
    return response.data;
  }
};
