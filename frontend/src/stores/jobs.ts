import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Job, JobProgress } from '@/types'
import { jobsApi } from '@/api/jobs'

interface PaginatedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export const useJobsStore = defineStore('jobs', () => {
  const jobs = ref<Job[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchJobs = async (page = 1, pageSize = 20, status?: string) => {
    loading.value = true
    error.value = null
    try {
      const res = await jobsApi.list(page, pageSize, status)
      const data = res.data as unknown as (Job[] | PaginatedResponse<Job>)
      if (Array.isArray(data)) {
        jobs.value = data
      } else if (data && Array.isArray((data as PaginatedResponse<Job>).items)) {
        jobs.value = (data as PaginatedResponse<Job>).items
      } else {
        jobs.value = []
      }
    } catch (e) {
      jobs.value = []
      error.value = e instanceof Error ? e.message : 'Failed to fetch jobs'
    } finally {
      loading.value = false
    }
  }

  const updateFromProgress = (progress: JobProgress) => {
    const idx = jobs.value.findIndex((j) => j.id === progress.jobId)
    if (idx >= 0) {
      jobs.value[idx].status = progress.status
      jobs.value[idx].progress = progress.overallProgress
      jobs.value[idx].steps = progress.steps
    }
  }

  return { jobs, loading, error, fetchJobs, updateFromProgress }
})
