<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useConnectionsStore } from '@/stores/connections'
import { connectionsApi } from '@/api/connections'
import { jobsApi } from '@/api/jobs'
import type {
  ArtifactFormat,
  CreateJobRequest,
  MigrationStrategy,
  PreFlightCheckResult,
  VmInfo,
} from '@/types'

const router = useRouter()
const { t } = useI18n()
const connectionsStore = useConnectionsStore()

const currentStep = ref(1)
const totalSteps = 5
const submitting = ref(false)
const submitError = ref<string | null>(null)

const selectedVmId = ref('')
const availableVms = ref<VmInfo[]>([])
const loadingVms = ref(false)
const vmLoadError = ref<string | null>(null)

const preflightResult = ref<PreFlightCheckResult | null>(null)
const preflightRunning = ref(false)
const preflightError = ref<string | null>(null)

const form = ref<CreateJobRequest>({
  sourceConnectionId: '',
  targetConnectionId: '',
  storageTarget: { type: 'S3', endpoint: '', bucketOrPath: '', region: '' },
  strategy: 'FullCopy',
  options: { targetDiskFormat: 'Qcow2', verifyChecksum: true, maxRetries: 3 },
  vmId: '',
})

const sourceConnection = computed(() =>
  connectionsStore.connections.find((c) => c.id === form.value.sourceConnectionId),
)

const targetConnection = computed(() =>
  connectionsStore.connections.find((c) => c.id === form.value.targetConnectionId),
)

const isHyperVSource = computed(() => sourceConnection.value?.type === 'HyperV')

const availableStrategies = computed<MigrationStrategy[]>(() =>
  isHyperVSource.value ? ['FullCopy'] : ['FullCopy', 'Incremental'],
)

const diskFormats = computed<ArtifactFormat[]>(() =>
  isHyperVSource.value ? ['Qcow2', 'Raw', 'Vhdx'] : ['Vmdk', 'Qcow2', 'Raw'],
)

const selectedVm = computed(() => availableVms.value.find((vm) => vm.id === selectedVmId.value))

const preflightPassed = computed(() => preflightResult.value?.isAllPassed === true)

const canNext = computed(() => {
  switch (currentStep.value) {
    case 1:
      return form.value.sourceConnectionId !== '' && selectedVmId.value !== ''
    case 2:
      return form.value.targetConnectionId !== ''
    case 3:
      return form.value.storageTarget.endpoint !== '' && form.value.storageTarget.bucketOrPath !== ''
    case 4:
      if (isHyperVSource.value) {
        return form.value.strategy === 'FullCopy' && preflightPassed.value
      }
      return true
    case 5:
      return !isHyperVSource.value || preflightPassed.value
    default:
      return false
  }
})

const next = () => {
  if (currentStep.value < totalSteps && canNext.value) currentStep.value++
}
const prev = () => {
  if (currentStep.value > 1) currentStep.value--
}

const loadVms = async (connectionId: string) => {
  if (!connectionId) {
    availableVms.value = []
    return
  }

  loadingVms.value = true
  vmLoadError.value = null
  try {
    const { data } = await connectionsApi.listVms(connectionId)
    availableVms.value = data
  } catch (e) {
    availableVms.value = []
    vmLoadError.value = e instanceof Error ? e.message : t('common.loadFailed')
  } finally {
    loadingVms.value = false
  }
}

const runPreflight = async () => {
  if (!form.value.sourceConnectionId || !selectedVmId.value) return

  preflightRunning.value = true
  preflightError.value = null
  try {
    const { data } = await connectionsApi.runPreflight(form.value.sourceConnectionId, selectedVmId.value)
    preflightResult.value = data
  } catch (e) {
    preflightResult.value = null
    preflightError.value = e instanceof Error ? e.message : t('jobs.preflight.failed')
  } finally {
    preflightRunning.value = false
  }
}

watch(
  () => form.value.sourceConnectionId,
  async (connectionId) => {
    selectedVmId.value = ''
    preflightResult.value = null
    preflightError.value = null
    await loadVms(connectionId)

    if (isHyperVSource.value) {
      form.value.strategy = 'FullCopy'
      if (!diskFormats.value.includes(form.value.options.targetDiskFormat)) {
        form.value.options.targetDiskFormat = 'Qcow2'
      }
    }
  },
)

watch(selectedVmId, () => {
  preflightResult.value = null
  preflightError.value = null
})

