<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuditStore } from '@/stores/audit'

const { t } = useI18n()
const store = useAuditStore()

const filters = reactive({
  action: '',
  entityType: '',
  userId: '',
  from: '',
  to: '',
})

const page = ref(1)
const pageSize = ref(20)

const formatDate = (iso: string) => new Date(iso).toLocaleString('zh-TW')

// 摘要卡片：常見操作 top 3
const topActions = computed(() => {
  if (!store.summary) return []
  return Object.entries(store.summary.actionCounts)
    .sort(([, a], [, b]) => b - a)
    .slice(0, 3)
})

const totalPages = computed(() => Math.max(1, Math.ceil(store.total / pageSize.value)))

// 分頁頁碼
const visiblePages = computed(() => {
  const pages: number[] = []
  const start = Math.max(1, page.value - 2)
  const end = Math.min(totalPages.value, page.value + 2)
  for (let i = start; i <= end; i++) pages.push(i)
  return pages
})

// 時間軸：最近 20 筆
const timelineEntries = computed(() => store.entries.slice(0, 20))

// 動作對應 badge 樣式
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

const changePageSize = (e: Event) => {
  pageSize.value = Number((e.target as HTMLSelectElement).value)
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
  <div class="audit">
    <h1>📋 {{ t('audit.title') }}</h1>

    <!-- 統計摘要卡片 -->
    <div class="summary-cards" v-if="store.summary">
      <div class="card card-recent">
        <div class="card-count">{{ store.summary.recentCount }}</div>
        <div class="card-label">{{ t('audit.summary.recentCount') }}</div>
      </div>
      <div class="card card-total">
        <div class="card-count">{{ store.summary.totalCount }}</div>
        <div class="card-label">{{ t('audit.summary.totalCount') }}</div>
      </div>
      <div v-for="([action, count]) in topActions" :key="action" class="card card-action">
        <div class="card-count">{{ count }}</div>
        <div class="card-label">{{ action }}</div>
      </div>
    </div>

    <!-- 篩選列 -->
    <div class="filter-bar">
      <input v-model="filters.action" :placeholder="t('audit.action')" class="filter-input" @keyup.enter="search" />
      <input v-model="filters.entityType" :placeholder="t('audit.entityType')" class="filter-input" @keyup.enter="search" />
      <input v-model="filters.userId" :placeholder="t('audit.userId')" class="filter-input" @keyup.enter="search" />
      <input v-model="filters.from" type="date" class="filter-input" :title="t('audit.from')" />
      <input v-model="filters.to" type="date" class="filter-input" :title="t('audit.to')" />
      <button class="btn btn-primary" @click="search">{{ t('common.search') }}</button>
      <button class="btn btn-secondary" @click="resetFilters">{{ t('common.reset') }}</button>
      <button class="btn btn-export" @click="exportCsv">📥 {{ t('audit.exportCsv') }}</button>
    </div>

    <div v-if="store.error" class="error">{{ store.error }}</div>
    <div v-if="store.loading" class="loading">{{ t('common.loading') }}</div>

    <!-- 資料表格 -->
    <table v-if="!store.loading" class="audit-table">
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
        <tr v-for="entry in store.entries" :key="entry.id">
          <td class="col-time">{{ formatDate(entry.createdAt) }}</td>
          <td><span :class="['badge', actionBadgeClass(entry.action)]">{{ entry.action }}</span></td>
          <td>{{ entry.entityType }}</td>
          <td class="col-id" :title="entry.entityId">{{ entry.entityId.slice(0, 8) }}…</td>
          <td>{{ entry.userId ?? '—' }}</td>
          <td class="col-details" :title="entry.details ?? ''">{{ entry.details ? (entry.details.length > 60 ? entry.details.slice(0, 60) + '…' : entry.details) : '—' }}</td>
        </tr>
        <tr v-if="store.entries.length === 0">
          <td colspan="6" class="empty">{{ t('common.noData') }}</td>
        </tr>
      </tbody>
    </table>

    <!-- 分頁 -->
    <div v-if="store.total > 0" class="pagination">
      <div class="page-info">{{ t('audit.pagination.total', { n: store.total }) }}，{{ page }} / {{ totalPages }}</div>
      <div class="page-controls">
        <button class="btn btn-page" :disabled="page <= 1" @click="goToPage(page - 1)">{{ t('common.prev') }}</button>
        <button v-for="p in visiblePages" :key="p" :class="['btn', 'btn-page', { active: p === page }]" @click="goToPage(p)">{{ p }}</button>
        <button class="btn btn-page" :disabled="page >= totalPages" @click="goToPage(page + 1)">{{ t('common.next') }}</button>
        <select class="page-size-select" :value="pageSize" @change="changePageSize">
          <option :value="10">10 {{ t('audit.pagination.items') }}/{{ t('audit.pagination.pageSize') }}</option>
          <option :value="20">20 {{ t('audit.pagination.items') }}/{{ t('audit.pagination.pageSize') }}</option>
          <option :value="50">50 {{ t('audit.pagination.items') }}/{{ t('audit.pagination.pageSize') }}</option>
          <option :value="100">100 {{ t('audit.pagination.items') }}/{{ t('audit.pagination.pageSize') }}</option>
        </select>
      </div>
    </div>

    <!-- 時間軸 -->
    <h2>{{ t('audit.timeline') }}</h2>
    <div class="timeline" v-if="timelineEntries.length > 0">
      <div v-for="entry in timelineEntries" :key="'tl-' + entry.id" class="timeline-item">
        <div class="timeline-dot" :class="actionBadgeClass(entry.action)"></div>
        <div class="timeline-content">
          <span class="timeline-time">{{ formatDate(entry.createdAt) }}</span>
          <span :class="['badge', actionBadgeClass(entry.action)]">{{ entry.action }}</span>
          <span class="timeline-entity">{{ entry.entityType }} <code>{{ entry.entityId.slice(0, 8) }}</code></span>
          <span v-if="entry.userId" class="timeline-user">by {{ entry.userId }}</span>
        </div>
      </div>
    </div>
    <div v-else class="empty">{{ t('common.noData') }}</div>
  </div>
</template>

<style scoped>
.audit { max-width: 1200px; }
h1 { margin-bottom: 16px; }
h2 { margin: 32px 0 12px; }

/* 摘要卡片 */
.summary-cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 12px; margin-bottom: 20px; }
.card { background: white; border-radius: 8px; padding: 16px; text-align: center; box-shadow: 0 1px 3px rgba(0,0,0,.1); }
.card-count { font-size: 1.8rem; font-weight: 700; }
.card-label { color: #666; margin-top: 4px; font-size: 0.85rem; }
.card-recent .card-count { color: #3b82f6; }
.card-total .card-count { color: #6366f1; }
.card-action .card-count { color: #0891b2; }

/* 篩選列 */
.filter-bar { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 16px; align-items: center; }
.filter-input { padding: 6px 10px; border: 1px solid #d1d5db; border-radius: 6px; font-size: 0.9rem; }
.filter-input:focus { outline: none; border-color: #3b82f6; box-shadow: 0 0 0 2px rgba(59,130,246,.2); }
.btn { padding: 6px 14px; border: none; border-radius: 6px; cursor: pointer; font-size: 0.9rem; transition: background 0.15s; }
.btn-primary { background: #3b82f6; color: white; }
.btn-primary:hover { background: #2563eb; }
.btn-secondary { background: #e5e7eb; color: #374151; }
.btn-secondary:hover { background: #d1d5db; }
.btn-export { background: #059669; color: white; }
.btn-export:hover { background: #047857; }

/* 表格 */
.audit-table { width: 100%; border-collapse: collapse; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,.1); }
.audit-table th { background: #f9fafb; text-align: left; padding: 10px 12px; font-weight: 600; border-bottom: 1px solid #e5e7eb; font-size: 0.9rem; }
.audit-table td { padding: 10px 12px; border-bottom: 1px solid #f3f4f6; font-size: 0.9rem; }
.audit-table tbody tr:hover { background: #f9fafb; }
.col-time { white-space: nowrap; font-size: 0.85rem; color: #555; }
.col-id { font-family: monospace; font-size: 0.85rem; }
.col-details { max-width: 200px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

/* Badge */
.badge { padding: 2px 8px; border-radius: 4px; font-size: 0.8rem; font-weight: 500; white-space: nowrap; }
.badge-create { background: #dcfce7; color: #166534; }
.badge-delete { background: #fef2f2; color: #b91c1c; }
.badge-update { background: #dbeafe; color: #1d4ed8; }
.badge-other { background: #f3f4f6; color: #374151; }

/* 分頁 */
.pagination { display: flex; justify-content: space-between; align-items: center; margin-top: 16px; flex-wrap: wrap; gap: 8px; }
.page-info { font-size: 0.85rem; color: #666; }
.page-controls { display: flex; gap: 4px; align-items: center; }
.btn-page { background: #f3f4f6; color: #374151; padding: 4px 10px; font-size: 0.85rem; }
.btn-page:hover:not(:disabled) { background: #e5e7eb; }
.btn-page:disabled { opacity: 0.4; cursor: not-allowed; }
.btn-page.active { background: #3b82f6; color: white; }
.page-size-select { padding: 4px 8px; border: 1px solid #d1d5db; border-radius: 6px; font-size: 0.85rem; margin-left: 8px; }

/* 時間軸 */
.timeline { position: relative; padding-left: 24px; }
.timeline::before { content: ''; position: absolute; left: 7px; top: 0; bottom: 0; width: 2px; background: #e5e7eb; }
.timeline-item { position: relative; padding: 8px 0; display: flex; align-items: flex-start; gap: 10px; }
.timeline-dot { width: 12px; height: 12px; border-radius: 50%; position: absolute; left: -21px; top: 12px; border: 2px solid white; }
.timeline-dot.badge-create { background: #22c55e; }
.timeline-dot.badge-delete { background: #ef4444; }
.timeline-dot.badge-update { background: #3b82f6; }
.timeline-dot.badge-other { background: #9ca3af; }
.timeline-content { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; font-size: 0.85rem; }
.timeline-time { color: #666; min-width: 140px; }
.timeline-entity { color: #374151; }
.timeline-entity code { font-family: monospace; font-size: 0.8rem; background: #f3f4f6; padding: 1px 4px; border-radius: 3px; }
.timeline-user { color: #999; font-size: 0.8rem; }

.error { background: #fef2f2; color: #b91c1c; padding: 12px; border-radius: 6px; margin-bottom: 16px; }
.loading { color: #666; padding: 20px; }
.empty { text-align: center; color: #999; padding: 24px; }
</style>
