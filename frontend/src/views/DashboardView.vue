<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useJobsStore } from '@/stores/jobs'
import { useSignalR } from '@/composables/useSignalR'
import type { JobStatus } from '@/types'
import { dashboardApi, type DashboardStats } from '@/api/dashboard'

// ECharts 按需引入
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { PieChart, LineChart } from 'echarts/charts'
import {
  TitleComponent,
  TooltipComponent,
  LegendComponent,
  GridComponent,
} from 'echarts/components'

use([CanvasRenderer, PieChart, LineChart, TitleComponent, TooltipComponent, LegendComponent, GridComponent])

const { t } = useI18n()
const router = useRouter()
const jobsStore = useJobsStore()
const { connect, connected, onJobProgress } = useSignalR()

// 圖表統計資料
const stats = ref<DashboardStats | null>(null)
const statsLoading = ref(false)
const statsError = ref<string | null>(null)

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

// 狀態顏色對照表
const statusColorMap: Record<string, string> = {
  Running: '#3b82f6',
  Queued: '#f59e0b',
  Failed: '#ef4444',
  Succeeded: '#22c55e',
  Created: '#8b5cf6',
  Pausing: '#6366f1',
  Paused: '#9ca3af',
  Resuming: '#06b6d4',
  Cancelling: '#f97316',
  Cancelled: '#6b7280',
}

// 圓餅圖選項（使用 computed 以支援語言切換）
const pieOption = computed(() => {
  if (!stats.value) return {}
  const data = Object.entries(stats.value.statusCounts)
    .filter(([, v]) => v > 0)
    .map(([name, value]) => ({
      name,
      value,
      itemStyle: { color: statusColorMap[name] ?? '#9ca3af' },
    }))
  return {
    title: { text: t('dashboard.charts.statusDistribution'), left: 'center', textStyle: { fontSize: 14 } },
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
    legend: { bottom: 0, type: 'scroll' },
    series: [{ type: 'pie', radius: ['40%', '70%'], data, emphasis: { itemStyle: { shadowBlur: 10, shadowColor: 'rgba(0,0,0,.2)' } } }],
  }
})

// 折線圖選項（使用 computed 以支援語言切換）
const lineOption = computed(() => {
  if (!stats.value) return {}
  const trend = stats.value.dailyTrend
  const succeededLabel = t('dashboard.succeeded')
  const failedLabel = t('dashboard.failed')
  const totalLabel = t('dashboard.charts.totalJobs')
  return {
    title: { text: t('dashboard.charts.dailyTrend'), left: 'center', textStyle: { fontSize: 14 } },
    tooltip: { trigger: 'axis' },
    legend: { data: [succeededLabel, failedLabel, totalLabel], bottom: 0 },
    grid: { left: 40, right: 20, top: 40, bottom: 40, containLabel: true },
    xAxis: { type: 'category', data: trend.map((d) => d.date.slice(5)), axisLabel: { rotate: 45 } },
    yAxis: { type: 'value', minInterval: 1 },
    series: [
      { name: succeededLabel, type: 'line', data: trend.map((d) => d.succeeded), smooth: true, itemStyle: { color: '#22c55e' }, areaStyle: { color: 'rgba(34,197,94,.1)' } },
      { name: failedLabel, type: 'line', data: trend.map((d) => d.failed), smooth: true, itemStyle: { color: '#ef4444' }, areaStyle: { color: 'rgba(239,68,68,.1)' } },
      { name: totalLabel, type: 'line', data: trend.map((d) => d.total), smooth: true, itemStyle: { color: '#3b82f6' }, lineStyle: { type: 'dashed' } },
    ],
  }
})

// 格式化傳輸量
const formatBytes = (bytes: number) => {
  if (bytes >= 1073741824) return (bytes / 1073741824).toFixed(2) + ' GB'
  if (bytes >= 1048576) return (bytes / 1048576).toFixed(2) + ' MB'
  if (bytes >= 1024) return (bytes / 1024).toFixed(2) + ' KB'
  return bytes + ' B'
}

