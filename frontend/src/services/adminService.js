import api from '../config/api';

const adminService = {
  getTenants: () => api.get('/admin/tenants').then(r => r.data),
  getTenant: (id) => api.get(`/admin/tenants/${id}`).then(r => r.data),
  createTenant: (data) => api.post('/admin/tenants', data).then(r => r.data),
  setTenantStatus: (id, isActive) =>
    api.patch(`/admin/tenants/${id}/status`, { isActive }).then(r => r.data),
  updateTenant: (id, data) =>
    api.put(`/admin/tenants/${id}`, data).then(r => r.data),
  resetTenantPassword: (id, newPassword) =>
    api.post(`/admin/tenants/${id}/reset-password`, { newPassword }).then(r => r.data),
};

export default adminService;
