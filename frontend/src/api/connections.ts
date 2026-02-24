import { api } from './client'
import type { Connection, CreateConnectionRequest, VmInfo } from '@/types'

export const connectionsApi = {
  list: (page = 1, pageSize = 20) =>
    api.get<Connection[]>('/connections', { params: { page, pageSize } }),
  get: (id: string) => api.get<Connection>(`/connections/${id}`),
  create: (data: CreateConnectionRequest) => api.post<Connection>('/connections', data),
  validate: (id: string) => api.post(`/connections/${id}/validate`),
  delete: (id: string) => api.delete(`/connections/${id}`),
  listVms: (id: string) => api.get<VmInfo[]>(`/connections/${id}/vms`),
}
