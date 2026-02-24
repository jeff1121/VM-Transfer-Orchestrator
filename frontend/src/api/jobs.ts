import { api } from './client'
import type { Job, JobProgress, CreateJobRequest } from '@/types'

export const jobsApi = {
  list: (page = 1, pageSize = 20, status?: string) =>
    api.get<Job[]>('/jobs', { params: { page, pageSize, status } }),
  get: (id: string) => api.get<Job>(`/jobs/${id}`),
  create: (data: CreateJobRequest) => api.post<Job>('/jobs', data),
  cancel: (id: string) => api.post(`/jobs/${id}/cancel`),
  pause: (id: string) => api.post(`/jobs/${id}/pause`),
  resume: (id: string) => api.post(`/jobs/${id}/resume`),
  retry: (id: string) => api.post(`/jobs/${id}/retry`),
  progress: (id: string) => api.get<JobProgress>(`/jobs/${id}/progress`),
}
