<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuditStore } from '@/stores/audit'
import NeuSelect, { type SelectOption } from '@/components/common/NeuSelect.vue'

const { t } = useI18n()
const store = useAuditStore()

const pageSizeOptions: SelectOption[] = [
  { value: 10, label: '10 / Page' },
  { value: 20, label: '20 / Page' },
  { value: 50, label: '50 / Page' },
]

const filters = reactive({
  action: '',
  entityType: '',
  userId: '',
  from: '',
  to: '',
})

const page = ref(1)
const pageSize = ref(20)

const formatDate = (iso: string) => new Date(iso).toLocaleString('zh-TW', { hour12: false })

const topActions = computed(() => {
  if (!store.summary) return []
  return Object.entries(store.summary.actionCounts)
    .sort(([, a], [, b]) => b - a)
    .slice(0, 3)
})

const totalPages = computed(() => Math.max(1, Math.ceil(store.total / pageSize.value)))

const visiblePages = computed(() => {
  const pages: number[] = []
  const start = Math.max(1, page.value - 2)
  const end = Math.min(totalPages.value, page.value + 2)
  for (let i = start; i <= end; i++) pages.push(i)
  return pages
})

const timelineEntries = computed(() => store.entries.slice(0, 15))

const actionBadgeClass = (action: string) => {
  if (action.startsWith('Create')) return 'badge-create'
  if (action.startsWith('Delete') || action.startsWith('Cancel')) return 'badge-delete'
  if (action.startsWith('Update') || action.startsWith('Modify')) return 'badge-update'
  return 'badge-other'
}

const loadData = async () => {
  const params: Record<string, unknown> = { page: page.value, pageSize: pageSize.value }
  if (filters.action) params.action = filters.action
  if (filters.entityType) params.entityType = filters.entityType
  if (filters.userId) params.userId = filters.userId
  if (filters.from) params.from = filters.from
  if (filters.to) params.to = filters.to
  await store.fetchEntries(params as Parameters<typeof store.fetchEntries>[0])
}

const search = () => {
  page.value = 1
  loadData()
}

const resetFilters = () => {
  filters.action = ''
  filters.entityType = ''
  filters.userId = ''
  filters.from = ''
  filters.to = ''
  page.value = 1
  loadData()
}

const goToPage = (p: number) => {
  page.value = p
  loadData()
}

const changePageSize = (val: string | number) => {
  pageSize.value = Number(val)
  page.value = 1
  loadData()
}

const exportCsv = () => {
  const params: Record<string, string> = {}
  if (filters.action) params.action = filters.action
  if (filters.entityType) params.entityType = filters.entityType
  if (filters.userId) params.userId = filters.userId
  if (filters.from) params.from = filters.from
  if (filters.to) params.to = filters.to
  store.exportCsv(params as Parameters<typeof store.exportCsv>[0])
}

onMounted(() => {
  loadData()
  store.fetchSummary()
})
</script>

