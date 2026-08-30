import { defineStore } from 'pinia'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('app_token') || 'mock-jwt-token',
    role: localStorage.getItem('app_role') || 'Admin',
    userName: localStorage.getItem('app_user') || 'admin',
  }),
  getters: {
    isAuthenticated: (state) => !!state.token,
    isAdmin: (state) => state.role === 'Admin',
  },
  actions: {
    login(token: string, role: string, userName: string) {
      this.token = token
      this.role = role
      this.userName = userName
      localStorage.setItem('app_token', token)
      localStorage.setItem('app_role', role)
      localStorage.setItem('app_user', userName)
    },
    logout() {
      this.token = ''
      this.role = ''
      this.userName = ''
      localStorage.removeItem('app_token')
      localStorage.removeItem('app_role')
      localStorage.removeItem('app_user')
    },
  },
})
