import axios from 'axios'

export const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})

api.interceptors.request.use((config) => {
  const correlationId = crypto.randomUUID().replace(/-/g, '')
  config.headers['X-Correlation-Id'] = correlationId

  // 自動附加 JWT token 到 Authorization header
  const token = localStorage.getItem('vmto_token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  return config
})

// 401 回應時自動導向登入頁
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('vmto_token')
      localStorage.removeItem('vmto_role')
      localStorage.removeItem('vmto_user')
      window.location.href = '/login'
    }
    return Promise.reject(error)
  },
)
