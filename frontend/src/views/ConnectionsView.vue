<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useConnectionsStore } from '@/stores/connections'
import { connectionsApi } from '@/api/connections'
import type { CreateConnectionRequest, PlatformKind } from '@/types'
import NeuSelect, { type SelectOption } from '@/components/common/NeuSelect.vue'

const { t } = useI18n()
const connectionsStore = useConnectionsStore()
const showForm = ref(false)
const validating = ref<string | null>(null)
const deleting = ref<string | null>(null)
const formError = ref<string | null>(null)
const validateError = ref<string | null>(null)
const deleteError = ref<string | null>(null)

const newConn = ref<CreateConnectionRequest>({
  name: '',
  type: 'VSphere',
  endpoint: '',
  secret: '',
})

const connectionOptions = computed<SelectOption[]>(() => [
  { value: 'VSphere', label: t('connections.types.VSphere'), icon: '🌐' },
  { value: 'ProxmoxVE', label: t('connections.types.ProxmoxVE'), icon: '⚡' },
  { value: 'HyperV', label: t('connections.types.HyperV'), icon: '🪟' },
])
const endpointHint = computed(() => t(`connections.endpointHint.${newConn.value.type}`))

const resetForm = () => {
  newConn.value = { name: '', type: 'VSphere', endpoint: '', secret: '' }
  formError.value = null
}

const handleCreate = async () => {
  formError.value = null
  try {
    await connectionsStore.createConnection(newConn.value)
    showForm.value = false
    resetForm()
  } catch (e) {
    formError.value = e instanceof Error ? e.message : t('common.noData')
  }
}

const handleValidate = async (id: string) => {
  validating.value = id
  validateError.value = null
  try {
    await connectionsApi.validate(id)
    await connectionsStore.fetchConnections()
  } catch (e) {
    validateError.value = e instanceof Error ? e.message : t('connections.validateFailed')
  } finally {
    validating.value = null
  }
}

const handleDelete = async (id: string) => {
  deleting.value = id
  deleteError.value = null
  try {
    await connectionsStore.deleteConnection(id)
  } catch (e) {
    deleteError.value = e instanceof Error ? e.message : t('connections.deleteFailed')
  } finally {
    deleting.value = null
  }
}

const formatDate = (iso: string | undefined) =>
  iso ? new Date(iso).toLocaleString('zh-TW', { hour12: false }) : t('connections.notValidated')

const platformBadge = (type: PlatformKind) => {
  switch (type) {
    case 'VSphere': return { class: 'type-vsphere', icon: '🌐' }
    case 'HyperV': return { class: 'type-hyperv', icon: '🪟' }
    case 'ProxmoxVE': return { class: 'type-pve', icon: '⚡' }
    default: return { class: 'type-default', icon: '🔌' }
  }
}

onMounted(() => connectionsStore.fetchConnections())
</script>

