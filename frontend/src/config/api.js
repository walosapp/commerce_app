/**
 * Configuración de API
 * ¿Qué es? Cliente HTTP configurado para comunicarse con el backend
 * ¿Para qué? Centralizar llamadas API con interceptores
 */

import axios from 'axios';

const rawApiBaseUrl = import.meta.env.VITE_API_URL || 'http://localhost:3000';
const API_BASE_URL = rawApiBaseUrl.replace(/\/+$/, '');
const API_VERSION = import.meta.env.VITE_API_VERSION || 'v1';

const api = axios.create({
  baseURL: `${API_BASE_URL}/api/${API_VERSION}`,
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
  },
});

let _getAuthState = null;

export const setAuthStateGetter = (getter) => {
  _getAuthState = getter;
};

api.interceptors.request.use(
  (config) => {
    const state = _getAuthState?.();
    if (state?.token) {
      config.headers.Authorization = `Bearer ${state.token}`;
    }
    if (state?.tenantId) {
      config.headers['X-Company-ID'] = state.tenantId;
    }
    if (state?.branchId) {
      config.headers['X-Branch-ID'] = state.branchId;
    }

    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      const state = _getAuthState?.();
      if (state?.isAuthenticated) {
        state.logout?.();
      }
      window.location.href = '/login';
    }

    return Promise.reject(error);
  }
);

export default api;