const submit = async () => {
  if (isHyperVSource.value && !preflightPassed.value) {
    submitError.value = t('jobs.preflight.required')
    return
  }

  submitting.value = true
  submitError.value = null
  try {
    const { data } = await jobsApi.create({
      ...form.value,
      vmId: selectedVmId.value,
      diskKeys: selectedVm.value?.diskKeys ?? [],
    })
    router.push(`/jobs/${data.id}`)
  } catch (e) {
    submitError.value = e instanceof Error ? e.message : t('jobs.createFailed')
  } finally {
    submitting.value = false
  }
}

onMounted(() => connectionsStore.fetchConnections())
</script>

<template>
  <div class="wizard-container">
    <!-- Wizard Header -->
    <div class="wizard-header">
      <h1 class="wizard-title">🚀 {{ t('jobs.title') }}</h1>
      <p class="wizard-subtitle">Orchestrate full automated virtual machine transfer across hypervisors.</p>
    </div>

    <!-- Stepper Progress (Neumorphic Pills) -->
    <div class="wizard-stepper">
      <div
        v-for="s in totalSteps"
        :key="s"
        :class="['stepper-item', { active: s === currentStep, completed: s < currentStep }]"
      >
        <div class="stepper-circle">
          <span v-if="s < currentStep">✓</span>
          <span v-else>{{ s }}</span>
        </div>
        <span class="stepper-label">
          {{ s === 1 ? 'Source' : s === 2 ? 'Target' : s === 3 ? 'Storage' : s === 4 ? 'Config' : 'Review' }}
        </span>
        <div v-if="s < totalSteps" class="stepper-line"></div>
      </div>
    </div>

    <!-- Step 1: Select Source VM -->
    <transition name="fade" mode="out-in">
      <div v-if="currentStep === 1" class="glass-card step-card">
        <h2 class="step-title">📍 {{ t('jobs.selectSource') }}</h2>

        <div class="form-group">
          <label class="form-label">{{ t('jobs.selectSource') }}</label>
          <select v-model="form.sourceConnectionId" class="neu-input">
            <option value="" disabled>{{ t('jobs.selectSource') }}…</option>
            <option v-for="c in connectionsStore.connections" :key="c.id" :value="c.id">
              {{ c.name }} ({{ t(`connections.types.${c.type}`) }}) — {{ c.endpoint }}
            </option>
          </select>
        </div>

        <div v-if="form.sourceConnectionId" class="vm-selection-box">
          <label class="form-label">{{ t('jobs.selectVm') }}</label>
          <div v-if="loadingVms" class="loading-bar">🌀 {{ t('common.loading') }}</div>
          <div v-else-if="vmLoadError" class="neu-banner banner-error">{{ vmLoadError }}</div>
          <select v-else v-model="selectedVmId" class="neu-input">
            <option value="" disabled>{{ t('jobs.selectVmPlaceholder') }}</option>
            <option v-for="vm in availableVms" :key="vm.id" :value="vm.id">
              {{ vm.name }} ({{ vm.id }}) — CPU: {{ vm.cpuCount }} Cores, Disks: {{ vm.diskKeys.length }}
            </option>
          </select>

          <div v-if="selectedVm" class="vm-preview-card">
            <div class="preview-header">
              <span class="preview-tag">SELECTED VM PROFILE</span>
              <span class="vm-name">{{ selectedVm.name }}</span>
            </div>
            <div class="preview-specs">
              <div class="spec-item">
                <span class="spec-label">Compute</span>
                <span class="spec-val">{{ selectedVm.cpuCount }} vCPU / {{ (selectedVm.memoryBytes / (1024 * 1024 * 1024)).toFixed(1) }} GB</span>
              </div>
              <div class="spec-item">
                <span class="spec-label">Disks Included</span>
                <span class="spec-val">{{ selectedVm.diskKeys.join(', ') }}</span>
              </div>
            </div>
            <p v-if="isHyperVSource" class="hint-msg">
              ℹ️ {{ t('jobs.offlineExportHint') }}
            </p>
          </div>
        </div>
      </div>

      <!-- Step 2: Target Hypervisor -->
      <div v-else-if="currentStep === 2" class="glass-card step-card">
        <h2 class="step-title">🎯 {{ t('jobs.selectTarget') }}</h2>

        <div class="form-group">
          <label class="form-label">{{ t('jobs.selectTarget') }}</label>
          <select v-model="form.targetConnectionId" class="neu-input">
            <option value="" disabled>{{ t('jobs.selectTarget') }}…</option>
            <option v-for="c in connectionsStore.connections" :key="c.id" :value="c.id">
              {{ c.name }} ({{ t(`connections.types.${c.type}`) }}) — {{ c.endpoint }}
            </option>
          </select>
        </div>
      </div>

      <!-- Step 3: Staging Artifact Storage -->
      <div v-else-if="currentStep === 3" class="glass-card step-card">
        <h2 class="step-title">📦 {{ t('jobs.storageSettings') }}</h2>

        <div class="form-grid">
          <div class="form-group">
            <label class="form-label">{{ t('connections.type') }}</label>
            <select v-model="form.storageTarget.type" class="neu-input">
              <option value="S3">S3 / MinIO (Object Storage)</option>
              <option value="Local">Local Filesystem</option>
            </select>
          </div>

          <div class="form-group">
            <label class="form-label">{{ t('connections.host') }}</label>
            <input v-model="form.storageTarget.endpoint" class="neu-input" placeholder="http://minio:9000" />
          </div>

          <div class="form-group">
            <label class="form-label">Bucket / Path</label>
            <input v-model="form.storageTarget.bucketOrPath" class="neu-input" placeholder="vmto-artifacts" />
          </div>

          <div class="form-group">
            <label class="form-label">Region (Optional)</label>
            <input v-model="form.storageTarget.region" class="neu-input" placeholder="us-east-1" />
          </div>
        </div>
      </div>

      <!-- Step 4: Migration Strategy & Pre-flight -->
      <div v-else-if="currentStep === 4" class="glass-card step-card">
        <h2 class="step-title">⚙️ {{ t('jobs.migrationOptions') }}</h2>

        <div class="form-grid">
          <div class="form-group">
            <label class="form-label">{{ t('jobs.strategy') }}</label>
            <select v-model="form.strategy" class="neu-input" :disabled="isHyperVSource">
              <option v-for="strategy in availableStrategies" :key="strategy" :value="strategy">
                {{ t(`jobs.strategies.${strategy}`) }}
              </option>
            </select>
          </div>

          <div class="form-group">
            <label class="form-label">{{ t('jobs.diskFormat') }}</label>
            <select v-model="form.options.targetDiskFormat" class="neu-input">
              <option v-for="f in diskFormats" :key="f" :value="f">{{ f }}</option>
            </select>
          </div>

          <div class="form-group full-width">
            <label class="neu-checkbox">
              <input type="checkbox" v-model="form.options.verifyChecksum" />
              <span class="checkbox-box"></span>
              <span class="checkbox-text">{{ t('jobs.verifyChecksum') }}</span>
            </label>
          </div>

          <div class="form-group">
            <label class="form-label">{{ t('jobs.maxRetries') }}</label>
            <input type="number" v-model.number="form.options.maxRetries" class="neu-input" min="0" max="10" />
          </div>
        </div>

        <!-- Hyper-V Pre-flight Panel -->
        <div v-if="isHyperVSource" class="preflight-glass-panel">
          <div class="preflight-top">
            <div class="preflight-title">
              <span>🛡️</span>
              <h3>{{ t('jobs.preflight.title') }}</h3>
            </div>
            <button
              class="neu-btn btn-sm btn-primary"
              :disabled="!selectedVmId || preflightRunning"
              @click="runPreflight"
            >
              <span>{{ preflightRunning ? '⏳ ' + t('jobs.preflight.running') : '▶ ' + t('jobs.preflight.run') }}</span>
            </button>
          </div>

          <div v-if="preflightError" class="neu-banner banner-error">{{ preflightError }}</div>

          <div v-if="preflightResult" :class="['preflight-status-card', preflightPassed ? 'passed' : 'failed']">
            <div class="status-summary">
              <span class="status-icon">{{ preflightPassed ? '✅' : '❌' }}</span>
              <span>{{ preflightPassed ? t('jobs.preflight.allPassed') : t('jobs.preflight.hasFailures') }}</span>
            </div>

            <div class="check-items-list">
              <div v-for="item in preflightResult.items" :key="item.name" :class="['check-item', item.isPassed ? 'ok' : 'bad']">
                <span class="item-badge">{{ item.isPassed ? 'PASS' : 'FAIL' }}</span>
                <div class="item-content">
                  <div class="item-name">{{ t(`jobs.preflight.items.${item.name}`, item.name) }}</div>
                  <div class="item-msg">{{ item.message }}</div>
                  <div v-if="item.details" class="item-details">{{ item.details }}</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Step 5: Review & Submit -->
      <div v-else-if="currentStep === 5" class="glass-card step-card">
        <h2 class="step-title">📋 {{ t('jobs.reviewSubmit') }}</h2>

        <div class="review-grid">
          <div class="review-item">
            <span class="review-label">{{ t('jobs.source') }}</span>
            <span class="review-val">{{ sourceConnection?.name }} ({{ sourceConnection?.type }})</span>
          </div>

          <div class="review-item">
            <span class="review-label">Selected VM</span>
            <span class="review-val highlight">{{ selectedVm?.name }} ({{ selectedVmId }})</span>
          </div>

          <div class="review-item">
            <span class="review-label">{{ t('jobs.target') }}</span>
            <span class="review-val">{{ targetConnection?.name }} ({{ targetConnection?.type }})</span>
          </div>

          <div class="review-item">
            <span class="review-label">Storage Staging</span>
            <span class="review-val">{{ form.storageTarget.type }}://{{ form.storageTarget.endpoint }}/{{ form.storageTarget.bucketOrPath }}</span>
          </div>

          <div class="review-item">
            <span class="review-label">Strategy</span>
            <span class="review-val">{{ form.strategy }}</span>
          </div>

          <div class="review-item">
            <span class="review-label">Target Format</span>
            <span class="review-val">{{ form.options.targetDiskFormat }}</span>
          </div>

          <div class="review-item">
            <span class="review-label">Verification</span>
            <span class="review-val">{{ form.options.verifyChecksum ? 'Enabled (SHA256)' : 'Disabled' }}</span>
          </div>

          <div class="review-item">
            <span class="review-label">Max Retries</span>
            <span class="review-val">{{ form.options.maxRetries }}</span>
          </div>
        </div>

        <div v-if="submitError" class="neu-banner banner-error">{{ submitError }}</div>
      </div>
    </transition>

    <!-- Wizard Actions Footer -->
    <div class="wizard-footer">
      <button v-if="currentStep > 1" class="neu-btn btn-secondary" @click="prev">
        <span>← {{ t('jobs.prev') }}</span>
      </button>

      <button
        v-if="currentStep < totalSteps"
        class="neu-btn btn-primary"
        :disabled="!canNext"
        @click="next"
      >
        <span>{{ t('jobs.next') }} →</span>
      </button>

      <button
        v-if="currentStep === totalSteps"
        class="neu-btn btn-primary"
        :disabled="submitting || !canNext"
        @click="submit"
      >
        <span>{{ submitting ? '⏳ ' + t('jobs.submitting') : '🚀 ' + t('jobs.createJob') }}</span>
      </button>
    </div>
  </div>
