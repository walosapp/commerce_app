import api from '../config/api';

const creditService = {
  getCredits: (params) => api.get('/sales/credits', { params }).then(r => r.data),
  addPayment: (id, body) => api.post(`/sales/credits/${id}/pay`, body).then(r => r.data),
  cancel: (id) => api.delete(`/sales/credits/${id}`).then(r => r.data),
};

export default creditService;
