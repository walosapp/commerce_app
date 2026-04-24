import api from '../config/api';

export const aiService = {
  chat: async (message, sessionId = null) => {
    const response = await api.post('/ai/chat', { message, sessionId });
    return response.data;
  },
};

export default aiService;