</template>

<style scoped>
.wizard-container {
  max-width: 860px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.wizard-header {
  text-align: center;
  margin-bottom: 8px;
}

.wizard-title {
  font-size: 2rem;
  font-weight: 800;
  letter-spacing: -0.5px;
  margin-bottom: 6px;
}

.wizard-subtitle {
  color: var(--text-secondary);
  font-size: 0.95rem;
}

/* Stepper Progress */
.wizard-stepper {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: var(--bg-surface);
  backdrop-filter: var(--glass-blur);
  border: var(--glass-border);
  border-radius: 16px;
  padding: 16px 28px;
  box-shadow: var(--neu-shadow-sm);
}

.stepper-item {
  display: flex;
  align-items: center;
  gap: 10px;
  position: relative;
  flex: 1;
}

.stepper-circle {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: var(--bg-surface-elevated);
  box-shadow: var(--neu-shadow-sm);
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 800;
  font-size: 0.9rem;
  color: var(--text-muted);
  transition: all 0.3s ease;
}

.stepper-item.active .stepper-circle {
  background: var(--primary-gradient);
  color: white;
  box-shadow: 0 4px 14px var(--primary-glow);
}

.stepper-item.completed .stepper-circle {
  background: var(--success);
  color: white;
}

.stepper-label {
  font-size: 0.85rem;
  font-weight: 700;
  color: var(--text-muted);
}

.stepper-item.active .stepper-label {
  color: var(--text-primary);
}

.stepper-line {
  flex: 1;
  height: 2px;
  background: var(--border-color);
  margin: 0 12px;
}

/* Step Card */
.step-card {
  padding: 32px;
}

.step-title {
  font-size: 1.3rem;
  font-weight: 800;
  margin-bottom: 24px;
}

/* Forms */
.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
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
  outline: none;
  transition: all 0.2s ease;
}

