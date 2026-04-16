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

api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    const branchId = localStorage.getItem('branchId');
    if (branchId) {
      config.headers['X-Branch-ID'] = branchId;
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
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      window.location.href = '/login';
    }

    return Promise.reject(error);
  }
);

export default api;
