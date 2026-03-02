<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { webhooksApi } from '@/api/webhooks'
import type { WebhookSubscription, CreateWebhookRequest, UpdateWebhookRequest } from '@/api/webhooks'

const { t } = useI18n()
const webhooks = ref<WebhookSubscription[]>([])
const loading = ref(false)
const showForm = ref(false)
const editingId = ref<string | null>(null)
const formError = ref<string | null>(null)
const testingId = ref<string | null>(null)
const deletingId = ref<string | null>(null)
const testMessage = ref<string | null>(null)

const webhookTypes = ['Slack', 'Teams', 'Email', 'Http'] as const
const eventOptions = ['JobSucceeded', 'JobFailed', 'StepFailed'] as const

const form = ref({
  name: '',
  type: 'Http' as string,
  target: '',
  events: [] as string[],
  isEnabled: true,
  customHeaders: '',
  secret: '',
})

const resetForm = () => {
  form.value = { name: '', type: 'Http', target: '', events: [], isEnabled: true, customHeaders: '', secret: '' }
  editingId.value = null
  formError.value = null
}

const fetchWebhooks = async () => {
  loading.value = true
  try {
    const res = await webhooksApi.list()
    webhooks.value = res.data
  } catch {
    // 載入失敗
  } finally {
    loading.value = false
  }
}

const openCreate = () => {
  resetForm()
  showForm.value = true
}

const openEdit = (w: WebhookSubscription) => {
  editingId.value = w.id
  form.value = {
    name: w.name,
    type: w.type,
    target: w.target,
    events: w.events.split(',').map(e => e.trim()).filter(Boolean),
    isEnabled: w.isEnabled,
    customHeaders: '',
    secret: '',
  }
  showForm.value = true
}

const handleSubmit = async () => {
  formError.value = null
  const eventsStr = form.value.events.join(',')

  try {
    if (editingId.value) {
      const data: UpdateWebhookRequest = {
        name: form.value.name,
        target: form.value.target,
        events: eventsStr,
        isEnabled: form.value.isEnabled,
        customHeaders: form.value.customHeaders || undefined,
        secret: form.value.secret || undefined,
      }
      await webhooksApi.update(editingId.value, data)
    } else {
      const data: CreateWebhookRequest = {
        name: form.value.name,
        type: form.value.type,
        target: form.value.target,
        events: eventsStr,
        customHeaders: form.value.customHeaders || undefined,
        secret: form.value.secret || undefined,
      }
      await webhooksApi.create(data)
    }
    showForm.value = false
    resetForm()
    await fetchWebhooks()
  } catch (e) {
    formError.value = e instanceof Error ? e.message : t('common.noData')
  }
}

const handleToggle = async (w: WebhookSubscription) => {
  try {
    await webhooksApi.update(w.id, {
      name: w.name,
      target: w.target,
      events: w.events,
      isEnabled: !w.isEnabled,
    })
    await fetchWebhooks()
  } catch {
    // 切換失敗
  }
}

const handleDelete = async (id: string) => {
  if (!confirm(t('webhooks.deleteConfirm'))) return
  deletingId.value = id
  try {
    await webhooksApi.delete(id)
    await fetchWebhooks()
  } finally {
    deletingId.value = null
  }
}

const handleTest = async (id: string) => {
  testingId.value = id
  testMessage.value = null
  try {
    await webhooksApi.test(id)
    testMessage.value = t('webhooks.testSent')
    setTimeout(() => { testMessage.value = null }, 3000)
  } catch {
    testMessage.value = t('webhooks.testFailed')
    setTimeout(() => { testMessage.value = null }, 3000)
  } finally {
    testingId.value = null
  }
}

const typeLabel = (tp: string) => {
  const key = `webhooks.types.${tp}` as const
  return t(key)
}

onMounted(fetchWebhooks)
</script>

