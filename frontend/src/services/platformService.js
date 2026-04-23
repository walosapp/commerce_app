import api from '../config/api';

const BASE = '/platform';

export const platformService = {
  getServiceCatalog: async () => {
    const r = await api.get(`${BASE}/admin/catalog`);
    return r.data;
  },

  getAdminCompanies: async () => {
    const r = await api.get(`${BASE}/admin/companies`);
    return r.data;
  },

  getAdminCompanyPlan: async (companyId) => {
    const r = await api.get(`${BASE}/admin/companies/${companyId}/plan`);
    return r.data;
  },

  assignService: async (companyId, data) => {
    const r = await api.post(`${BASE}/admin/companies/${companyId}/services`, data);
    return r.data;
  },

  updateService: async (companyId, serviceCode, data) => {
    const r = await api.patch(`${BASE}/admin/companies/${companyId}/services/${serviceCode}`, data);
    return r.data;
  },

  generateInvoice: async (companyId, data) => {
    const r = await api.post(`${BASE}/admin/companies/${companyId}/invoices`, data);
    return r.data;
  },

  updateInvoiceStatus: async (invoiceId, data) => {
    const r = await api.patch(`${BASE}/admin/invoices/${invoiceId}/status`, data);
    return r.data;
  },

  getMyPlan: async () => {
    const r = await api.get(`${BASE}/my-plan`);
    return r.data;
  },

  getMyInvoices: async () => {
    const r = await api.get(`${BASE}/my-invoices`);
    return r.data;
  },

  getAiUsage: async () => {
    const r = await api.get(`${BASE}/ai-usage`);
    return r.data;
  },

  updateAiKey: async (data) => {
    const r = await api.put(`${BASE}/ai-key`, data);
    return r.data;
  },

  getPaymentMethods: async () => {
    const r = await api.get(`${BASE}/payment-methods`);
    return r.data;
  },

  addPaymentMethod: async (data) => {
    const r = await api.post(`${BASE}/payment-methods`, data);
    return r.data;
  },

  setDefaultPaymentMethod: async (id) => {
    const r = await api.patch(`${BASE}/payment-methods/${id}/default`);
    return r.data;
  },

  deletePaymentMethod: async (id) => {
    const r = await api.delete(`${BASE}/payment-methods/${id}`);
    return r.data;
  },
};

export default platformService;