<template>
  <div class="audit-container">
    <div class="page-header">
      <div>
        <h1 class="page-title">🛡️ {{ t('audit.title') }}</h1>
        <p class="page-subtitle">Security audit trail, governance timeline, and immutable event logging.</p>
      </div>

      <button class="neu-btn btn-export" @click="exportCsv">
        <span>📥 {{ t('audit.exportCsv') }}</span>
      </button>
    </div>

    <!-- Summary KPI Cards -->
    <div v-if="store.summary" class="kpi-grid">
      <div class="glass-card kpi-card card-recent">
        <div class="kpi-icon-box">⚡</div>
        <div class="kpi-info">
          <div class="kpi-value">{{ store.summary.recentCount }}</div>
          <div class="kpi-label">{{ t('audit.summary.recentCount') }}</div>
        </div>
      </div>

      <div class="glass-card kpi-card card-total">
        <div class="kpi-icon-box">📊</div>
        <div class="kpi-info">
          <div class="kpi-value">{{ store.summary.totalCount }}</div>
          <div class="kpi-label">{{ t('audit.summary.totalCount') }}</div>
        </div>
      </div>

      <div v-for="([action, count]) in topActions" :key="action" class="glass-card kpi-card card-action">
        <div class="kpi-icon-box">📌</div>
        <div class="kpi-info">
          <div class="kpi-value">{{ count }}</div>
          <div class="kpi-label">{{ action }}</div>
        </div>
      </div>
    </div>

    <!-- Glass Filter Bar -->
    <div class="glass-card filter-panel">
      <div class="filter-grid">
        <input v-model="filters.action" :placeholder="t('audit.action')" class="neu-input" @keyup.enter="search" />
        <input v-model="filters.entityType" :placeholder="t('audit.entityType')" class="neu-input" @keyup.enter="search" />
        <input v-model="filters.userId" :placeholder="t('audit.userId')" class="neu-input" @keyup.enter="search" />
        <input v-model="filters.from" type="date" class="neu-input" :title="t('audit.from')" />
        <input v-model="filters.to" type="date" class="neu-input" :title="t('audit.to')" />
      </div>

      <div class="filter-actions">
        <button class="neu-btn btn-secondary" @click="resetFilters">{{ t('common.reset') }}</button>
        <button class="neu-btn btn-primary" @click="search">{{ t('common.search') }}</button>
      </div>
    </div>

    <!-- Table and Timeline Section -->
    <div class="glass-card table-panel">
      <div v-if="store.error" class="neu-banner banner-error">{{ store.error }}</div>

      <div v-if="store.loading" class="loading-state">
        <span class="spinner">🌀</span>
        <p>{{ t('common.loading') }}</p>
      </div>

      <div v-else class="table-wrapper">
        <table class="glass-table">
          <thead>
            <tr>
              <th>{{ t('audit.time') }}</th>
              <th>{{ t('audit.action') }}</th>
              <th>{{ t('audit.entityType') }}</th>
              <th>{{ t('audit.entityId') }}</th>
              <th>{{ t('audit.userId') }}</th>
              <th>{{ t('audit.details') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="entry in store.entries" :key="entry.id" class="table-row">
              <td :data-label="t('audit.time')" class="time-cell">{{ formatDate(entry.createdAt) }}</td>
              <td :data-label="t('audit.action')">
                <span :class="['badge', actionBadgeClass(entry.action)]">{{ entry.action }}</span>
              </td>
              <td :data-label="t('audit.entityType')" class="entity-type">{{ entry.entityType }}</td>
              <td :data-label="t('audit.entityId')" class="col-id">
                <code>{{ entry.entityId.slice(0, 8) }}</code>
              </td>
              <td :data-label="t('audit.userId')">{{ entry.userId ?? '—' }}</td>
              <td :data-label="t('audit.details')" class="details-cell" :title="entry.details ?? ''">
                {{ entry.details || '—' }}
              </td>
            </tr>

            <tr v-if="store.entries.length === 0">
              <td colspan="6" class="empty-state">
                <span class="empty-icon">📭</span>
                <p>{{ t('common.noData') }}</p>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      <div v-if="store.total > 0" class="pagination-bar">
        <div class="page-info">{{ t('audit.pagination.total', { n: store.total }) }} ({{ page }} / {{ totalPages }})</div>
        <div class="page-controls">
          <button class="neu-page-btn" :disabled="page <= 1" @click="goToPage(page - 1)">{{ t('common.prev') }}</button>
          <button
            v-for="p in visiblePages"
            :key="p"
            :class="['neu-page-btn', { active: p === page }]"
            @click="goToPage(p)"
          >
            {{ p }}
          </button>
          <button class="neu-page-btn" :disabled="page >= totalPages" @click="goToPage(page + 1)">{{ t('common.next') }}</button>
          <NeuSelect
            :model-value="pageSize"
            :options="pageSizeOptions"
            @change="changePageSize"
          />
        </div>
      </div>
    </div>

    <!-- Timeline Activity Feed -->
    <div class="glass-card timeline-card">
      <h2 class="timeline-heading">⏳ {{ t('audit.timeline') }}</h2>
      <div v-if="timelineEntries.length > 0" class="timeline-feed">
        <div v-for="entry in timelineEntries" :key="'tl-' + entry.id" class="timeline-node">
          <div :class="['timeline-dot', actionBadgeClass(entry.action)]"></div>
          <div class="timeline-body">
            <div class="timeline-meta">
              <span class="timeline-timestamp">{{ formatDate(entry.createdAt) }}</span>
              <span :class="['badge', actionBadgeClass(entry.action)]">{{ entry.action }}</span>
              <span class="timeline-target">{{ entry.entityType }} <code>{{ entry.entityId.slice(0, 8) }}</code></span>
            </div>
            <div v-if="entry.details" class="timeline-detail-box">{{ entry.details }}</div>
          </div>
        </div>
      </div>
      <div v-else class="empty-state">{{ t('common.noData') }}</div>
    </div>
  </div>
</template>

<style scoped>
.audit-container {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
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

.glass-card {
  background: var(--bg-surface);
  backdrop-filter: var(--glass-blur);
  border: var(--glass-border);
  border-radius: 20px;
  box-shadow: var(--neu-shadow);
  padding: 24px;
}

/* KPI Grid */
.kpi-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 16px;
}

.kpi-card {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 20px;
}

.kpi-icon-box {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  background: var(--bg-surface-elevated);
  box-shadow: var(--neu-inset);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.3rem;
}

.kpi-value {
  font-size: 1.8rem;
  font-weight: 800;
}

.kpi-label {
  font-size: 0.8rem;
  color: var(--text-muted);
  font-weight: 700;
  text-transform: uppercase;
}

.card-recent .kpi-value { color: var(--primary); }
.card-total .kpi-value { color: #8b5cf6; }
.card-action .kpi-value { color: var(--info); }

/* Filter Panel */
.filter-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.filter-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 12px;
}

.filter-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.neu-input {
  width: 100%;
  padding: 10px 14px;
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  border-radius: 10px;
  box-shadow: var(--neu-inset);
  color: var(--text-primary);
  font-size: 0.9rem;
  outline: none;
}

.neu-input:focus {
  border-color: var(--primary);
  box-shadow: 0 0 0 2px var(--primary-glow), var(--neu-inset);
}

/* Table */
.table-panel {
  padding: 8px;
}

.table-wrapper {
  overflow-x: auto;
}

.glass-table {
  width: 100%;
  border-collapse: separate;
  border-spacing: 0;
}

.glass-table th {
  background: var(--bg-surface-elevated);
  padding: 14px 18px;
  text-align: left;
  font-size: 0.8rem;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--text-muted);
  border-bottom: var(--glass-border-subtle);
}

.glass-table td {
  padding: 14px 18px;
  border-bottom: var(--glass-border-subtle);
  font-size: 0.9rem;
}

.table-row:hover {
  background: var(--bg-surface-elevated);
}

.time-cell {
  color: var(--text-secondary);
  font-size: 0.85rem;
  white-space: nowrap;
}

.col-id code {
  font-family: monospace;
  background: var(--bg-surface-elevated);
  padding: 2px 6px;
  border-radius: 4px;
  border: var(--glass-border-subtle);
}

.details-cell {
  max-width: 280px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-secondary);
}