<template>
  <div class="webhooks-page">
    <div class="header">
      <h1>🔔 {{ t('webhooks.title') }}</h1>
      <button class="btn btn-primary" @click="showForm ? (showForm = false, resetForm()) : openCreate()">
        {{ showForm ? t('common.cancel') : t('webhooks.addWebhook') }}
      </button>
    </div>

    <div v-if="testMessage" class="toast">{{ testMessage }}</div>

    <div v-if="showForm" class="form-panel">
      <h2>{{ editingId ? t('webhooks.editWebhook') : t('webhooks.addWebhook') }}</h2>

      <label class="form-label">{{ t('webhooks.name') }}
        <input v-model="form.name" class="input" placeholder="例：Slack 通知" />
      </label>

      <label v-if="!editingId" class="form-label">{{ t('webhooks.type') }}
        <select v-model="form.type" class="input">
          <option v-for="tp in webhookTypes" :key="tp" :value="tp">{{ typeLabel(tp) }}</option>
        </select>
      </label>

      <label class="form-label">{{ t('webhooks.target') }}
        <input v-model="form.target" class="input" placeholder="https://hooks.slack.com/services/..." />
      </label>

      <fieldset class="form-fieldset">
        <legend>{{ t('webhooks.events') }}</legend>
        <label v-for="evt in eventOptions" :key="evt" class="checkbox-label">
          <input type="checkbox" :value="evt" v-model="form.events" />
          {{ evt }}
        </label>
      </fieldset>

      <label v-if="editingId" class="form-label toggle-label">
        <input type="checkbox" v-model="form.isEnabled" />
        {{ t('common.enabled') }}
      </label>

      <label v-if="form.type === 'Http'" class="form-label">{{ t('webhooks.customHeaders') }}（JSON）
        <input v-model="form.customHeaders" class="input" placeholder='{"X-Custom": "value"}' />
      </label>

      <label class="form-label">{{ t('webhooks.secret') }}（HMAC）
        <input v-model="form.secret" type="password" class="input" />
      </label>

      <div v-if="formError" class="error">{{ formError }}</div>
      <button class="btn btn-primary" :disabled="!form.name || !form.target || form.events.length === 0" @click="handleSubmit">
        {{ editingId ? t('common.save') : t('common.create') }}
      </button>
    </div>

    <div v-if="loading" class="loading">{{ t('common.loading') }}</div>
    <table v-else class="wh-table">
      <thead>
        <tr>
          <th>{{ t('webhooks.name') }}</th>
          <th>{{ t('webhooks.type') }}</th>
          <th>{{ t('webhooks.target') }}</th>
          <th>{{ t('webhooks.events') }}</th>
          <th>{{ t('webhooks.status') }}</th>
          <th>{{ t('common.actions') }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="w in webhooks" :key="w.id">
          <td>{{ w.name }}</td>
          <td><span class="type-badge">{{ typeLabel(w.type) }}</span></td>
          <td class="mono target-cell">{{ w.target }}</td>
          <td>
            <span v-for="evt in w.events.split(',')" :key="evt" class="event-tag">{{ evt.trim() }}</span>
          </td>
          <td>
            <button class="btn-toggle" :class="{ active: w.isEnabled }" @click="handleToggle(w)">
              {{ w.isEnabled ? t('common.enabled') : t('common.disabled') }}
            </button>
          </td>
          <td class="actions-cell">
            <button class="btn-sm btn-secondary" @click="openEdit(w)">{{ t('common.edit') }}</button>
            <button class="btn-sm btn-secondary" :disabled="testingId === w.id" @click="handleTest(w.id)">
              {{ testingId === w.id ? t('common.loading') : t('common.test') }}
            </button>
            <button class="btn-sm btn-danger" :disabled="deletingId === w.id" @click="handleDelete(w.id)">
              {{ deletingId === w.id ? t('common.loading') : t('common.delete') }}
            </button>
          </td>
        </tr>
        <tr v-if="webhooks.length === 0">
          <td colspan="6" class="empty">{{ t('common.noData') }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.webhooks-page { max-width: 1000px; }
.header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
h2 { margin-bottom: 16px; }
.form-panel { background: white; border-radius: 8px; padding: 24px; box-shadow: 0 1px 3px rgba(0,0,0,.1); margin-bottom: 20px; }
.form-label { display: block; margin-bottom: 12px; font-weight: 500; }
.input { display: block; width: 100%; padding: 8px 12px; border: 1px solid #d1d5db; border-radius: 6px; margin-top: 4px; font-size: 0.95rem; }
.form-fieldset { border: 1px solid #e5e7eb; border-radius: 6px; padding: 12px; margin-bottom: 12px; }
.form-fieldset legend { font-weight: 500; padding: 0 4px; }
.checkbox-label { display: inline-flex; align-items: center; gap: 4px; margin-right: 16px; cursor: pointer; }
.toggle-label { display: inline-flex; align-items: center; gap: 6px; }
.wh-table { width: 100%; border-collapse: collapse; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,.1); }
.wh-table th { background: #f9fafb; text-align: left; padding: 12px; font-weight: 600; border-bottom: 1px solid #e5e7eb; }
.wh-table td { padding: 12px; border-bottom: 1px solid #f3f4f6; }
.mono { font-family: monospace; font-size: 0.85rem; }
.target-cell { max-width: 200px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.type-badge { background: #e0e7ff; color: #3730a3; padding: 2px 8px; border-radius: 4px; font-size: 0.8rem; }
.event-tag { display: inline-block; background: #fef3c7; color: #92400e; padding: 1px 6px; border-radius: 3px; font-size: 0.75rem; margin: 1px 2px; }
.btn-toggle { padding: 3px 10px; border: 1px solid #d1d5db; border-radius: 4px; cursor: pointer; font-size: 0.8rem; background: #f3f4f6; color: #6b7280; }
.btn-toggle.active { background: #d1fae5; color: #065f46; border-color: #a7f3d0; }
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
.toast { position: fixed; top: 20px; right: 20px; background: #065f46; color: white; padding: 10px 20px; border-radius: 6px; z-index: 1000; box-shadow: 0 2px 8px rgba(0,0,0,.2); }
</style>
