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
  options: { targetDiskFormat: 'Qcow2', deleteSourceAfter: false, verifyChecksum: true, maxRetries: 3 },
})

const sourceConnection = computed(() =>
  connectionsStore.connections.find((c) => c.id === form.value.sourceConnectionId),
)

const isHyperVSource = computed(() => sourceConnection.value?.type === 'HyperV')

const availableStrategies = computed<MigrationStrategy[]>(() =>
  isHyperVSource.value ? ['HyperVOffline'] : ['FullCopy', 'Incremental'],
)

const diskFormats = computed<ArtifactFormat[]>(() =>
  isHyperVSource.value ? ['Qcow2', 'Raw', 'Vhdx'] : ['Vmdk', 'Qcow2', 'Raw'],
)

const preflightPassed = computed(() => preflightResult.value?.isAllPassed === true)

const canNext = computed(() => {
  switch (currentStep.value) {
    case 1:
      return form.value.sourceConnectionId !== '' && (!isHyperVSource.value || selectedVmId.value !== '')
    case 2:
      return form.value.targetConnectionId !== ''
    case 3:
      return form.value.storageTarget.endpoint !== '' && form.value.storageTarget.bucketOrPath !== ''
    case 4:
      if (isHyperVSource.value) {
        return form.value.strategy === 'HyperVOffline' && preflightPassed.value
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
      form.value.strategy = 'HyperVOffline'
      if (!diskFormats.value.includes(form.value.options.targetDiskFormat)) {
        form.value.options.targetDiskFormat = 'Qcow2'
      }
    } else if (form.value.strategy === 'HyperVOffline') {
      form.value.strategy = 'FullCopy'
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
    const { data } = await jobsApi.create(form.value)
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
  <div class="new-job">
    <h1>{{ t('jobs.title') }}</h1>

    <div class="step-indicator">
      <div
        v-for="s in totalSteps"
        :key="s"
        :class="['step-dot', { active: s === currentStep, done: s < currentStep }]"
      >
        {{ s }}
      </div>
    </div>

    <!-- Step 1: Source -->
    <div v-if="currentStep === 1" class="step-panel">
      <h2>{{ t('jobs.selectSource') }}</h2>
      <select v-model="form.sourceConnectionId" class="input">
        <option value="" disabled>{{ t('jobs.selectSource') }}…</option>
        <option v-for="c in connectionsStore.connections" :key="c.id" :value="c.id">
          {{ c.name }} ({{ t(`connections.types.${c.type}`) }}) — {{ c.endpoint }}
        </option>
      </select>

      <div v-if="form.sourceConnectionId" class="vm-section">
        <h3>{{ t('jobs.selectVm') }}</h3>
        <div v-if="loadingVms" class="muted">{{ t('common.loading') }}</div>
        <div v-else-if="vmLoadError" class="error">{{ vmLoadError }}</div>
        <select v-else v-model="selectedVmId" class="input">
          <option value="" disabled>{{ t('jobs.selectVmPlaceholder') }}</option>
          <option v-for="vm in availableVms" :key="vm.id" :value="vm.id">
            {{ vm.name }} ({{ vm.id }}) — CPU {{ vm.cpuCount }}, Disks {{ vm.diskKeys.length }}
          </option>
        </select>
        <p v-if="isHyperVSource" class="hint">
          {{ t('jobs.strategies.HyperVOffline') }}
        </p>
      </div>
    </div>

    <!-- Step 2: Target -->
    <div v-if="currentStep === 2" class="step-panel">
      <h2>{{ t('jobs.selectTarget') }}</h2>
      <select v-model="form.targetConnectionId" class="input">
        <option value="" disabled>{{ t('jobs.selectTarget') }}…</option>
        <option v-for="c in connectionsStore.connections" :key="c.id" :value="c.id">
          {{ c.name }} ({{ t(`connections.types.${c.type}`) }}) — {{ c.endpoint }}
        </option>
      </select>
    </div>

    <!-- Step 3: Storage -->
    <div v-if="currentStep === 3" class="step-panel">
      <h2>{{ t('jobs.storageSettings') }}</h2>
      <label class="form-label">{{ t('connections.type') }}
        <select v-model="form.storageTarget.type" class="input">
          <option value="S3">S3</option>
          <option value="NFS">NFS</option>
          <option value="Local">Local</option>
        </select>
      </label>
      <label class="form-label">{{ t('connections.host') }}
        <input v-model="form.storageTarget.endpoint" class="input" placeholder="https://s3.example.com" />
      </label>
      <label class="form-label">Bucket / Path
        <input v-model="form.storageTarget.bucketOrPath" class="input" placeholder="my-bucket" />
      </label>
      <label class="form-label">Region
        <input v-model="form.storageTarget.region" class="input" placeholder="us-east-1" />
      </label>
    </div>

    <!-- Step 4: Options + Pre-flight -->
    <div v-if="currentStep === 4" class="step-panel">
      <h2>{{ t('jobs.migrationOptions') }}</h2>
      <label class="form-label">{{ t('jobs.strategy') }}
        <select v-model="form.strategy" class="input" :disabled="isHyperVSource">
          <option v-for="strategy in availableStrategies" :key="strategy" :value="strategy">
            {{ t(`jobs.strategies.${strategy}`) }}
          </option>
        </select>
      </label>
      <label class="form-label">{{ t('jobs.diskFormat') }}
        <select v-model="form.options.targetDiskFormat" class="input">
          <option v-for="f in diskFormats" :key="f" :value="f">{{ f }}</option>
        </select>
      </label>
      <label class="form-label checkbox-label">
        <input type="checkbox" v-model="form.options.verifyChecksum" /> {{ t('jobs.verifyChecksum') }}
      </label>
      <label class="form-label checkbox-label">
        <input type="checkbox" v-model="form.options.deleteSourceAfter" /> {{ t('jobs.deleteSourceAfter') }}
      </label>
      <label class="form-label">{{ t('jobs.maxRetries') }}
        <input type="number" v-model.number="form.options.maxRetries" class="input" min="0" max="10" />
      </label>

      <div v-if="isHyperVSource" class="preflight-panel">
        <div class="preflight-header">
          <h3>{{ t('jobs.preflight.title') }}</h3>
          <button
            class="btn btn-secondary"
            :disabled="!selectedVmId || preflightRunning"
            @click="runPreflight"
          >
            {{ preflightRunning ? t('jobs.preflight.running') : t('jobs.preflight.run') }}
          </button>
        </div>

        <div v-if="preflightError" class="error">{{ preflightError }}</div>

        <div v-if="preflightResult" class="preflight-result" :class="{ passed: preflightPassed, failed: !preflightPassed }">
          <p class="preflight-summary">
            {{ preflightPassed ? t('jobs.preflight.allPassed') : t('jobs.preflight.hasFailures') }}
          </p>
          <ul class="preflight-items">
            <li v-for="item in preflightResult.items" :key="item.name" :class="{ ok: item.isPassed, bad: !item.isPassed }">
              <span class="item-status">{{ item.isPassed ? '✓' : '✗' }}</span>
              <div>
                <strong>{{ t(`jobs.preflight.items.${item.name}`, item.name) }}</strong>
                <div class="item-message">{{ item.message }}</div>
                <div v-if="item.details" class="item-details">{{ item.details }}</div>
              </div>
            </li>
          </ul>
        </div>
      </div>
    </div>

    <!-- Step 5: Review -->
    <div v-if="currentStep === 5" class="step-panel">
      <h2>{{ t('jobs.reviewSubmit') }}</h2>
      <table class="review-table">
        <tr><td>{{ t('jobs.source') }}</td><td>{{ form.sourceConnectionId.slice(0, 8) }}…</td></tr>
        <tr v-if="selectedVmId"><td>{{ t('jobs.selectVm') }}</td><td>{{ selectedVmId }}</td></tr>
        <tr><td>{{ t('jobs.target') }}</td><td>{{ form.targetConnectionId.slice(0, 8) }}…</td></tr>
        <tr><td>{{ t('connections.type') }}</td><td>{{ form.storageTarget.type }}</td></tr>
        <tr><td>{{ t('connections.host') }}</td><td>{{ form.storageTarget.endpoint }}</td></tr>
        <tr><td>Bucket / Path</td><td>{{ form.storageTarget.bucketOrPath }}</td></tr>
        <tr><td>{{ t('jobs.strategy') }}</td><td>{{ t(`jobs.strategies.${form.strategy}`) }}</td></tr>
        <tr><td>{{ t('jobs.diskFormat') }}</td><td>{{ form.options.targetDiskFormat }}</td></tr>
        <tr><td>{{ t('jobs.verifyChecksum') }}</td><td>{{ form.options.verifyChecksum ? t('common.enabled') : t('common.disabled') }}</td></tr>
        <tr><td>{{ t('jobs.deleteSourceAfter') }}</td><td>{{ form.options.deleteSourceAfter ? t('common.enabled') : t('common.disabled') }}</td></tr>
        <tr><td>{{ t('jobs.maxRetries') }}</td><td>{{ form.options.maxRetries }}</td></tr>
        <tr v-if="isHyperVSource">
          <td>{{ t('jobs.preflight.title') }}</td>
          <td>{{ preflightPassed ? t('jobs.preflight.allPassed') : t('jobs.preflight.required') }}</td>
        </tr>
      </table>
      <div v-if="submitError" class="error">{{ submitError }}</div>
    </div>

    <div class="step-actions">
      <button v-if="currentStep > 1" class="btn btn-secondary" @click="prev">{{ t('jobs.prev') }}</button>
      <button
        v-if="currentStep < totalSteps"
        class="btn btn-primary"
        :disabled="!canNext"
        @click="next"
      >
        {{ t('jobs.next') }}
      </button>
      <button
        v-if="currentStep === totalSteps"
        class="btn btn-primary"
        :disabled="submitting || !canNext"
        @click="submit"
      >
        {{ submitting ? t('jobs.submitting') : t('jobs.createJob') }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.new-job { max-width: 720px; }
h1 { margin-bottom: 20px; }
h2 { margin-bottom: 16px; }
h3 { margin: 16px 0 8px; font-size: 1rem; }
.step-indicator { display: flex; gap: 12px; margin-bottom: 24px; }
.step-dot { width: 32px; height: 32px; border-radius: 50%; background: var(--border-color); color: var(--text-secondary); display: flex; align-items: center; justify-content: center; font-weight: 600; font-size: 0.85rem; }
.step-dot.active { background: #3b82f6; color: white; }
.step-dot.done { background: #22c55e; color: white; }
.step-panel { background: var(--bg-elevated); border-radius: 8px; padding: 24px; box-shadow: 0 1px 3px rgba(0,0,0,.1); margin-bottom: 16px; }
.form-label { display: block; margin-bottom: 12px; font-weight: 500; }
.checkbox-label { display: flex; align-items: center; gap: 8px; }
.input { display: block; width: 100%; padding: 8px 12px; border: 1px solid #d1d5db; border-radius: 6px; margin-top: 4px; font-size: 0.95rem; }
.review-table { width: 100%; border-collapse: collapse; }
.review-table td { padding: 8px 0; border-bottom: 1px solid var(--border-color); }
.review-table td:first-child { font-weight: 500; width: 40%; color: var(--text-secondary); }
.step-actions { display: flex; gap: 12px; justify-content: flex-end; }
.btn { padding: 10px 20px; border: none; border-radius: 6px; font-size: 0.95rem; cursor: pointer; font-weight: 500; }
.btn-primary { background: #3b82f6; color: white; }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
.btn-secondary { background: var(--border-color); color: #374151; }
.btn-secondary:disabled { opacity: 0.5; cursor: not-allowed; }
.error { background: #fef2f2; color: #b91c1c; padding: 12px; border-radius: 6px; margin-top: 12px; }
.muted { color: var(--text-secondary); margin-top: 8px; }
.hint { color: var(--text-secondary); font-size: 0.85rem; margin-top: 8px; }
.vm-section { margin-top: 16px; }
.preflight-panel { margin-top: 20px; padding-top: 16px; border-top: 1px solid var(--border-color); }
.preflight-header { display: flex; justify-content: space-between; align-items: center; gap: 12px; margin-bottom: 12px; }
.preflight-result { border-radius: 8px; padding: 12px 14px; }
.preflight-result.passed { background: #f0fdf4; border: 1px solid #86efac; }
.preflight-result.failed { background: #fef2f2; border: 1px solid #fecaca; }
.preflight-summary { margin: 0 0 10px; font-weight: 600; }
.preflight-items { list-style: none; padding: 0; margin: 0; display: grid; gap: 10px; }
.preflight-items li { display: flex; gap: 10px; align-items: flex-start; }
.item-status { width: 20px; font-weight: 700; }
.preflight-items li.ok .item-status { color: #16a34a; }
.preflight-items li.bad .item-status { color: #dc2626; }
.item-message { color: var(--text-secondary); font-size: 0.9rem; }
.item-details { color: var(--text-secondary); font-size: 0.8rem; margin-top: 2px; }
</style>
