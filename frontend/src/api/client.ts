import axios from 'axios'

export const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})

api.interceptors.request.use((config) => {
  const correlationId = crypto.randomUUID().replace(/-/g, '')
  config.headers['X-Correlation-Id'] = correlationId
  return config
})
