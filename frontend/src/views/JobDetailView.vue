<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { jobsApi } from '@/api/jobs'
import { useSignalR } from '@/composables/useSignalR'
import type { Job, JobStatus, StepStatus } from '@/types'

const route = useRoute()
const jobId = route.params.id as string

const job = ref<Job | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const actionLoading = ref(false)

const { connect, connected, onJobProgress, onStepProgress } = useSignalR()

const canCancel = computed(() => {
  const s = job.value?.status
  return s === 'Running' || s === 'Queued' || s === 'Paused'
})
const canPause = computed(() => job.value?.status === 'Running')
const canResume = computed(() => job.value?.status === 'Paused')
const canRetry = computed(() => job.value?.status === 'Failed')

const statusClass = (status: JobStatus | StepStatus) => {
  const map: Record<string, string> = {
    Running: 'badge-running', Queued: 'badge-queued', Pending: 'badge-queued',
    Failed: 'badge-failed', Succeeded: 'badge-succeeded', Retrying: 'badge-running',
    Paused: 'badge-paused', Cancelled: 'badge-cancelled', Skipped: 'badge-cancelled',
  }
  return map[status] ?? 'badge-default'
}

const formatDate = (iso: string) => new Date(iso).toLocaleString('zh-TW')

const fetchJob = async () => {
  loading.value = true
  error.value = null
  try {
    const { data } = await jobsApi.get(jobId)
    job.value = data
  } catch (e) {
    error.value = e instanceof Error ? e.message : '載入任務失敗'
  } finally {
    loading.value = false
  }
}

const doAction = async (action: 'cancel' | 'pause' | 'resume' | 'retry') => {
  actionLoading.value = true
  try {
    await jobsApi[action](jobId)
    await fetchJob()
  } catch (e) {
    error.value = e instanceof Error ? e.message : '操作失敗'
  } finally {
    actionLoading.value = false
  }
}

onMounted(async () => {
  await fetchJob()
  try {
    await connect()
    onJobProgress((progress) => {
      if (progress.jobId === jobId && job.value) {
        job.value.status = progress.status
        job.value.progress = progress.overallProgress
        job.value.steps = progress.steps
      }
    })
    onStepProgress((jId, stepId, progress, status) => {
      if (jId === jobId && job.value) {
        const step = job.value.steps.find(s => s.id === stepId)
        if (step) {
          step.progress = progress
          step.status = status as StepStatus
        }
      }
    })
  } catch {
    // SignalR not available
  }
})
</script>

<template>
  <div class="job-detail">
    <div v-if="loading" class="loading">載入中…</div>
    <div v-else-if="error" class="error">{{ error }}</div>
    <template v-else-if="job">
      <div class="header">
        <h1>任務詳情</h1>
        <div class="signal-status">
          <span :class="connected ? 'dot-green' : 'dot-red'"></span>
          {{ connected ? '即時更新中' : '離線' }}
        </div>
      </div>

      <div class="info-panel">
        <div class="info-row"><span class="label">ID</span><span class="value mono">{{ job.id }}</span></div>
        <div class="info-row"><span class="label">策略</span><span class="value">{{ job.strategy }}</span></div>
        <div class="info-row">
          <span class="label">狀態</span>
          <span :class="['badge', statusClass(job.status)]">{{ job.status }}</span>
        </div>
        <div class="info-row">
          <span class="label">總進度</span>
          <div class="progress-bar"><div class="progress-fill" :style="{ width: job.progress + '%' }"></div></div>
          <span class="progress-text">{{ job.progress }}%</span>
        </div>
        <div class="info-row"><span class="label">建立時間</span><span class="value">{{ formatDate(job.createdAt) }}</span></div>
        <div class="info-row"><span class="label">更新時間</span><span class="value">{{ formatDate(job.updatedAt) }}</span></div>
      </div>

      <div class="actions">
        <button v-if="canPause" class="btn btn-secondary" :disabled="actionLoading" @click="doAction('pause')">暫停</button>
        <button v-if="canResume" class="btn btn-primary" :disabled="actionLoading" @click="doAction('resume')">恢復</button>
        <button v-if="canRetry" class="btn btn-primary" :disabled="actionLoading" @click="doAction('retry')">重試</button>
        <button v-if="canCancel" class="btn btn-danger" :disabled="actionLoading" @click="doAction('cancel')">取消</button>
      </div>

      <h2>步驟</h2>
      <div class="steps">
        <div v-for="step in job.steps" :key="step.id" class="step-card">
          <div class="step-header">
            <span class="step-order">#{{ step.order }}</span>
            <span class="step-name">{{ step.name }}</span>
            <span :class="['badge', statusClass(step.status)]">{{ step.status }}</span>
          </div>
          <div class="step-progress">
            <div class="progress-bar"><div class="progress-fill" :style="{ width: step.progress + '%' }"></div></div>
            <span class="progress-text">{{ step.progress }}%</span>
          </div>
          <div v-if="step.retryCount > 0" class="step-retry">重試次數: {{ step.retryCount }}</div>
          <div v-if="step.errorMessage" class="step-error">{{ step.errorMessage }}</div>
        </div>
        <div v-if="job.steps.length === 0" class="empty">尚無步驟</div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.job-detail { max-width: 800px; }
