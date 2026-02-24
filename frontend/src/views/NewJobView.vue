<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useConnectionsStore } from '@/stores/connections'
import { jobsApi } from '@/api/jobs'
import type { CreateJobRequest, ArtifactFormat } from '@/types'

const router = useRouter()
const connectionsStore = useConnectionsStore()

const currentStep = ref(1)
const totalSteps = 5
const submitting = ref(false)
const submitError = ref<string | null>(null)

const form = ref<CreateJobRequest>({
  sourceConnectionId: '',
  targetConnectionId: '',
  storageTarget: { type: 'S3', endpoint: '', bucketOrPath: '', region: '' },
  strategy: 'FullCopy',
  options: { targetDiskFormat: 'Qcow2', deleteSourceAfter: false, verifyChecksum: true, maxRetries: 3 },
})

const canNext = computed(() => {
  switch (currentStep.value) {
    case 1: return form.value.sourceConnectionId !== ''
    case 2: return form.value.targetConnectionId !== ''
    case 3: return form.value.storageTarget.endpoint !== '' && form.value.storageTarget.bucketOrPath !== ''
    case 4: return true
    case 5: return true
    default: return false
  }
})

const next = () => { if (currentStep.value < totalSteps) currentStep.value++ }
const prev = () => { if (currentStep.value > 1) currentStep.value-- }

const submit = async () => {
  submitting.value = true
  submitError.value = null
  try {
    const { data } = await jobsApi.create(form.value)
    router.push(`/jobs/${data.id}`)
  } catch (e) {
    submitError.value = e instanceof Error ? e.message : '建立任務失敗'
  } finally {
    submitting.value = false
  }
}

const diskFormats: ArtifactFormat[] = ['Vmdk', 'Qcow2', 'Raw']

onMounted(() => connectionsStore.fetchConnections())
</script>

