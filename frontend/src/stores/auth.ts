import { defineStore } from 'pinia'

// 認證狀態管理
export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('vmto_token') || '',
    role: localStorage.getItem('vmto_role') || '',
    userName: localStorage.getItem('vmto_user') || '',
  }),
  getters: {
    isAuthenticated: (state) => !!state.token,
    isAdmin: (state) => state.role === 'Admin',
    isOperator: (state) => state.role === 'Admin' || state.role === 'Operator',
  },
  actions: {
    login(token: string, role: string, userName: string) {
      this.token = token
      this.role = role
      this.userName = userName
      localStorage.setItem('vmto_token', token)
      localStorage.setItem('vmto_role', role)
      localStorage.setItem('vmto_user', userName)
    },
    logout() {
      this.token = ''
      this.role = ''
      this.userName = ''
      localStorage.removeItem('vmto_token')
      localStorage.removeItem('vmto_role')
      localStorage.removeItem('vmto_user')
    },
  },
})
