<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useConnectionsStore } from '@/stores/connections'
import { connectionsApi } from '@/api/connections'
import type { CreateConnectionRequest, ConnectionType } from '@/types'

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
    formError.value = e instanceof Error ? e.message : '建立連線失敗'
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

const formatDate = (iso: string | undefined) => iso ? new Date(iso).toLocaleString('zh-TW') : '未驗證'

onMounted(() => connectionsStore.fetchConnections())
</script>

<template>
  <div class="connections-page">
    <div class="header">
      <h1>連線管理</h1>
      <button class="btn btn-primary" @click="showForm = !showForm">
        {{ showForm ? '取消' : '新增連線' }}
      </button>
    </div>

    <div v-if="showForm" class="form-panel">
      <h2>新增連線</h2>
      <label class="form-label">名稱
        <input v-model="newConn.name" class="input" placeholder="My vSphere" />
      </label>
      <label class="form-label">類型
        <select v-model="newConn.type" class="input">
          <option v-for="t in connectionTypes" :key="t" :value="t">{{ t }}</option>
        </select>
      </label>
      <label class="form-label">端點
        <input v-model="newConn.endpoint" class="input" placeholder="https://vcenter.example.com" />
      </label>
      <label class="form-label">密鑰
        <input v-model="newConn.secret" type="password" class="input" placeholder="API token / password" />
      </label>
      <div v-if="formError" class="error">{{ formError }}</div>
      <button class="btn btn-primary" :disabled="!newConn.name || !newConn.endpoint || !newConn.secret" @click="handleCreate">建立</button>
    </div>

    <div v-if="connectionsStore.loading" class="loading">載入中…</div>
    <table v-else class="conn-table">
      <thead>
        <tr>
          <th>名稱</th>
          <th>類型</th>
          <th>端點</th>
          <th>驗證時間</th>
          <th>操作</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="c in connectionsStore.connections" :key="c.id">
          <td>{{ c.name }}</td>
          <td><span class="type-badge">{{ c.type }}</span></td>
          <td class="mono">{{ c.endpoint }}</td>
          <td>{{ formatDate(c.validatedAt) }}</td>
          <td class="actions-cell">
            <button class="btn-sm btn-secondary" :disabled="validating === c.id" @click="handleValidate(c.id)">
              {{ validating === c.id ? '驗證中…' : '驗證' }}
            </button>
            <button class="btn-sm btn-danger" :disabled="deleting === c.id" @click="handleDelete(c.id)">
              {{ deleting === c.id ? '刪除中…' : '刪除' }}
            </button>
          </td>
        </tr>
        <tr v-if="connectionsStore.connections.length === 0">
          <td colspan="5" class="empty">尚無連線</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.connections-page { max-width: 900px; }
.header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
h2 { margin-bottom: 16px; }
.form-panel { background: white; border-radius: 8px; padding: 24px; box-shadow: 0 1px 3px rgba(0,0,0,.1); margin-bottom: 20px; }
.form-label { display: block; margin-bottom: 12px; font-weight: 500; }
.input { display: block; width: 100%; padding: 8px 12px; border: 1px solid #d1d5db; border-radius: 6px; margin-top: 4px; font-size: 0.95rem; }
.conn-table { width: 100%; border-collapse: collapse; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,.1); }
.conn-table th { background: #f9fafb; text-align: left; padding: 12px; font-weight: 600; border-bottom: 1px solid #e5e7eb; }
.conn-table td { padding: 12px; border-bottom: 1px solid #f3f4f6; }
.mono { font-family: monospace; font-size: 0.85rem; }
.type-badge { background: #e0e7ff; color: #3730a3; padding: 2px 8px; border-radius: 4px; font-size: 0.8rem; }
.actions-cell { display: flex; gap: 6px; }
.btn { padding: 8px 16px; border: none; border-radius: 6px; cursor: pointer; font-weight: 500; }
.btn-primary { background: #3b82f6; color: white; }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
.btn-sm { padding: 4px 10px; border: none; border-radius: 4px; cursor: pointer; font-size: 0.8rem; }
.btn-secondary { background: #e5e7eb; color: #374151; }
.btn-danger { background: #fef2f2; color: #b91c1c; }
.btn-sm:disabled { opacity: 0.5; cursor: not-allowed; }
.error { background: #fef2f2; color: #b91c1c; padding: 12px; border-radius: 6px; margin-bottom: 12px; }
.loading { color: #666; padding: 20px; }
.empty { text-align: center; color: #999; padding: 24px; }
</style>