<template>
  <div class="new-job">
    <h1>新增遷移任務</h1>

    <div class="step-indicator">
      <div v-for="s in totalSteps" :key="s" :class="['step-dot', { active: s === currentStep, done: s < currentStep }]">
        {{ s }}
      </div>
    </div>

    <!-- Step 1: Source -->
    <div v-if="currentStep === 1" class="step-panel">
      <h2>選擇來源連線</h2>
      <select v-model="form.sourceConnectionId" class="input">
        <option value="" disabled>請選擇來源…</option>
        <option v-for="c in connectionsStore.connections" :key="c.id" :value="c.id">
          {{ c.name }} ({{ c.type }}) — {{ c.endpoint }}
        </option>
      </select>
    </div>

    <!-- Step 2: Target -->
    <div v-if="currentStep === 2" class="step-panel">
      <h2>選擇目標連線</h2>
      <select v-model="form.targetConnectionId" class="input">
        <option value="" disabled>請選擇目標…</option>
        <option v-for="c in connectionsStore.connections" :key="c.id" :value="c.id">
          {{ c.name }} ({{ c.type }}) — {{ c.endpoint }}
        </option>
      </select>
    </div>

    <!-- Step 3: Storage -->
    <div v-if="currentStep === 3" class="step-panel">
      <h2>儲存設定</h2>
      <label class="form-label">儲存類型
        <select v-model="form.storageTarget.type" class="input">
          <option value="S3">S3</option>
          <option value="NFS">NFS</option>
          <option value="Local">Local</option>
        </select>
      </label>
      <label class="form-label">端點
        <input v-model="form.storageTarget.endpoint" class="input" placeholder="https://s3.example.com" />
      </label>
      <label class="form-label">Bucket / 路徑
        <input v-model="form.storageTarget.bucketOrPath" class="input" placeholder="my-bucket" />
      </label>
      <label class="form-label">區域（選填）
        <input v-model="form.storageTarget.region" class="input" placeholder="us-east-1" />
      </label>
    </div>

    <!-- Step 4: Options -->
    <div v-if="currentStep === 4" class="step-panel">
      <h2>遷移選項</h2>
      <label class="form-label">策略
        <select v-model="form.strategy" class="input">
          <option value="FullCopy">完整複製</option>
          <option value="Incremental">增量</option>
        </select>
      </label>
      <label class="form-label">目標磁碟格式
        <select v-model="form.options.targetDiskFormat" class="input">
          <option v-for="f in diskFormats" :key="f" :value="f">{{ f }}</option>
        </select>
      </label>
      <label class="form-label">
        <input type="checkbox" v-model="form.options.verifyChecksum" /> 驗證校驗碼
      </label>
      <label class="form-label">
        <input type="checkbox" v-model="form.options.deleteSourceAfter" /> 遷移後刪除來源
      </label>
      <label class="form-label">最大重試次數
        <input type="number" v-model.number="form.options.maxRetries" class="input" min="0" max="10" />
      </label>
    </div>

    <!-- Step 5: Review -->
    <div v-if="currentStep === 5" class="step-panel">
      <h2>確認並送出</h2>
      <table class="review-table">
        <tr><td>來源連線</td><td>{{ form.sourceConnectionId.slice(0, 8) }}…</td></tr>
        <tr><td>目標連線</td><td>{{ form.targetConnectionId.slice(0, 8) }}…</td></tr>
        <tr><td>儲存類型</td><td>{{ form.storageTarget.type }}</td></tr>
        <tr><td>端點</td><td>{{ form.storageTarget.endpoint }}</td></tr>
        <tr><td>Bucket / 路徑</td><td>{{ form.storageTarget.bucketOrPath }}</td></tr>
        <tr><td>策略</td><td>{{ form.strategy }}</td></tr>
        <tr><td>磁碟格式</td><td>{{ form.options.targetDiskFormat }}</td></tr>
        <tr><td>驗證校驗碼</td><td>{{ form.options.verifyChecksum ? '是' : '否' }}</td></tr>
        <tr><td>刪除來源</td><td>{{ form.options.deleteSourceAfter ? '是' : '否' }}</td></tr>
        <tr><td>最大重試</td><td>{{ form.options.maxRetries }}</td></tr>
      </table>
      <div v-if="submitError" class="error">{{ submitError }}</div>
    </div>

    <div class="step-actions">
      <button v-if="currentStep > 1" class="btn btn-secondary" @click="prev">上一步</button>
      <button v-if="currentStep < totalSteps" class="btn btn-primary" :disabled="!canNext" @click="next">下一步</button>
      <button v-if="currentStep === totalSteps" class="btn btn-primary" :disabled="submitting" @click="submit">
        {{ submitting ? '送出中…' : '建立任務' }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.new-job { max-width: 640px; }
h1 { margin-bottom: 20px; }
h2 { margin-bottom: 16px; }
.step-indicator { display: flex; gap: 12px; margin-bottom: 24px; }
.step-dot { width: 32px; height: 32px; border-radius: 50%; background: #e5e7eb; color: #666; display: flex; align-items: center; justify-content: center; font-weight: 600; font-size: 0.85rem; }
.step-dot.active { background: #3b82f6; color: white; }
.step-dot.done { background: #22c55e; color: white; }
.step-panel { background: white; border-radius: 8px; padding: 24px; box-shadow: 0 1px 3px rgba(0,0,0,.1); margin-bottom: 16px; }
.form-label { display: block; margin-bottom: 12px; font-weight: 500; }
.input { display: block; width: 100%; padding: 8px 12px; border: 1px solid #d1d5db; border-radius: 6px; margin-top: 4px; font-size: 0.95rem; }
.review-table { width: 100%; border-collapse: collapse; }
.review-table td { padding: 8px 0; border-bottom: 1px solid #f3f4f6; }
.review-table td:first-child { font-weight: 500; width: 40%; color: #666; }
.step-actions { display: flex; gap: 12px; justify-content: flex-end; }
.btn { padding: 10px 20px; border: none; border-radius: 6px; font-size: 0.95rem; cursor: pointer; font-weight: 500; }
.btn-primary { background: #3b82f6; color: white; }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
.btn-secondary { background: #e5e7eb; color: #374151; }
.error { background: #fef2f2; color: #b91c1c; padding: 12px; border-radius: 6px; margin-top: 12px; }
</style>