.header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
h2 { margin: 24px 0 12px; }
.signal-status { font-size: 0.85rem; color: #666; display: flex; align-items: center; gap: 6px; }
.dot-green, .dot-red { width: 8px; height: 8px; border-radius: 50%; display: inline-block; }
.dot-green { background: #22c55e; }
.dot-red { background: #ef4444; }
.info-panel { background: white; border-radius: 8px; padding: 20px; box-shadow: 0 1px 3px rgba(0,0,0,.1); }
.info-row { display: flex; align-items: center; gap: 12px; padding: 8px 0; border-bottom: 1px solid #f3f4f6; }
.info-row:last-child { border-bottom: none; }
.label { font-weight: 500; color: #666; min-width: 100px; }
.mono { font-family: monospace; font-size: 0.85rem; }
.actions { display: flex; gap: 8px; margin-top: 16px; }
.btn { padding: 8px 16px; border: none; border-radius: 6px; cursor: pointer; font-weight: 500; }
.btn-primary { background: #3b82f6; color: white; }
.btn-secondary { background: #e5e7eb; color: #374151; }
.btn-danger { background: #ef4444; color: white; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.steps { display: flex; flex-direction: column; gap: 8px; }
.step-card { background: white; border-radius: 8px; padding: 16px; box-shadow: 0 1px 3px rgba(0,0,0,.1); }
.step-header { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; }
.step-order { font-weight: 700; color: #999; }
.step-name { flex: 1; font-weight: 500; }
.step-progress { display: flex; align-items: center; gap: 8px; }
.progress-bar { flex: 1; height: 6px; background: #e5e7eb; border-radius: 3px; }
.progress-fill { height: 100%; background: #3b82f6; border-radius: 3px; transition: width 0.3s; }
.progress-text { font-size: 0.8rem; color: #666; min-width: 36px; }
.step-retry { font-size: 0.8rem; color: #f59e0b; margin-top: 4px; }
.step-error { font-size: 0.8rem; color: #ef4444; margin-top: 4px; }
.badge { padding: 2px 8px; border-radius: 4px; font-size: 0.8rem; font-weight: 500; }
.badge-running { background: #dbeafe; color: #1d4ed8; }
.badge-queued { background: #fef3c7; color: #92400e; }
.badge-failed { background: #fef2f2; color: #b91c1c; }
.badge-succeeded { background: #dcfce7; color: #166534; }
.badge-paused { background: #f3f4f6; color: #374151; }
.badge-cancelled { background: #f3f4f6; color: #6b7280; }
.badge-default { background: #f3f4f6; color: #374151; }
.loading { color: #666; padding: 20px; }
.error { background: #fef2f2; color: #b91c1c; padding: 12px; border-radius: 6px; }
.empty { text-align: center; color: #999; padding: 24px; }
</style>
