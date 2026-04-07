/**
 * Servicio de Configuracion de Empresa
 * ¿Qué es? Funciones para comunicarse con la API de branding, temas y reglas operativas
 * ¿Para qué? Gestionar nombre, logo y politicas del negocio
 */

import api from '../config/api';

export const companyService = {
  getSettings: async () => {
    const response = await api.get('/company/settings');
    return response.data;
  },

  updateSettings: async (settingsData) => {
    const response = await api.put('/company/settings', settingsData);
    return response.data;
  },

  getOperationsSettings: async () => {
    const response = await api.get('/company/settings/operations');
    return response.data;
  },

  updateOperationsSettings: async (settingsData) => {
    const response = await api.put('/company/settings/operations', settingsData);
    return response.data;
  },

  uploadLogo: async (file) => {
    const formData = new FormData();
    formData.append('file', file);
    const response = await api.post('/company/settings/logo', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return response.data;
  },
};

export default companyService;
