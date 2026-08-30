<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { jobsApi } from '@/api/jobs'
import { useSignalR } from '@/composables/useSignalR'
import type { Job, JobStatus, StepStatus } from '@/types'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()
const jobId = route.params.id as string

const job = ref<Job | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const actionLoading = ref(false)

const { connect, onJobProgress, onStepProgress } = useSignalR()

const canCancel = computed(() => {
  const s = job.value?.status
  return s === 'Running' || s === 'Queued' || s === 'Paused'
})
const canPause = computed(() => job.value?.status === 'Running')
const canResume = computed(() => job.value?.status === 'Paused')
const canRetry = computed(() => job.value?.status === 'Failed')

const statusBadge = (status: JobStatus | StepStatus) => {
  const map: Record<string, { class: string; icon: string }> = {
    Running: { class: 'status-running', icon: '⚡' },
    Queued: { class: 'status-queued', icon: '⏳' },
    Pending: { class: 'status-pending', icon: '⏳' },
    Failed: { class: 'status-failed', icon: '❌' },
    Succeeded: { class: 'status-succeeded', icon: '✅' },
    Retrying: { class: 'status-running', icon: '🔄' },
    Paused: { class: 'status-paused', icon: '⏸️' },
    Cancelled: { class: 'status-cancelled', icon: '🚫' },
    Skipped: { class: 'status-cancelled', icon: '⏭️' },
  }
  return map[status] ?? { class: 'status-default', icon: '•' }
}

const formatDate = (iso: string) => new Date(iso).toLocaleString('zh-TW', { hour12: false })

