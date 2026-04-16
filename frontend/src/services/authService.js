/**
 * Servicio de Autenticación
 * ¿Qué es? Funciones para comunicarse con la API de auth
 * ¿Para qué? Abstraer llamadas HTTP de autenticación
 */

import api from '../config/api';

export const authService = {
  login: async (username, password) => {
    const response = await api.post('/auth/login', { username, password });
    return response.data;
  },

  logout: async () => {
    try {
      await api.post('/auth/logout');
    } catch {
      // Ignore errors — we clear local state regardless
    }
  },

  refreshToken: async (refreshToken) => {
    const response = await api.post('/auth/refresh', { refreshToken });
    return response.data;
  },
};

export default authService;
