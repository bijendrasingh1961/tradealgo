import axios from 'axios';

const API_BASE_URL = 'https://localhost:7236/api';

export const settingsService = {
  getSettings: async () => {
    const response = await axios.get(`${API_BASE_URL}/settings`);
    return response.data;
  },
  saveSettings: async (settings: { clientId: string; accessToken: string }) => {
    const response = await axios.post(`${API_BASE_URL}/settings`, settings);
    return response.data;
  }
};

export const tradingService = {
  getOptionChain: async (underlyingId: string, expiry: string) => {
    const response = await axios.get(`${API_BASE_URL}/trading/option-chain/${underlyingId}/${expiry}`);
    return response.data;
  },
  sellStraddle: async (data: { callSecurityId: string; putSecurityId: string; quantity: number }) => {
    const response = await axios.post(`${API_BASE_URL}/trading/sell-straddle`, data);
    return response.data;
  }
};