const fetchJob = async () => {
  loading.value = true
  error.value = null
  try {
    const { data } = await jobsApi.get(jobId)
    job.value = data
  } catch (e) {
    error.value = e instanceof Error ? e.message : t('jobs.loadFailed')
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
    error.value = e instanceof Error ? e.message : t('jobs.actionFailed')
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
        const step = job.value.steps.find((s) => s.id === stepId)
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
  <div class="job-detail-container">
    <!-- Top Nav Back -->
    <div class="nav-back">
      <button class="neu-back-btn" @click="router.push('/')">
        <span>← {{ t('nav.dashboard') }}</span>
      </button>
    </div>

    <div v-if="loading" class="glass-card loading-state">
      <span class="spinner">🌀</span>
      <p>{{ t('common.loading') }}</p>
    </div>

    <div v-else-if="error" class="neu-banner banner-error">{{ error }}</div>

    <template v-else-if="job">
      <!-- Main Overview Hero Card -->
      <section class="glass-card hero-card">
        <div class="hero-top">
          <div class="hero-id-badge">
            <span class="badge-icon">📦</span>
            <div class="id-info">
              <span class="id-label">MIGRATION JOB</span>
              <span class="id-val">{{ job.id }}</span>
            </div>
          </div>

          <div class="hero-status">
            <span :class="['status-pill', statusBadge(job.status).class]">
              <span class="pill-icon">{{ statusBadge(job.status).icon }}</span>
              <span class="pill-text">{{ job.status }}</span>
            </span>
          </div>
        </div>

        <!-- Overall Progress Display -->
        <div class="hero-progress-section">
          <div class="progress-info">
            <span class="progress-label">{{ t('jobs.totalProgress') }}</span>
            <span class="progress-percent">{{ job.progress }}%</span>
          </div>
          <div class="glass-progress-track large">
            <div class="glass-progress-bar" :style="{ width: job.progress + '%' }"></div>
          </div>
        </div>

        <!-- Specs Grid -->
        <div class="specs-grid">
          <div class="spec-card">
            <span class="spec-label">{{ t('jobs.strategy') }}</span>
            <span class="spec-val">{{ job.strategy }}</span>
          </div>
          <div class="spec-card">
            <span class="spec-label">{{ t('dashboard.table.createdAt') }}</span>
            <span class="spec-val">{{ formatDate(job.createdAt) }}</span>
          </div>
          <div class="spec-card">
            <span class="spec-label">{{ t('jobs.updatedAt') }}</span>
            <span class="spec-val">{{ formatDate(job.updatedAt) }}</span>
          </div>
        </div>

        <!-- Action Buttons -->
        <div class="hero-actions">
          <button v-if="canPause" class="neu-action-btn pause-btn" :disabled="actionLoading" @click="doAction('pause')">
            <span>⏸️ {{ t('jobs.pause') }}</span>
          </button>
          <button v-if="canResume" class="neu-action-btn resume-btn" :disabled="actionLoading" @click="doAction('resume')">
            <span>▶️ {{ t('jobs.resume') }}</span>
          </button>
          <button v-if="canRetry" class="neu-action-btn retry-btn" :disabled="actionLoading" @click="doAction('retry')">
            <span>🔄 {{ t('jobs.retryShort') }}</span>
          </button>
          <button v-if="canCancel" class="neu-action-btn cancel-btn" :disabled="actionLoading" @click="doAction('cancel')">
            <span>🚫 {{ t('jobs.cancelShort') }}</span>
          </button>
        </div>
      </section>

      <!-- Step Pipeline Timeline Section -->
      <section class="steps-section">
        <div class="section-title">
          <span>⚡</span>
          <h2>{{ t('jobs.steps') }} ({{ job.steps.length }})</h2>
        </div>

        <div class="steps-grid">
          <div
            v-for="step in job.steps"
            :key="step.id"
            :class="['glass-card', 'step-item-card', { active: step.status === 'Running' }]"
          >
            <div class="step-card-header">
              <div class="step-title-box">
                <span class="step-num">#{{ step.order }}</span>
                <span class="step-name">{{ step.name }}</span>
              </div>
              <span :class="['status-pill-sm', statusBadge(step.status).class]">
                {{ step.status }}
              </span>
            </div>

            <div class="step-progress-row">
              <div class="glass-progress-track">
                <div class="glass-progress-bar" :style="{ width: step.progress + '%' }"></div>
              </div>
              <span class="step-pct">{{ step.progress }}%</span>
            </div>

            <div v-if="step.retryCount > 0" class="step-retry-text">
              ⚠️ {{ t('jobs.retryCount') }}: {{ step.retryCount }}
            </div>
            <div v-if="step.errorMessage" class="step-error-banner">
              {{ step.errorMessage }}
            </div>
          </div>
        </div>
      </section>
    </template>
  </div>
</template>

<style scoped>
.job-detail-container {
  display: flex;
  flex-direction: column;
  gap: 28px;
  max-width: 960px;
  margin: 0 auto;
}

.nav-back {
  display: flex;
}

.neu-back-btn {
  background: var(--bg-surface);
  backdrop-filter: var(--glass-blur);
  border: var(--glass-border);
  border-radius: 12px;
  padding: 8px 16px;
  font-size: 0.9rem;
  font-weight: 700;
  color: var(--text-secondary);
  box-shadow: var(--neu-shadow-sm);
  cursor: pointer;
  transition: all 0.2s ease;
}

.neu-back-btn:hover {
  color: var(--text-primary);
  transform: translateX(-4px);
}

/* Glass Card */
.glass-card {
  background: var(--bg-surface);
  backdrop-filter: var(--glass-blur);
  border: var(--glass-border);
  border-radius: 20px;
  box-shadow: var(--neu-shadow);
  padding: 32px;
}

/* Hero Card */
.hero-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 28px;
}

.hero-id-badge {
  display: flex;
  align-items: center;
  gap: 14px;
}

.badge-icon {
  width: 48px;
  height: 48px;
  border-radius: 14px;
  background: var(--bg-surface-elevated);
  box-shadow: var(--neu-inset);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.4rem;
}

.id-info {
  display: flex;
  flex-direction: column;
}

.id-label {
  font-size: 0.75rem;
  font-weight: 800;
  color: var(--primary);
  letter-spacing: 0.5px;
}

.id-val {
  font-family: monospace;
  font-size: 1.1rem;
  font-weight: 700;
  color: var(--text-primary);
}

/* Progress Section */
.hero-progress-section {
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  border-radius: 16px;
  padding: 20px 24px;
  box-shadow: var(--neu-inset);
  margin-bottom: 24px;
}

.progress-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
}

.progress-label {
  font-weight: 700;
  font-size: 0.95rem;
}