<template>
  <div class="connections-container">
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ t('connections.title') }}</h1>
        <p class="page-subtitle">Configure hypervisor endpoints and credential management for source and target platforms.</p>
      </div>

      <button class="neu-btn btn-primary" @click="showForm = !showForm">
        <span>{{ showForm ? t('common.cancel') : `➕ ${t('connections.addConnection')}` }}</span>
      </button>
    </div>

    <!-- Create Connection Modal/Panel -->
    <transition name="expand">
      <div v-if="showForm" class="glass-card form-panel">
        <h2 class="form-title">⚡ {{ t('connections.addConnection') }}</h2>

        <div class="form-grid">
          <div class="form-group">
            <label class="form-label">{{ t('connections.name') }}</label>
            <input v-model="newConn.name" class="neu-input" placeholder="e.g. vCenter Production DC-1" />
          </div>

          <div class="form-group">
            <label class="form-label">{{ t('connections.type') }}</label>
            <NeuSelect
              v-model="newConn.type"
              :options="connectionOptions"
              full-width
            />
          </div>

          <div class="form-group full-width">
            <label class="form-label">{{ t('connections.host') }}</label>
            <input v-model="newConn.endpoint" class="neu-input" :placeholder="endpointHint" />
            <span class="hint-text">{{ endpointHint }}</span>
          </div>

          <div class="form-group full-width">
            <label class="form-label">{{ t('connections.secret') }}</label>
            <input
              v-model="newConn.secret"
              type="password"
              class="neu-input"
              placeholder="API Token / Secret Key / Password"
            />
          </div>
        </div>

        <div v-if="formError" class="neu-banner banner-error">{{ formError }}</div>

        <div class="form-actions">
          <button class="neu-btn btn-secondary" @click="showForm = false; resetForm()">
            {{ t('common.cancel') }}
          </button>
          <button
            class="neu-btn btn-primary"
            :disabled="!newConn.name || !newConn.endpoint || !newConn.secret"
            @click="handleCreate"
          >
            {{ t('common.create') }}
          </button>
        </div>
      </div>
    </transition>

    <!-- Error Banners -->
    <div v-if="connectionsStore.error" class="neu-banner banner-error">{{ connectionsStore.error }}</div>
    <div v-if="validateError" class="neu-banner banner-error">{{ validateError }}</div>
    <div v-if="deleteError" class="neu-banner banner-error">{{ deleteError }}</div>

    <!-- Table Container -->
    <div class="glass-card table-card">
      <div v-if="connectionsStore.loading" class="loading-state">
        <span class="spinner">🌀</span>
        <p>{{ t('common.loading') }}</p>
      </div>

      <div v-else class="table-wrapper">
        <table class="glass-table">
          <thead>
            <tr>
              <th>{{ t('connections.name') }}</th>
              <th>{{ t('connections.type') }}</th>
              <th>{{ t('connections.host') }}</th>
              <th>{{ t('connections.validated') }}</th>
              <th class="text-right">{{ t('common.actions') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="c in connectionsStore.connections" :key="c.id" class="table-row">
              <td :data-label="t('connections.name')" class="name-cell">
                <span class="conn-name">{{ c.name }}</span>
              </td>
              <td :data-label="t('connections.type')">
                <span :class="['type-badge', platformBadge(c.type).class]">
                  <span>{{ platformBadge(c.type).icon }}</span>
                  <span>{{ t(`connections.types.${c.type}`) }}</span>
                </span>
              </td>
              <td :data-label="t('connections.host')" class="endpoint-cell">
                <code>{{ c.endpoint }}</code>
              </td>
              <td :data-label="t('connections.validated')" class="time-cell">
                <span :class="c.validatedAt ? 'status-validated' : 'status-unvalidated'">
                  {{ c.validatedAt ? '✅ ' : '⚠️ ' }}{{ formatDate(c.validatedAt) }}
                </span>
              </td>
              <td :data-label="t('common.actions')" class="actions-cell">
                <button
                  class="action-btn validate-btn"
                  :disabled="validating === c.id"
                  @click="handleValidate(c.id)"
                >
                  <span>{{ validating === c.id ? '⏳' : '⚡' }}</span>
                  <span>{{ validating === c.id ? t('common.loading') : t('connections.testConnection') }}</span>
                </button>
                <button
                  class="action-btn delete-btn"
                  :disabled="deleting === c.id"
                  @click="handleDelete(c.id)"
                >
                  <span>🗑️</span>
                  <span>{{ deleting === c.id ? t('common.loading') : t('common.delete') }}</span>
                </button>
              </td>
            </tr>

            <tr v-if="connectionsStore.connections.length === 0">
              <td colspan="5" class="empty-state">
                <span class="empty-icon">🔌</span>
                <p>{{ t('common.noData') }}</p>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<style scoped>
.connections-container {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
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

/* Glass Card */
.glass-card {
  background: var(--bg-surface);
  backdrop-filter: var(--glass-blur);
  border: var(--glass-border);
  border-radius: 20px;
  box-shadow: var(--neu-shadow);
  overflow: hidden;
}

/* Form Panel */
.form-panel {
  padding: 28px;
}

.form-title {
  font-size: 1.2rem;
  font-weight: 800;
  margin-bottom: 20px;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 18px;
}

.full-width {
  grid-column: span 2;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-label {
  font-size: 0.85rem;
  font-weight: 700;
  color: var(--text-secondary);
}

.neu-input {
  width: 100%;
  padding: 12px 16px;
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  border-radius: 12px;
  box-shadow: var(--neu-inset);
  color: var(--text-primary);
  font-size: 0.95rem;
  transition: all 0.2s ease;
  outline: none;
}

.neu-input:focus {
  border-color: var(--primary);
  box-shadow: 0 0 0 3px var(--primary-glow), var(--neu-inset);
}

.hint-text {
  font-size: 0.8rem;
  color: var(--text-muted);
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  margin-top: 24px;
}

/* Table Card */
.table-card {
  padding: 8px;
}

.table-wrapper {
  overflow-x: auto;
  border-radius: 14px;
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

.glass-table th.text-right {
  text-align: right;
}

.glass-table td {
  padding: 16px 20px;
  border-bottom: var(--glass-border-subtle);
  font-size: 0.95rem;
}

.table-row {
  transition: background 0.2s ease;
}

.table-row:hover {
  background: var(--bg-surface-elevated);
}

.name-cell .conn-name {
  font-weight: 700;
  color: var(--text-primary);
}

.type-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 12px;
  border-radius: 999px;
  font-size: 0.8rem;
  font-weight: 700;
}

.type-vsphere { background: rgba(59, 130, 246, 0.15); color: #3b82f6; }
.type-hyperv { background: rgba(14, 165, 233, 0.15); color: #0ea5e9; }
.type-pve { background: rgba(249, 115, 22, 0.15); color: #f97316; }
.type-default { background: rgba(148, 163, 184, 0.15); color: #64748b; }

.endpoint-cell code {
  font-family: monospace;
  font-size: 0.85rem;
  background: var(--bg-surface-elevated);
  padding: 4px 8px;
  border-radius: 6px;
  border: var(--glass-border-subtle);
}

.status-validated {
  color: var(--success);
  font-weight: 600;
  font-size: 0.85rem;
}

.status-unvalidated {
  color: var(--warning);
  font-weight: 600;
  font-size: 0.85rem;
}

.actions-cell {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.action-btn {
  padding: 8px 14px;
  border-radius: 10px;
  font-size: 0.85rem;
  font-weight: 700;
  border: var(--glass-border-subtle);
  background: var(--bg-surface-elevated);
  box-shadow: var(--neu-shadow-sm);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  transition: all 0.2s ease;
}

.validate-btn {
  color: var(--primary);
}

.validate-btn:hover:not(:disabled) {
  background: var(--primary-glow);
  transform: translateY(-2px);
}

.delete-btn {
  color: var(--danger);
}

.delete-btn:hover:not(:disabled) {
  background: var(--danger-bg);
  transform: translateY(-2px);
}

.action-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* Buttons */
.neu-btn {
  padding: 12px 24px;
  border-radius: 12px;
  font-size: 0.9rem;
  font-weight: 700;
  border: none;
  cursor: pointer;
  box-shadow: 0 4px 14px var(--primary-glow);
  transition: all 0.25s ease;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
}

.btn-primary { background: var(--primary-gradient); color: white; }
.btn-secondary {
  background: var(--bg-surface-elevated);
  border: var(--glass-border);
  color: var(--text-secondary);
  box-shadow: var(--neu-shadow-sm);
}

.btn-secondary:hover {
  background: var(--bg-surface);
  color: var(--text-primary);
}

.loading-state, .empty-state {
  padding: 60px 20px;
  text-align: center;
  color: var(--text-muted);
}

.spinner, .empty-icon {
  font-size: 2.5rem;
  display: block;
  margin-bottom: 8px;
}

@media (max-width: 768px) {
  .page-header { flex-direction: column; align-items: stretch; }
  .form-grid { grid-template-columns: 1fr; }
  .full-width { grid-column: span 1; }
  .glass-table, .glass-table tbody, .glass-table tr, .glass-table td { display: block; width: 100%; }
  .glass-table thead { display: none; }
  .table-row { padding: 16px; border-bottom: var(--glass-border); }
  .glass-table td { border: none; padding: 6px 0; display: flex; justify-content: space-between; align-items: center; }
  .glass-table td::before { content: attr(data-label); font-weight: 700; color: var(--text-muted); font-size: 0.85rem; }
  .actions-cell { justify-content: flex-end; margin-top: 8px; }
}
</style>
