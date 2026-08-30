<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import NeuSelect, { type SelectOption } from '@/components/common/NeuSelect.vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart, PieChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'

use([CanvasRenderer, LineChart, PieChart, GridComponent, TooltipComponent, LegendComponent])

const { t } = useI18n()

const selectedFilter = ref('7d')
const filterOptions: SelectOption[] = [
  { value: '24h', label: 'Past 24 Hours', icon: '⏱️' },
  { value: '7d', label: 'Past 7 Days', icon: '📅' },
  { value: '30d', label: 'Past 30 Days', icon: '📊' },
]

const chartOption = ref({
  backgroundColor: 'transparent',
  tooltip: { trigger: 'axis' },
  grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
  xAxis: {
    type: 'category',
    data: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
    axisLine: { lineStyle: { color: 'rgba(148, 163, 184, 0.4)' } },
  },
  yAxis: {
    type: 'value',
    splitLine: { lineStyle: { color: 'rgba(148, 163, 184, 0.15)' } },
  },
  series: [
    {
      name: 'Throughput',
      type: 'line',
      smooth: true,
      data: [120, 132, 101, 134, 190, 230, 210],
      lineStyle: { width: 3, color: '#6366f1' },
      areaStyle: {
        color: {
          type: 'linear',
          x: 0,
          y: 0,
          x2: 0,
          y2: 1,
          colorStops: [
            { offset: 0, color: 'rgba(99, 102, 241, 0.35)' },
            { offset: 1, color: 'rgba(99, 102, 241, 0.0)' },
          ],
        },
      },
    },
  ],
})
</script>

<template>
  <div class="dashboard-container">
    <!-- Header with Action Toolbar -->
    <div class="dash-header">
      <div>
        <div class="badge-pill">
          <span class="pulse-dot online"></span>
          <span>ENTERPRISE DASHBOARD</span>
        </div>
        <h1 class="page-title">{{ t('nav.dashboard') }}</h1>
        <p class="page-subtitle">Real-time system telemetry and infrastructure throughput orchestration.</p>
      </div>

      <div class="header-actions">
        <NeuSelect v-model="selectedFilter" :options="filterOptions" />
        <button class="neu-btn btn-primary">
          <span>⚡ Quick Action</span>
        </button>
      </div>
    </div>

    <!-- Stat Cards Grid -->
    <div class="stats-grid">
      <div class="glass-card stat-card">
        <div class="stat-icon-wrapper blue">⚡</div>
        <div class="stat-info">
          <span class="stat-label">Active Nodes</span>
          <span class="stat-val">12 <span class="stat-unit">Units</span></span>
          <span class="stat-change pos">↑ +4.2%</span>
        </div>
      </div>

      <div class="glass-card stat-card">
        <div class="stat-icon-wrapper green">✅</div>
        <div class="stat-info">
          <span class="stat-label">Tasks Completed</span>
          <span class="stat-val">1,280 <span class="stat-unit">Jobs</span></span>
          <span class="stat-change pos">↑ +18.5%</span>
        </div>
      </div>

      <div class="glass-card stat-card">
        <div class="stat-icon-wrapper amber">⏳</div>
        <div class="stat-info">
          <span class="stat-label">Queued Jobs</span>
          <span class="stat-val">3 <span class="stat-unit">Pending</span></span>
          <span class="stat-change neutral">→ Stable</span>
        </div>
      </div>

      <div class="glass-card stat-card">
        <div class="stat-icon-wrapper purple">🛡️</div>
        <div class="stat-info">
          <span class="stat-label">Security Health</span>
          <span class="stat-val">100% <span class="stat-unit">Optimal</span></span>
          <span class="stat-change pos">✓ Secured</span>
        </div>
      </div>
    </div>

    <!-- Charts & Analytics Section -->
    <div class="glass-card chart-card">
      <div class="card-header">
        <h2 class="card-title">📈 System Throughput & Workload Telemetry</h2>
      </div>
      <div class="chart-wrapper">
        <v-chart class="echart" :option="chartOption" autoresize />
      </div>
    </div>
  </div>
</template>

<style scoped>
.dashboard-container {
  display: flex;
  flex-direction: column;
  gap: 28px;
}

.dash-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  gap: 20px;
  flex-wrap: wrap;
}

.badge-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: var(--primary-glow);
  color: var(--primary);
  padding: 4px 10px;
  border-radius: 999px;
  font-size: 0.75rem;
  font-weight: 800;
  margin-bottom: 6px;
}

.page-title {
  font-size: 1.8rem;
  font-weight: 800;
  letter-spacing: -0.5px;
  margin-bottom: 4px;
}

.page-subtitle {
  color: var(--text-secondary);
  font-size: 0.9rem;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.glass-card {
  background: var(--bg-surface);
  backdrop-filter: var(--glass-blur);
  -webkit-backdrop-filter: var(--glass-blur);
  border: var(--glass-border);
  border-radius: 20px;
  box-shadow: var(--neu-shadow);
  padding: 24px;
}

/* Stats Grid */
.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 20px;
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 18px;
}

.stat-icon-wrapper {
  width: 54px;
  height: 54px;
  border-radius: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
  box-shadow: var(--neu-shadow-sm);
}

.stat-icon-wrapper.blue { background: rgba(99, 102, 241, 0.15); border: 1px solid rgba(99, 102, 241, 0.3); }
.stat-icon-wrapper.green { background: rgba(16, 185, 129, 0.15); border: 1px solid rgba(16, 185, 129, 0.3); }
.stat-icon-wrapper.amber { background: rgba(245, 158, 11, 0.15); border: 1px solid rgba(245, 158, 11, 0.3); }
.stat-icon-wrapper.purple { background: rgba(168, 85, 247, 0.15); border: 1px solid rgba(168, 85, 247, 0.3); }

.stat-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.stat-label {
  font-size: 0.75rem;
  color: var(--text-muted);
  font-weight: 700;
  text-transform: uppercase;
}

.stat-val {
  font-size: 1.4rem;
  font-weight: 800;
  color: var(--text-primary);
}

.stat-unit {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--text-muted);
}

.stat-change {
  font-size: 0.75rem;
  font-weight: 700;
}

.stat-change.pos { color: var(--success); }
.stat-change.neutral { color: var(--text-muted); }

/* Chart Card */
.chart-card {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.card-title {
  font-size: 1.1rem;
  font-weight: 800;
}

.chart-wrapper {
  height: 340px;
  width: 100%;
}

.echart {
  width: 100%;
  height: 100%;
}

.neu-btn {
  padding: 12px 20px;
  border-radius: 12px;
  font-size: 0.9rem;
  font-weight: 700;
  border: none;
  cursor: pointer;
  box-shadow: 0 4px 14px var(--primary-glow);
  display: inline-flex;
  align-items: center;
  gap: 6px;
  white-space: nowrap;
}

.btn-primary { background: var(--primary-gradient); color: white; }

@media (max-width: 1200px) {
  .stats-grid { grid-template-columns: repeat(2, 1fr); }
}

@media (max-width: 768px) {
  .stats-grid { grid-template-columns: 1fr; }
  .dash-header { flex-direction: column; align-items: stretch; }
  .header-actions { justify-content: space-between; }
}
</style>