.progress-percent {
  font-weight: 800;
  font-size: 1.4rem;
  color: var(--primary);
}

.glass-progress-track {
  width: 100%;
  height: 8px;
  border-radius: 999px;
  background: var(--bg-surface);
  overflow: hidden;
  box-shadow: var(--neu-inset);
}

.glass-progress-track.large {
  height: 12px;
}

.glass-progress-bar {
  height: 100%;
  background: var(--primary-gradient);
  border-radius: 999px;
  transition: width 0.4s ease;
}

/* Specs Grid */
.specs-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
  margin-bottom: 28px;
}

.spec-card {
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  border-radius: 14px;
  padding: 14px 18px;
  box-shadow: var(--neu-shadow-sm);
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.spec-label { font-size: 0.75rem; color: var(--text-muted); font-weight: 700; text-transform: uppercase; }
.spec-val { font-size: 0.95rem; font-weight: 700; }

/* Hero Actions */
.hero-actions {
  display: flex;
  gap: 12px;
  justify-content: flex-end;
}

.neu-action-btn {
  padding: 10px 20px;
  border-radius: 12px;
  font-size: 0.9rem;
  font-weight: 700;
  border: none;
  cursor: pointer;
  box-shadow: var(--neu-shadow-sm);
  transition: all 0.2s ease;
}

.pause-btn, .resume-btn { background: var(--primary-gradient); color: white; }
.retry-btn { background: var(--warning-bg); color: var(--warning); border: 1px solid var(--warning); }
.cancel-btn { background: var(--danger-bg); color: var(--danger); border: 1px solid var(--danger); }

.neu-action-btn:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: var(--neu-shadow-hover);
}

/* Steps Pipeline */
.steps-section {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.section-title {
  display: flex;
  align-items: center;
  gap: 8px;
}

.section-title h2 {
  font-size: 1.3rem;
  font-weight: 800;
}

.steps-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 14px;
}

.step-item-card {
  padding: 20px 24px;
  border-radius: 16px;
  transition: all 0.25s ease;
}

.step-item-card.active {
  border-left: 6px solid var(--primary);
  background: var(--bg-surface-elevated);
}

.step-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.step-title-box {
  display: flex;
  align-items: center;
  gap: 10px;
}

.step-num {
  font-size: 0.85rem;
  font-weight: 800;
  color: var(--text-muted);
  background: var(--bg-surface-elevated);
  padding: 2px 8px;
  border-radius: 6px;
  box-shadow: var(--neu-inset);
}

.step-name {
  font-size: 1rem;
  font-weight: 700;
}

.step-progress-row {
  display: flex;
  align-items: center;
  gap: 14px;
}

.step-pct {
  font-size: 0.85rem;
  font-weight: 700;
  color: var(--text-muted);
  min-width: 36px;
}

.step-retry-text {
  font-size: 0.8rem;
  color: var(--warning);
  font-weight: 700;
  margin-top: 8px;
}

.step-error-banner {
  background: var(--danger-bg);
  color: var(--danger);
  border: 1px solid var(--danger);
  padding: 10px 14px;
  border-radius: 10px;
  font-size: 0.85rem;
  margin-top: 10px;
  font-weight: 600;
}

/* Status Badges */
.status-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 16px;
  border-radius: 999px;
  font-size: 0.85rem;
  font-weight: 700;
}

.status-pill-sm {
  font-size: 0.75rem;
  font-weight: 700;
  padding: 3px 10px;
  border-radius: 999px;
}

.status-running { background: var(--primary-glow); color: var(--primary); }
.status-queued, .status-pending { background: var(--warning-bg); color: var(--warning); }
.status-succeeded { background: var(--success-bg); color: var(--success); }
.status-failed { background: var(--danger-bg); color: var(--danger); }
.status-paused, .status-cancelled { background: rgba(148, 163, 184, 0.15); color: #64748b; }

@media (max-width: 768px) {
  .specs-grid { grid-template-columns: 1fr; }
  .hero-top { flex-direction: column; align-items: flex-start; gap: 12px; }
  .hero-actions { justify-content: stretch; flex-direction: column; }
}
</style>
