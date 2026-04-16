import api from '../config/api';

const userService = {
  getAll: ()                    => api.get('/users').then(r => r.data),
  getRoles: ()                  => api.get('/users/roles').then(r => r.data),
  getById: (id)                 => api.get(`/users/${id}`).then(r => r.data),
  create: (data)                => api.post('/users', data).then(r => r.data),
  update: (id, data)            => api.put(`/users/${id}`, data).then(r => r.data),
  setStatus: (id, isActive)     => api.patch(`/users/${id}/status`, isActive).then(r => r.data),
  delete: (id)                  => api.delete(`/users/${id}`).then(r => r.data),
};

export default userService;
