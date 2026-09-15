import api from '../config/api';

const featureService = {
  getMine: async () => {
    const response = await api.get('/features');
    return response.data;
  },
};

export default featureService;
