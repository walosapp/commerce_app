import api from '../config/api';

const adminService = {
  getTenants: () => api.get('/admin/tenants').then(r => r.data),
  getTenant: (id) => api.get(`/admin/tenants/${id}`).then(r => r.data),
  createTenant: (data) => api.post('/admin/tenants', data).then(r => r.data),
  setTenantStatus: (id, isActive) =>
    api.patch(`/admin/tenants/${id}/status`, { isActive }).then(r => r.data),
};

export default adminService;
