import { defineStore } from 'pinia';
import { api } from '../lib/api';

export const useAuth = defineStore('auth', {
  state: () => ({ user: null, ready: false }),
  getters: {
    isTeacher: (s) => s.user?.role === 'Teacher',
  },
  actions: {
    async fetchMe() {
      try {
        this.user = await api.get('/api/auth/me');
      } catch {
        this.user = null;
      } finally {
        this.ready = true;
      }
    },
    async login(email, password) {
      this.user = await api.post('/api/auth/login', { email, password });
    },
    async register(payload) {
      this.user = await api.post('/api/auth/register', payload);
    },
    async logout() {
      await api.post('/api/auth/logout');
      this.user = null;
    },
  },
});