/* Badges */
.badge {
  padding: 3px 10px;
  border-radius: 999px;
  font-size: 0.75rem;
  font-weight: 700;
}

.badge-create { background: var(--success-bg); color: var(--success); }
.badge-delete { background: var(--danger-bg); color: var(--danger); }
.badge-update { background: var(--primary-glow); color: var(--primary); }
.badge-other { background: rgba(148, 163, 184, 0.15); color: #64748b; }

/* Pagination */
.pagination-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  flex-wrap: wrap;
  gap: 12px;
}

.page-info {
  font-size: 0.85rem;
  color: var(--text-muted);
}

.page-controls {
  display: flex;
  align-items: center;
  gap: 6px;
}

.neu-page-btn {
  padding: 6px 12px;
  border-radius: 8px;
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  box-shadow: var(--neu-shadow-sm);
  color: var(--text-primary);
  font-size: 0.85rem;
  font-weight: 700;
  cursor: pointer;
}

.neu-page-btn.active {
  background: var(--primary-gradient);
  color: white;
}

.neu-select {
  padding: 6px 10px;
  border-radius: 8px;
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  color: var(--text-primary);
  font-size: 0.85rem;
}

/* Timeline */
.timeline-card {
  padding: 28px;
}

.timeline-heading {
  font-size: 1.2rem;
  font-weight: 800;
  margin-bottom: 20px;
}

.timeline-feed {
  position: relative;
  padding-left: 28px;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.timeline-feed::before {
  content: '';
  position: absolute;
  left: 9px;
  top: 6px;
  bottom: 6px;
  width: 2px;
  background: var(--glass-border-subtle);
}

.timeline-node {
  position: relative;
}

.timeline-dot {
  width: 14px;
  height: 14px;
  border-radius: 50%;
  position: absolute;
  left: -25px;
  top: 4px;
  box-shadow: 0 0 8px currentColor;
}

.timeline-body {
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  border-radius: 12px;
  padding: 12px 16px;
  box-shadow: var(--neu-shadow-sm);
}

.timeline-meta {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  font-size: 0.85rem;
}

.timeline-timestamp {
  color: var(--text-muted);
}

.timeline-detail-box {
  margin-top: 8px;
  font-size: 0.85rem;
  color: var(--text-secondary);
  background: var(--bg-surface);
  padding: 8px 12px;
  border-radius: 8px;
}

/* Buttons */
.neu-btn {
  padding: 10px 20px;
  border-radius: 10px;
  font-size: 0.9rem;
  font-weight: 700;
  border: none;
  cursor: pointer;
  box-shadow: var(--neu-shadow-sm);
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.btn-primary { background: var(--primary-gradient); color: white; }
.btn-secondary { background: var(--bg-surface-elevated); border: var(--glass-border); color: var(--text-secondary); }
.btn-export { background: var(--success); color: white; }

.loading-state, .empty-state {
  padding: 40px;
  text-align: center;
  color: var(--text-muted);
}
</style>
