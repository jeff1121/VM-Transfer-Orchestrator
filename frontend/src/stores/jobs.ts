import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Job, JobProgress } from '@/types'
import { jobsApi } from '@/api/jobs'

export const useJobsStore = defineStore('jobs', () => {
  const jobs = ref<Job[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchJobs = async (page = 1, pageSize = 20, status?: string) => {
    loading.value = true
    error.value = null
    try {
      const { data } = await jobsApi.list(page, pageSize, status)
      jobs.value = data
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch jobs'
    } finally {
      loading.value = false
    }
  }

  const updateFromProgress = (progress: JobProgress) => {
    const idx = jobs.value.findIndex(j => j.id === progress.jobId)
    if (idx >= 0) {
      jobs.value[idx].status = progress.status
      jobs.value[idx].progress = progress.overallProgress
      jobs.value[idx].steps = progress.steps
    }
  }

  return { jobs, loading, error, fetchJobs, updateFromProgress }
})
