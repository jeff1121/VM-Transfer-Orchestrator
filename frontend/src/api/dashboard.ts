import { api } from './client'

export interface DailyTrend {
  date: string
  succeeded: number
  failed: number
  total: number
}

export interface DashboardStats {
  statusCounts: Record<string, number>
  dailyTrend: DailyTrend[]
  totalJobs: number
  totalTransferredBytes: number
  averageDurationMinutes: number
}

export const dashboardApi = {
  /** 取得儀表板統計資料 */
  stats: () => api.get<DashboardStats>('/dashboard/stats'),
}
