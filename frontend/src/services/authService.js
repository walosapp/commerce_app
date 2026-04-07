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
};

export default authService;
