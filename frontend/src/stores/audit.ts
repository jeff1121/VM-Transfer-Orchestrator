import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { AuditLogEntry, AuditSummary } from '@/api/audit'
import { auditApi } from '@/api/audit'

export const useAuditStore = defineStore('audit', () => {
  const entries = ref<AuditLogEntry[]>([])
  const summary = ref<AuditSummary | null>(null)
  const total = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchEntries = async (params: {
    page?: number
    pageSize?: number
    action?: string
    entityType?: string
    userId?: string
    from?: string
    to?: string
  } = {}) => {
    loading.value = true
    error.value = null
    try {
      const { data } = await auditApi.list(params)
      entries.value = data.items
      total.value = data.total
    } catch (e) {
      error.value = e instanceof Error ? e.message : '無法載入稽核日誌'
    } finally {
      loading.value = false
    }
  }

  const fetchSummary = async () => {
    try {
      const { data } = await auditApi.summary()
      summary.value = data
    } catch (e) {
      error.value = e instanceof Error ? e.message : '無法載入統計摘要'
    }
  }

  const exportCsv = async (params: {
    action?: string
    entityType?: string
    userId?: string
    from?: string
    to?: string
  } = {}) => {
    try {
      const { data } = await auditApi.exportCsv(params)
      const blob = new Blob([data], { type: 'text/csv' })
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = 'audit-logs.csv'
      a.click()
      URL.revokeObjectURL(url)
    } catch (e) {
      error.value = e instanceof Error ? e.message : '匯出失敗'
    }
  }

  return { entries, summary, total, loading, error, fetchEntries, fetchSummary, exportCsv }
})
