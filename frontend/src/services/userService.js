import api from '../config/api';

const userService = {
  getAll: ()                    => api.get('/users').then(r => r.data),
  getRoles: ()                  => api.get('/users/roles').then(r => r.data),
  getById: (id)                 => api.get(`/users/${id}`).then(r => r.data),
  create: (data)                => api.post('/users', data).then(r => r.data),
  update: (id, data)            => api.put(`/users/${id}`, data).then(r => r.data),
  setStatus: (id, isActive)     => api.patch(`/users/${id}/status`, isActive).then(r => r.data),
  delete: (id)                  => api.delete(`/users/${id}`).then(r => r.data),

  // Superadmin — todos los comercios
  adminGetAll: (companyId)                       => api.get('/admin/users', { params: companyId ? { companyId } : {} }).then(r => r.data),
  adminGetRolesForCompany: (companyId)           => api.get('/admin/users/roles', { params: { companyId } }).then(r => r.data),
  adminCreate: (data)                            => api.post('/admin/users', data).then(r => r.data),
  adminUpdate: (id, companyId, data)             => api.put(`/users/${id}`, data).then(r => r.data),
  adminSetStatus: (id, companyId, isActive)      => api.patch(`/admin/users/${id}/status`, isActive, { params: { companyId } }).then(r => r.data),
  adminResetPassword: (id, companyId, newPassword) => api.post(`/admin/users/${id}/reset-password`, { newPassword }, { params: { companyId } }).then(r => r.data),
  adminDelete: (id, companyId)                   => api.delete(`/users/${id}`).then(r => r.data),
};

export default userService;
