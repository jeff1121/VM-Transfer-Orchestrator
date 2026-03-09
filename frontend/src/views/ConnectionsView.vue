<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useConnectionsStore } from '@/stores/connections'
import { connectionsApi } from '@/api/connections'
import type { CreateConnectionRequest, ConnectionType } from '@/types'

const { t } = useI18n()
const connectionsStore = useConnectionsStore()
const showForm = ref(false)
const validating = ref<string | null>(null)
const deleting = ref<string | null>(null)
const formError = ref<string | null>(null)

const newConn = ref<CreateConnectionRequest>({
  name: '',
  type: 'VSphere',
  endpoint: '',
  secret: '',
})

const connectionTypes: ConnectionType[] = ['VSphere', 'ProxmoxVE']

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
  try {
    await connectionsApi.validate(id)
    await connectionsStore.fetchConnections()
  } catch {
    // validation failed
  } finally {
    validating.value = null
  }
}

const handleDelete = async (id: string) => {
  deleting.value = id
  try {
    await connectionsStore.deleteConnection(id)
  } finally {
    deleting.value = null
  }
}

const formatDate = (iso: string | undefined) => iso ? new Date(iso).toLocaleString('zh-TW') : t('connections.notValidated')

onMounted(() => connectionsStore.fetchConnections())
</script>

<template>
  <div class="connections-page">
    <div class="header">
      <h1>{{ t('connections.title') }}</h1>
      <button class="btn btn-primary" @click="showForm = !showForm">
        {{ showForm ? t('common.cancel') : t('connections.addConnection') }}
      </button>
    </div>

    <div v-if="showForm" class="form-panel">
      <h2>{{ t('connections.addConnection') }}</h2>
      <label class="form-label">{{ t('connections.name') }}
        <input v-model="newConn.name" class="input" placeholder="My vSphere" />
      </label>
      <label class="form-label">{{ t('connections.type') }}
        <select v-model="newConn.type" class="input">
          <option v-for="t in connectionTypes" :key="t" :value="t">{{ t }}</option>
        </select>
      </label>
      <label class="form-label">{{ t('connections.host') }}
        <input v-model="newConn.endpoint" class="input" placeholder="https://vcenter.example.com" />
      </label>
      <label class="form-label">{{ t('webhooks.secret') }}
        <input v-model="newConn.secret" type="password" class="input" placeholder="API token / password" />
      </label>
      <div v-if="formError" class="error">{{ formError }}</div>
      <button class="btn btn-primary" :disabled="!newConn.name || !newConn.endpoint || !newConn.secret" @click="handleCreate">{{ t('common.create') }}</button>
    </div>

    <div v-if="connectionsStore.loading" class="loading">{{ t('common.loading') }}</div>
    <table v-else class="conn-table">
      <thead>
        <tr>
          <th>{{ t('connections.name') }}</th>
          <th>{{ t('connections.type') }}</th>
          <th>{{ t('connections.host') }}</th>
          <th>{{ t('connections.validated') }}</th>
          <th>{{ t('common.actions') }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="c in connectionsStore.connections" :key="c.id">
          <td :data-label="t('connections.name')">{{ c.name }}</td>
          <td :data-label="t('connections.type')"><span class="type-badge">{{ c.type }}</span></td>
          <td :data-label="t('connections.host')" class="mono">{{ c.endpoint }}</td>
          <td :data-label="t('connections.validated')">{{ formatDate(c.validatedAt) }}</td>
          <td :data-label="t('common.actions')" class="actions-cell">
            <button class="btn-sm btn-secondary" :disabled="validating === c.id" @click="handleValidate(c.id)">
              {{ validating === c.id ? t('common.loading') : t('connections.testConnection') }}
            </button>
            <button class="btn-sm btn-danger" :disabled="deleting === c.id" @click="handleDelete(c.id)">
              {{ deleting === c.id ? t('common.loading') : t('common.delete') }}
            </button>
          </td>
        </tr>
        <tr v-if="connectionsStore.connections.length === 0">
          <td colspan="5" class="empty">{{ t('common.noData') }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.connections-page { max-width: 900px; }
.header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
h2 { margin-bottom: 16px; }
.form-panel { background: var(--bg-elevated); border-radius: 8px; padding: 24px; box-shadow: 0 1px 3px rgba(0,0,0,.1); margin-bottom: 20px; }
.form-label { display: block; margin-bottom: 12px; font-weight: 500; }
.input { display: block; width: 100%; padding: 8px 12px; border: 1px solid #d1d5db; border-radius: 6px; margin-top: 4px; font-size: 0.95rem; }
.conn-table { width: 100%; border-collapse: collapse; background: var(--bg-elevated); border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,.1); }
.conn-table th { background: var(--bg-primary); text-align: left; padding: 12px; font-weight: 600; border-bottom: 1px solid var(--border-color); }
.conn-table td { padding: 12px; border-bottom: 1px solid var(--border-color); }
.mono { font-family: monospace; font-size: 0.85rem; }
.type-badge { background: #e0e7ff; color: #3730a3; padding: 2px 8px; border-radius: 4px; font-size: 0.8rem; }
.actions-cell { display: flex; gap: 6px; }
.btn { padding: 8px 16px; border: none; border-radius: 6px; cursor: pointer; font-weight: 500; }
.btn-primary { background: #3b82f6; color: white; }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
.btn-sm { padding: 4px 10px; border: none; border-radius: 4px; cursor: pointer; font-size: 0.8rem; }
.btn-secondary { background: var(--border-color); color: #374151; }
.btn-danger { background: #fef2f2; color: #b91c1c; }
.btn-sm:disabled { opacity: 0.5; cursor: not-allowed; }
.error { background: #fef2f2; color: #b91c1c; padding: 12px; border-radius: 6px; margin-bottom: 12px; }
.loading { color: var(--text-secondary); padding: 20px; }
.empty { text-align: center; color: var(--text-secondary); padding: 24px; }
@media (max-width: 768px) {
  .header { flex-direction: column; align-items: stretch; gap: 12px; }
  .conn-table, .conn-table tbody, .conn-table tr, .conn-table td { display: block; width: 100%; }
  .conn-table thead { display: none; }
  .conn-table tr { padding: 12px; border-bottom: 1px solid var(--border-color); }
  .conn-table td { border: 0; padding: 6px 0; display: flex; justify-content: space-between; gap: 12px; }
  .conn-table td::before { content: attr(data-label); color: var(--text-secondary); font-weight: 600; }
  .actions-cell { justify-content: flex-end; flex-wrap: wrap; }
}
</style>
