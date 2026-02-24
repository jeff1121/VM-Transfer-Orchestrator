<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useJobsStore } from '@/stores/jobs'
import { useSignalR } from '@/composables/useSignalR'
import type { JobStatus } from '@/types'

const router = useRouter()
const jobsStore = useJobsStore()
const { connect, connected, onJobProgress } = useSignalR()

const statusCounts = computed(() => {
  const counts: Record<string, number> = { Running: 0, Queued: 0, Failed: 0, Succeeded: 0 }
  for (const job of jobsStore.jobs) {
    if (counts[job.status] !== undefined) counts[job.status]++
  }
  return counts
})

const recentJobs = computed(() =>
  [...jobsStore.jobs].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()).slice(0, 10)
)

const statusClass = (status: JobStatus) => {
  const map: Record<string, string> = {
    Running: 'badge-running', Queued: 'badge-queued', Failed: 'badge-failed',
    Succeeded: 'badge-succeeded', Paused: 'badge-paused', Cancelled: 'badge-cancelled',
  }
  return map[status] ?? 'badge-default'
}

const formatDate = (iso: string) => new Date(iso).toLocaleString('zh-TW')

onMounted(async () => {
  await jobsStore.fetchJobs()
  try {
    await connect()
    onJobProgress((progress) => jobsStore.updateFromProgress(progress))
  } catch {
    // SignalR not available
  }
})
</script>

<template>
  <div class="dashboard">
    <h1>儀表板</h1>
    <div class="signal-status">
      <span :class="connected ? 'dot-green' : 'dot-red'"></span>
      {{ connected ? '即時連線中' : '離線' }}
    </div>

    <div class="summary-cards">
      <div class="card card-running">
        <div class="card-count">{{ statusCounts.Running }}</div>
        <div class="card-label">執行中</div>
      </div>
      <div class="card card-queued">
        <div class="card-count">{{ statusCounts.Queued }}</div>
        <div class="card-label">排隊中</div>
      </div>
      <div class="card card-failed">
        <div class="card-count">{{ statusCounts.Failed }}</div>
        <div class="card-label">失敗</div>
      </div>
      <div class="card card-succeeded">
        <div class="card-count">{{ statusCounts.Succeeded }}</div>
        <div class="card-label">完成</div>
      </div>
    </div>

    <div v-if="jobsStore.error" class="error">{{ jobsStore.error }}</div>

    <h2>最近任務</h2>
    <div v-if="jobsStore.loading" class="loading">載入中…</div>
    <table v-else class="jobs-table">
      <thead>
        <tr>
          <th>ID</th>
          <th>策略</th>
          <th>狀態</th>
          <th>進度</th>
          <th>建立時間</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="job in recentJobs" :key="job.id" class="job-row" @click="router.push(`/jobs/${job.id}`)">
          <td class="job-id">{{ job.id.slice(0, 8) }}…</td>
          <td>{{ job.strategy }}</td>
          <td><span :class="['badge', statusClass(job.status)]">{{ job.status }}</span></td>
          <td>
            <div class="progress-bar">
              <div class="progress-fill" :style="{ width: job.progress + '%' }"></div>
            </div>
            <span class="progress-text">{{ job.progress }}%</span>
          </td>
          <td>{{ formatDate(job.createdAt) }}</td>
        </tr>
        <tr v-if="recentJobs.length === 0">
          <td colspan="5" class="empty">尚無任務</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.dashboard { max-width: 1000px; }
h1 { margin-bottom: 8px; }
h2 { margin: 24px 0 12px; }
.signal-status { margin-bottom: 16px; font-size: 0.85rem; color: #666; display: flex; align-items: center; gap: 6px; }
.dot-green, .dot-red { width: 8px; height: 8px; border-radius: 50%; display: inline-block; }
.dot-green { background: #22c55e; }
.dot-red { background: #ef4444; }
.summary-cards { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; margin-bottom: 24px; }
.card { background: white; border-radius: 8px; padding: 20px; text-align: center; box-shadow: 0 1px 3px rgba(0,0,0,.1); }
.card-count { font-size: 2rem; font-weight: 700; }
.card-label { color: #666; margin-top: 4px; }
.card-running .card-count { color: #3b82f6; }
.card-queued .card-count { color: #f59e0b; }
.card-failed .card-count { color: #ef4444; }
.card-succeeded .card-count { color: #22c55e; }
.error { background: #fef2f2; color: #b91c1c; padding: 12px; border-radius: 6px; margin-bottom: 16px; }
.loading { color: #666; padding: 20px; }
.jobs-table { width: 100%; border-collapse: collapse; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,.1); }
.jobs-table th { background: #f9fafb; text-align: left; padding: 12px; font-weight: 600; border-bottom: 1px solid #e5e7eb; }
.jobs-table td { padding: 12px; border-bottom: 1px solid #f3f4f6; }
.job-row { cursor: pointer; transition: background 0.15s; }
.job-row:hover { background: #f9fafb; }
.job-id { font-family: monospace; font-size: 0.85rem; }
.badge { padding: 2px 8px; border-radius: 4px; font-size: 0.8rem; font-weight: 500; }
.badge-running { background: #dbeafe; color: #1d4ed8; }
.badge-queued { background: #fef3c7; color: #92400e; }
.badge-failed { background: #fef2f2; color: #b91c1c; }
.badge-succeeded { background: #dcfce7; color: #166534; }
.badge-paused { background: #f3f4f6; color: #374151; }
.badge-cancelled { background: #f3f4f6; color: #6b7280; }
.badge-default { background: #f3f4f6; color: #374151; }
.progress-bar { width: 100px; height: 6px; background: #e5e7eb; border-radius: 3px; display: inline-block; vertical-align: middle; }
.progress-fill { height: 100%; background: #3b82f6; border-radius: 3px; transition: width 0.3s; }
.progress-text { font-size: 0.8rem; color: #666; margin-left: 6px; }
.empty { text-align: center; color: #999; padding: 24px; }
</style>
