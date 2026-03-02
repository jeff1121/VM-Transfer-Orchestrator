<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'

const { t, locale } = useI18n()
const appVersion = __APP_VERSION__
const licenseKey = ref('')
const activating = ref(false)
const activationResult = ref<string | null>(null)
const currentLocale = ref(locale.value)

// 切換語言並儲存至 localStorage
const changeLocale = () => {
  locale.value = currentLocale.value
  localStorage.setItem('locale', currentLocale.value)
}

const activateLicense = async () => {
  activating.value = true
  activationResult.value = null
  try {
    // Placeholder — will call API when endpoint is ready
    await new Promise(resolve => setTimeout(resolve, 1000))
    activationResult.value = '授權啟用成功（模擬）'
    licenseKey.value = ''
  } catch {
    activationResult.value = '啟用失敗'
  } finally {
    activating.value = false
  }
}
</script>

<template>
  <div class="settings-page">
    <h1>{{ t('settings.title') }}</h1>

    <section class="panel">
      <h2>{{ t('settings.language') }}</h2>
      <div class="setting-item">
        <label>{{ t('settings.language') }}</label>
        <select v-model="currentLocale" class="input" @change="changeLocale">
          <option value="zh-TW">繁體中文</option>
          <option value="en-US">English</option>
          <option value="zh-CN">简体中文</option>
        </select>
      </div>
    </section>

    <section class="panel">
      <h2>授權資訊</h2>
      <div class="info-row"><span class="label">產品</span><span>VM Transfer Orchestrator</span></div>
      <div class="info-row"><span class="label">{{ t('settings.version') }}</span><span>{{ appVersion }}</span></div>
      <div class="info-row"><span class="label">授權狀態</span><span class="badge-default">未啟用</span></div>
    </section>

    <section class="panel">
      <h2>啟用授權</h2>
      <label class="form-label">授權金鑰
        <input v-model="licenseKey" class="input" placeholder="XXXX-XXXX-XXXX-XXXX" />
      </label>
      <button class="btn btn-primary" :disabled="!licenseKey || activating" @click="activateLicense">
        {{ activating ? '啟用中…' : '啟用' }}
      </button>
      <div v-if="activationResult" class="result">{{ activationResult }}</div>
    </section>

    <section class="panel">
      <h2>一般設定</h2>
      <p class="placeholder-text">更多設定項目將在未來版本中提供。</p>
    </section>
  </div>
</template>

<style scoped>
.settings-page { max-width: 640px; }
h1 { margin-bottom: 20px; }
h2 { margin-bottom: 12px; }
.panel { background: white; border-radius: 8px; padding: 24px; box-shadow: 0 1px 3px rgba(0,0,0,.1); margin-bottom: 16px; }
.info-row { display: flex; gap: 12px; padding: 8px 0; border-bottom: 1px solid #f3f4f6; }
.info-row:last-child { border-bottom: none; }
.label { font-weight: 500; color: #666; min-width: 100px; }
.badge-default { background: #f3f4f6; color: #374151; padding: 2px 8px; border-radius: 4px; font-size: 0.85rem; }
.form-label { display: block; margin-bottom: 12px; font-weight: 500; }
.input { display: block; width: 100%; padding: 8px 12px; border: 1px solid #d1d5db; border-radius: 6px; margin-top: 4px; font-size: 0.95rem; }
.btn { padding: 8px 16px; border: none; border-radius: 6px; cursor: pointer; font-weight: 500; }
.btn-primary { background: #3b82f6; color: white; }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
.result { margin-top: 12px; padding: 12px; background: #f0fdf4; color: #166534; border-radius: 6px; }
.placeholder-text { color: #999; }
.setting-item { display: flex; align-items: center; gap: 12px; }
.setting-item label { font-weight: 500; color: #666; min-width: 60px; }
.setting-item .input { width: auto; min-width: 200px; margin-top: 0; }
</style>
