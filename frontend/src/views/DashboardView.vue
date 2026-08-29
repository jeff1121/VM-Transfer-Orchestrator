<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useJobsStore } from '@/stores/jobs'
import { useSignalR } from '@/composables/useSignalR'
import { useTheme } from '@/composables/useTheme'
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
const { connect } = useSignalR()
const { resolvedTheme } = useTheme()
const chartTheme = computed(() => resolvedTheme.value === 'dark' ? 'dark' : undefined)

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
  [...jobsStore.jobs].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()).slice(0, 8)
)

const statusBadge = (status: JobStatus) => {
  const map: Record<string, { class: string; icon: string }> = {
    Running: { class: 'status-running', icon: '⚡' },
    Queued: { class: 'status-queued', icon: '⏳' },
    Failed: { class: 'status-failed', icon: '❌' },
    Succeeded: { class: 'status-succeeded', icon: '✅' },
    Paused: { class: 'status-paused', icon: '⏸️' },
    Cancelled: { class: 'status-cancelled', icon: '🚫' },
  }
  return map[status] ?? { class: 'status-default', icon: '•' }
}

const formatDate = (iso: string) => new Date(iso).toLocaleString('zh-TW', { hour12: false })

const formatBytes = (bytes: number) => {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`
}

const pieOption = computed(() => {
  if (!stats.value) return {}
  const data = Object.entries(stats.value.statusCounts)
    .filter(([, v]) => v > 0)
    .map(([name, value]) => ({ name, value }))

  return {
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'item',
      backgroundColor: resolvedTheme.value === 'dark' ? 'rgba(15, 23, 42, 0.9)' : 'rgba(255, 255, 255, 0.9)',
      borderColor: 'rgba(255, 255, 255, 0.2)',
      textStyle: { color: resolvedTheme.value === 'dark' ? '#f8fafc' : '#0f172a' }
    },
    legend: {
      bottom: '5%',
      left: 'center',
      textStyle: { color: resolvedTheme.value === 'dark' ? '#94a3b8' : '#64748b' }
    },
    series: [
      {
        name: t('dashboard.charts.statusDistribution'),
        type: 'pie',
        radius: ['45%', '70%'],
        avoidLabelOverlap: false,
        itemStyle: {
          borderRadius: 8,
          borderColor: resolvedTheme.value === 'dark' ? '#111827' : '#ffffff',
          borderWidth: 2
        },
        label: { show: false },
        data,
      },
    ],
  }
})

const lineOption = computed(() => {
  if (!stats.value) return {}
  return {
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'axis',
      backgroundColor: resolvedTheme.value === 'dark' ? 'rgba(15, 23, 42, 0.9)' : 'rgba(255, 255, 255, 0.9)',
      textStyle: { color: resolvedTheme.value === 'dark' ? '#f8fafc' : '#0f172a' }
    },
    grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: stats.value.dailyTrend.map((d: { date: string }) => d.date),
      axisLine: { lineStyle: { color: resolvedTheme.value === 'dark' ? '#334155' : '#e2e8f0' } },
      axisLabel: { color: resolvedTheme.value === 'dark' ? '#94a3b8' : '#64748b' }
    },
    yAxis: {
      type: 'value',
      splitLine: { lineStyle: { color: resolvedTheme.value === 'dark' ? '#1e293b' : '#f1f5f9' } },
      axisLabel: { color: resolvedTheme.value === 'dark' ? '#94a3b8' : '#64748b' }
    },
    series: [
      {
        name: t('dashboard.charts.dailyTrend'),
        type: 'line',
        smooth: true,
        data: stats.value.dailyTrend.map((d: { total: number }) => d.total),
        lineStyle: { width: 3, color: '#6366f1' },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [
              { offset: 0, color: 'rgba(99, 102, 241, 0.4)' },
              { offset: 1, color: 'rgba(99, 102, 241, 0.0)' }
            ]
          }
        },
        itemStyle: { color: '#6366f1' }
      },
    ],
  }
})

const fetchStats = async () => {
  statsLoading.value = true
  statsError.value = null
  try {
    const { data } = await dashboardApi.stats()
    stats.value = data
  } catch (e) {
    statsError.value = e instanceof Error ? e.message : t('dashboard.charts.loadFailed')
  } finally {
    statsLoading.value = false
  }
}

onMounted(async () => {
  await jobsStore.fetchJobs()
  await fetchStats()
  try {
    await connect()
  } catch {
    // signalr fallback
  }
})
</script>

<template>
  <div class="dashboard-container">
    <!-- Hero Banner with Glass Aesthetics -->
    <section class="dashboard-hero">
      <div class="hero-content">
        <div class="hero-tag">
          <span class="pulse-icon">⚡</span>
          <span>ENTERPRISE ORCHESTRATOR</span>
        </div>
        <h1 class="hero-title">{{ t('dashboard.title') }}</h1>
        <p class="hero-desc">
          Automated multi-platform virtual machine migration engine with real-time distributed saga telemetry.
        </p>
      </div>

      <div class="hero-action">
        <button class="neu-btn btn-primary" @click="router.push('/jobs/new')">
          <span>🚀 {{ t('nav.newJob') }}</span>
        </button>
      </div>
    </section>

    <!-- KPI Metric Cards (Neumorphic) -->
    <section class="kpi-grid">
      <div class="kpi-card card-running">
        <div class="kpi-icon-box">⚡</div>
        <div class="kpi-info">
          <div class="kpi-value">{{ statusCounts.Running }}</div>
          <div class="kpi-label">{{ t('dashboard.running') }}</div>
        </div>
        <div class="kpi-glow"></div>
      </div>

      <div class="kpi-card card-queued">
        <div class="kpi-icon-box">⏳</div>
        <div class="kpi-info">
          <div class="kpi-value">{{ statusCounts.Queued }}</div>
          <div class="kpi-label">{{ t('dashboard.queued') }}</div>
        </div>
        <div class="kpi-glow"></div>
      </div>

      <div class="kpi-card card-succeeded">
        <div class="kpi-icon-box">✅</div>
        <div class="kpi-info">
          <div class="kpi-value">{{ statusCounts.Succeeded }}</div>
          <div class="kpi-label">{{ t('dashboard.succeeded') }}</div>
        </div>
        <div class="kpi-glow"></div>
      </div>

      <div class="kpi-card card-failed">
        <div class="kpi-icon-box">❌</div>
        <div class="kpi-info">
          <div class="kpi-value">{{ statusCounts.Failed }}</div>
          <div class="kpi-label">{{ t('dashboard.failed') }}</div>
        </div>
        <div class="kpi-glow"></div>
      </div>
    </section>

    <!-- Analytical Charts Section -->
    <section class="glass-section">
      <div class="section-header">
        <div class="section-title">
          <span>📈</span>
          <h2>{{ t('dashboard.charts.title') }}</h2>
        </div>
        <div v-if="stats" class="summary-chips">
          <div class="chip">
            <span class="chip-label">{{ t('dashboard.charts.totalJobs') }}:</span>
            <span class="chip-val">{{ stats.totalJobs }}</span>
          </div>
          <div class="chip">
            <span class="chip-label">{{ t('dashboard.charts.avgDuration') }}:</span>
            <span class="chip-val">{{ stats.averageDurationMinutes }}m</span>
          </div>
          <div class="chip">
            <span class="chip-label">{{ t('dashboard.charts.totalTransfer') }}:</span>
            <span class="chip-val">{{ formatBytes(stats.totalTransferredBytes) }}</span>
          </div>
        </div>
      </div>

      <div v-if="statsLoading" class="glass-loading">
        <span class="spinner">🌀</span>
        <p>{{ t('common.loading') }}</p>
      </div>

      <div v-else-if="stats" class="charts-row">
        <div class="chart-glass-panel">
          <h3 class="chart-title">{{ t('dashboard.charts.statusDistribution') }}</h3>
          <v-chart :option="pieOption" :theme="chartTheme" autoresize class="echart" />
        </div>

        <div class="chart-glass-panel">
          <h3 class="chart-title">{{ t('dashboard.charts.dailyTrend') }}</h3>
          <v-chart :option="lineOption" :theme="chartTheme" autoresize class="echart" />
        </div>
      </div>
    </section>

    <!-- Recent Jobs Table -->
    <section class="glass-section">
      <div class="section-header">
        <div class="section-title">
          <span>📋</span>
          <h2>{{ t('dashboard.recentJobs') }}</h2>
        </div>
      </div>

      <div class="glass-table-wrapper">
        <table class="glass-table">
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
            <tr
              v-for="job in recentJobs"
              :key="job.id"
              class="table-row"
              @click="router.push(`/jobs/${job.id}`)"
            >
              <td :data-label="t('dashboard.table.id')" class="job-id-cell">
                <span class="id-tag">{{ job.id.slice(0, 8) }}</span>
              </td>
              <td :data-label="t('dashboard.table.strategy')" class="strategy-cell">
                <span class="strategy-badge">{{ job.strategy }}</span>
              </td>
              <td :data-label="t('dashboard.table.status')">
                <span :class="['status-pill', statusBadge(job.status).class]">
                  <span class="pill-icon">{{ statusBadge(job.status).icon }}</span>
                  <span class="pill-text">{{ job.status }}</span>
                </span>
              </td>
              <td :data-label="t('dashboard.table.progress')" class="progress-cell">
                <div class="glass-progress-track">
                  <div class="glass-progress-bar" :style="{ width: job.progress + '%' }"></div>
                </div>
                <span class="progress-val">{{ job.progress }}%</span>
              </td>
              <td :data-label="t('dashboard.table.createdAt')" class="time-cell">
                {{ formatDate(job.createdAt) }}
              </td>
            </tr>

            <tr v-if="recentJobs.length === 0">
              <td colspan="5" class="empty-cell">
                <div class="empty-state">
                  <span class="empty-icon">📭</span>
                  <p>{{ t('dashboard.noJobs') }}</p>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </div>
</template>

<style scoped>
.dashboard-container {
  display: flex;
  flex-direction: column;
  gap: 28px;
}

/* Hero Section */
.dashboard-hero {
  background: var(--bg-surface);
  backdrop-filter: var(--glass-blur);
  border: var(--glass-border);
  border-radius: 20px;
  padding: 32px;
  box-shadow: var(--neu-shadow);
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 24px;
}

.hero-tag {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 0.75rem;
  font-weight: 800;
  letter-spacing: 1px;
  color: var(--primary);
  background: var(--primary-glow);
  padding: 4px 12px;
  border-radius: 999px;
  margin-bottom: 12px;
}

.hero-title {
  font-size: 2rem;
  font-weight: 800;
  letter-spacing: -0.5px;
  margin-bottom: 8px;
}

.hero-desc {
  color: var(--text-secondary);
  font-size: 0.95rem;
  max-width: 600px;
  line-height: 1.5;
}

/* Button */
.neu-btn {
  padding: 14px 28px;
  border-radius: 14px;
  font-size: 0.95rem;
  font-weight: 700;
  border: none;
  cursor: pointer;
  box-shadow: 0 6px 20px var(--primary-glow);
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}

.btn-primary {
  background: var(--primary-gradient);
  color: white;
}

.neu-btn:hover {
  transform: translateY(-3px);
  box-shadow: 0 10px 25px var(--primary-glow);
}

.neu-btn:active {
  transform: translateY(0);
}

/* KPI Cards */
.kpi-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 20px;
}

.kpi-card {
  background: var(--bg-surface);
  backdrop-filter: var(--glass-blur);
  border: var(--glass-border);
  border-radius: 18px;
  padding: 24px;
  box-shadow: var(--neu-shadow);
  display: flex;
  align-items: center;
  gap: 20px;
  position: relative;
  overflow: hidden;
  transition: all 0.3s ease;
}

.kpi-card:hover {
  transform: translateY(-4px);
  box-shadow: var(--neu-shadow-hover);
}

.kpi-icon-box {
  width: 54px;
  height: 54px;
  border-radius: 14px;
  background: var(--bg-surface-elevated);
  box-shadow: var(--neu-inset);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
}

.kpi-value {
  font-size: 2.2rem;
  font-weight: 800;
  line-height: 1;
  margin-bottom: 4px;
}

.kpi-label {
  font-size: 0.85rem;
  color: var(--text-secondary);
  font-weight: 600;
}

.card-running .kpi-value { color: var(--primary); }
.card-queued .kpi-value { color: var(--warning); }
.card-succeeded .kpi-value { color: var(--success); }
.card-failed .kpi-value { color: var(--danger); }

/* Glass Sections */
.glass-section {
  background: var(--bg-surface);
  backdrop-filter: var(--glass-blur);
  border: var(--glass-border);
  border-radius: 20px;
  padding: 28px;
  box-shadow: var(--neu-shadow);
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.section-title {
  display: flex;
  align-items: center;
  gap: 10px;
}

.section-title h2 {
  font-size: 1.3rem;
  font-weight: 800;
}

.summary-chips {
  display: flex;
  gap: 10px;
}

.chip {
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  padding: 6px 14px;
  border-radius: 999px;
  font-size: 0.85rem;
  box-shadow: var(--neu-shadow-sm);
  display: flex;
  gap: 6px;
}

.chip-label { color: var(--text-muted); }
.chip-val { font-weight: 700; color: var(--text-primary); }

/* Charts */
.charts-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
}

.chart-glass-panel {
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  border-radius: 16px;
  padding: 20px;
  box-shadow: var(--neu-inset);
}

.chart-title {
  font-size: 1rem;
  font-weight: 700;
  color: var(--text-secondary);
  margin-bottom: 12px;
}

.echart {
  width: 100%;
  height: 320px;
}

/* Glass Table */
.glass-table-wrapper {
  overflow-x: auto;
  border-radius: 14px;
  box-shadow: var(--neu-inset);
}

.glass-table {
  width: 100%;
  border-collapse: separate;
  border-spacing: 0;
}

.glass-table th {
  background: var(--bg-surface-elevated);
  padding: 16px 20px;
  text-align: left;
  font-size: 0.85rem;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--text-muted);
  letter-spacing: 0.5px;
  border-bottom: var(--glass-border-subtle);
}

.glass-table td {
  padding: 16px 20px;
  border-bottom: var(--glass-border-subtle);
  font-size: 0.95rem;
}

.table-row {
  cursor: pointer;
  transition: background 0.2s ease;
}

.table-row:hover {
  background: var(--bg-surface-elevated);
}

.id-tag {
  font-family: monospace;
  font-weight: 700;
  background: var(--bg-surface-elevated);
  padding: 4px 8px;
  border-radius: 6px;
  border: var(--glass-border-subtle);
}

.strategy-badge {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--text-secondary);
}

.status-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 12px;
  border-radius: 999px;
  font-size: 0.8rem;
  font-weight: 700;
}

.status-running { background: var(--primary-glow); color: var(--primary); }
.status-queued { background: var(--warning-bg); color: var(--warning); }
.status-succeeded { background: var(--success-bg); color: var(--success); }
.status-failed { background: var(--danger-bg); color: var(--danger); }
.status-paused { background: rgba(148, 163, 184, 0.15); color: #64748b; }

.progress-cell {
  display: flex;
  align-items: center;
  gap: 12px;
}

.glass-progress-track {
  width: 120px;
  height: 8px;
  border-radius: 999px;
  background: var(--bg-surface-elevated);
  box-shadow: var(--neu-inset);
  overflow: hidden;
}

.glass-progress-bar {
  height: 100%;
  background: var(--primary-gradient);
  border-radius: 999px;
  transition: width 0.4s ease;
}

.progress-val {
  font-size: 0.85rem;
  font-weight: 700;
  color: var(--text-secondary);
}

.time-cell {
  color: var(--text-muted);
  font-size: 0.85rem;
}

.empty-state {
  padding: 40px;
  text-align: center;
  color: var(--text-muted);
}

.empty-icon {
  font-size: 2.5rem;
  display: block;
  margin-bottom: 8px;
}

/* Responsive */
@media (max-width: 992px) {
  .kpi-grid { grid-template-columns: repeat(2, 1fr); }
  .charts-row { grid-template-columns: 1fr; }
  .dashboard-hero { flex-direction: column; align-items: flex-start; }
}

@media (max-width: 768px) {
  .kpi-grid { grid-template-columns: 1fr; }
  .glass-table, .glass-table tbody, .glass-table tr, .glass-table td {
    display: block;
    width: 100%;
  }
  .glass-table thead { display: none; }
  .table-row {
    padding: 16px;
    border-bottom: var(--glass-border);
  }
  .glass-table td {
    border: none;
    padding: 8px 0;
    display: flex;
    justify-content: space-between;
    align-items: center;
  }
  .glass-table td::before {
    content: attr(data-label);
    font-weight: 700;
    color: var(--text-muted);
    font-size: 0.85rem;
  }
}
</style>