.neu-input:focus {
  border-color: var(--primary);
  box-shadow: 0 0 0 3px var(--primary-glow), var(--neu-inset);
}

.neu-checkbox {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  cursor: pointer;
  user-select: none;
}

.neu-checkbox input {
  display: none;
}

.checkbox-box {
  width: 22px;
  height: 22px;
  border-radius: 6px;
  background: var(--bg-surface-elevated);
  box-shadow: var(--neu-inset);
  border: var(--glass-border-subtle);
  display: flex;
  align-items: center;
  justify-content: center;
}

.neu-checkbox input:checked + .checkbox-box {
  background: var(--primary-gradient);
  border-color: transparent;
}

.neu-checkbox input:checked + .checkbox-box::after {
  content: '✓';
  color: white;
  font-size: 0.85rem;
  font-weight: 800;
}

.checkbox-text {
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--text-primary);
}

/* VM Preview Card */
.vm-selection-box {
  margin-top: 20px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.vm-preview-card {
  margin-top: 12px;
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  border-radius: 14px;
  padding: 16px 20px;
  box-shadow: var(--neu-shadow-sm);
}

.preview-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.preview-tag {
  font-size: 0.75rem;
  font-weight: 800;
  color: var(--primary);
  letter-spacing: 0.5px;
}

.vm-name {
  font-weight: 700;
  font-size: 1rem;
}

.preview-specs {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  margin-bottom: 12px;
}

.spec-item {
  display: flex;
  flex-direction: column;
}

.spec-label { font-size: 0.75rem; color: var(--text-muted); }
.spec-val { font-weight: 700; font-size: 0.9rem; }
.hint-msg { font-size: 0.85rem; color: var(--text-muted); }

/* Pre-flight Panel */
.preflight-glass-panel {
  margin-top: 24px;
  padding-top: 20px;
  border-top: var(--glass-border-subtle);
}

.preflight-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.preflight-title {
  display: flex;
  align-items: center;
  gap: 8px;
}

.preflight-title h3 {
  font-size: 1.1rem;
  font-weight: 800;
}

.preflight-status-card {
  border-radius: 14px;
  padding: 18px;
  box-shadow: var(--neu-inset);
}

.preflight-status-card.passed { background: var(--success-bg); border: 1px solid var(--success); }
.preflight-status-card.failed { background: var(--danger-bg); border: 1px solid var(--danger); }

.status-summary {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 800;
  font-size: 0.95rem;
  margin-bottom: 14px;
}

.check-items-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.check-item {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  background: var(--bg-surface-elevated);
  padding: 10px 14px;
  border-radius: 10px;
  box-shadow: var(--neu-shadow-sm);
}

.item-badge {
  font-size: 0.7rem;
  font-weight: 800;
  padding: 2px 6px;
  border-radius: 4px;
}

.check-item.ok .item-badge { background: var(--success); color: white; }
.check-item.bad .item-badge { background: var(--danger); color: white; }

.item-name { font-weight: 700; font-size: 0.85rem; }
.item-msg { font-size: 0.8rem; color: var(--text-secondary); }
.item-details { font-size: 0.75rem; color: var(--text-muted); }

/* Review Grid */
.review-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.review-item {
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  border-radius: 12px;
  padding: 14px 16px;
  box-shadow: var(--neu-shadow-sm);
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.review-label { font-size: 0.75rem; color: var(--text-muted); font-weight: 600; text-transform: uppercase; }
.review-val { font-size: 0.95rem; font-weight: 700; }
.review-val.highlight { color: var(--primary); }

/* Wizard Footer */
.wizard-footer {
  display: flex;
  justify-content: flex-end;
  gap: 14px;
}

/* Transitions */
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease, transform 0.2s ease; }
.fade-enter-from { opacity: 0; transform: translateY(6px); }
.fade-leave-to { opacity: 0; transform: translateY(-6px); }

@media (max-width: 768px) {
  .wizard-stepper { display: none; }
  .form-grid, .review-grid, .preview-specs { grid-template-columns: 1fr; }
  .full-width { grid-column: span 1; }
}
</style>
