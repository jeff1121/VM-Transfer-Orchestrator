import { api } from './client'

export interface AuditLogEntry {
  id: string
  action: string
  entityType: string
  entityId: string
  userId: string | null
  details: string | null
  createdAt: string
}

export interface AuditListResponse {
  items: AuditLogEntry[]
  total: number
  page: number
  pageSize: number
}

export interface AuditSummary {
  totalCount: number
  actionCounts: Record<string, number>
  recentCount: number
}

export const auditApi = {
  list: (params: {
    page?: number
    pageSize?: number
    action?: string
    entityType?: string
    userId?: string
    from?: string
    to?: string
  }) => api.get<AuditListResponse>('/audit', { params }),

  summary: () => api.get<AuditSummary>('/audit/summary'),

  exportCsv: (params: {
    action?: string
    entityType?: string
    userId?: string
    from?: string
    to?: string
  }) => api.get('/audit/export', { params, responseType: 'blob' }),
}