onMounted(async () => {
  await jobsStore.fetchJobs()

  // 載入圖表統計資料
  statsLoading.value = true
  try {
    const res = await dashboardApi.stats()
    stats.value = res.data
  } catch {
    statsError.value = t('common.loading')
  } finally {
    statsLoading.value = false
  }

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
    <h1>{{ t('dashboard.title') }}</h1>
    <div class="signal-status">
      <span :class="connected ? 'dot-green' : 'dot-red'"></span>
      {{ connected ? t('common.online') : t('common.offline') }}
    </div>

    <div class="summary-cards">
      <div class="card card-running">
        <div class="card-count">{{ statusCounts.Running }}</div>
        <div class="card-label">{{ t('dashboard.running') }}</div>
      </div>
      <div class="card card-queued">
        <div class="card-count">{{ statusCounts.Queued }}</div>
        <div class="card-label">{{ t('dashboard.queued') }}</div>
      </div>
      <div class="card card-failed">
        <div class="card-count">{{ statusCounts.Failed }}</div>
        <div class="card-label">{{ t('dashboard.failed') }}</div>
      </div>
      <div class="card card-succeeded">
        <div class="card-count">{{ statusCounts.Succeeded }}</div>
        <div class="card-label">{{ t('dashboard.succeeded') }}</div>
      </div>
    </div>

    <div v-if="jobsStore.error" class="error">{{ jobsStore.error }}</div>

    <!-- 📊 遷移統計圖表 -->
    <section class="charts-section">
      <h2>📊 {{ t('dashboard.charts.title') }}</h2>

      <!-- 額外統計卡片 -->
      <div v-if="stats" class="stats-cards">
        <div class="stat-card">
          <div class="stat-value">{{ stats.totalJobs }}</div>
          <div class="stat-label">{{ t('dashboard.charts.totalJobs') }}</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ stats.averageDurationMinutes }} {{ t('dashboard.charts.minutes') }}</div>
          <div class="stat-label">{{ t('dashboard.charts.avgDuration') }}</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ formatBytes(stats.totalTransferredBytes) }}</div>
          <div class="stat-label">{{ t('dashboard.charts.totalTransfer') }}</div>
        </div>
      </div>

      <div v-if="statsLoading" class="loading">{{ t('common.loading') }}</div>
      <div v-else-if="statsError" class="error">{{ statsError }}</div>
      <div v-else-if="stats" class="charts-grid">
        <div class="chart-container">
          <v-chart :option="pieOption" autoresize class="chart" />
        </div>
        <div class="chart-container">
          <v-chart :option="lineOption" autoresize class="chart" />
        </div>
      </div>
    </section>

    <h2>{{ t('dashboard.recentJobs') }}</h2>
    <div v-if="jobsStore.loading" class="loading">{{ t('common.loading') }}</div>
    <table v-else class="jobs-table">
      <thead>
        <tr>
          <th>{{ t('dashboard.table.id') }}</th>
          <th>{{ t('dashboard.table.strategy') }}</th>
          <th>{{ t('dashboard.table.status') }}</th>
          <th>{{ t('dashboard.table.progress') }}</th>
          <th>{{ t('dashboard.table.createdAt') }}</th>
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
          <td colspan="5" class="empty">{{ t('dashboard.noJobs') }}</td>
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
.charts-section { margin-bottom: 24px; }
.stats-cards { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; margin-bottom: 20px; }
.stat-card { background: white; border-radius: 8px; padding: 16px; text-align: center; box-shadow: 0 1px 3px rgba(0,0,0,.1); }
.stat-value { font-size: 1.5rem; font-weight: 700; color: #1e293b; }
.stat-label { color: #64748b; margin-top: 4px; font-size: 0.85rem; }
.charts-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
.chart-container { background: white; border-radius: 8px; padding: 16px; box-shadow: 0 1px 3px rgba(0,0,0,.1); }
.chart { width: 100%; height: 320px; }
@media (max-width: 768px) {
  .charts-grid { grid-template-columns: 1fr; }
  .stats-cards { grid-template-columns: 1fr; }
  .summary-cards { grid-template-columns: repeat(2, 1fr); }
}
</style>
