<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useTheme, type ThemeMode } from '@/composables/useTheme'
import { api } from '@/api/client'
import NeuSelect, { type SelectOption } from '@/components/common/NeuSelect.vue'

const { t, locale } = useI18n()
const { setThemeMode, resolvedTheme } = useTheme()
const appVersion = __APP_VERSION__
const licenseKey = ref('')
const activating = ref(false)
const activationResult = ref<string | null>(null)
const activationError = ref<string | null>(null)
const currentLocale = ref(locale.value)
const showRenewalForm = ref(false)

const languageOptions: SelectOption[] = [
  { value: 'zh-TW', label: '繁體中文 (Traditional Chinese)', icon: '🇹🇼' },
  { value: 'en-US', label: 'English (US)', icon: '🇺🇸' },
]

interface LicenseInfo {
  id: string
  plan: string
  features: string[]
  maxConcurrentJobs: number
  expiresAt: string
  isValid: boolean
  createdAt: string
}

const activeLicense = ref<LicenseInfo | null>(null)

const maskKey = (key?: string) => {
  if (!key || key.length < 8) return '••••-••••-••••-••••'
  const clean = key.replace(/[-–—\s]/g, '')
  if (clean.length < 16) return '••••-••••-••••-••••'
  return `${clean.slice(0, 4)}-••••-••••-${clean.slice(12, 16)}`
}

const changeLocale = () => {
  locale.value = currentLocale.value
  localStorage.setItem('locale', currentLocale.value)
}

const changeTheme = (newMode: ThemeMode) => {
  setThemeMode(newMode)
}

const fetchLicenseInfo = async () => {
  try {
    const { data } = await api.get<LicenseInfo>('/license')
    activeLicense.value = data
    if (!data || !data.isValid) {
      showRenewalForm.value = true
    } else {
      showRenewalForm.value = false
    }
  } catch {
    activeLicense.value = null
    showRenewalForm.value = true
  }
}

const activateLicense = async () => {
  activating.value = true
  activationResult.value = null
  activationError.value = null
  try {
    await api.post('/license/activate', {
      licenseKey: licenseKey.value,
      bindings: {},
    })
    activationResult.value = '授權啟用成功！'
    licenseKey.value = ''
    showRenewalForm.value = false
    await fetchLicenseInfo()
  } catch (err: unknown) {
    const error = err as { response?: { data?: { detail?: string; message?: string } } }
    activationError.value = error.response?.data?.detail || error.response?.data?.message || '授權啟用失敗，請確認序號是否正確。'
  } finally {
    activating.value = false
  }
}

onMounted(() => {
  fetchLicenseInfo()
})
</script>

<template>
  <div class="settings-container">
    <div class="page-header">
      <div>
        <h1 class="page-title">⚙️ {{ t('settings.title') }}</h1>
        <p class="page-subtitle">Configure system localization, visual themes, and enterprise license activation.</p>
      </div>
    </div>

    <!-- Localization Settings Panel -->
    <div class="glass-card panel">
      <div class="panel-header">
        <span class="panel-icon">🌐</span>
        <h2 class="panel-title">{{ t('settings.language') }}</h2>
      </div>

      <div class="setting-row">
        <label class="setting-label">{{ t('settings.language') }}</label>
        <NeuSelect
          v-model="currentLocale"
          :options="languageOptions"
          @change="changeLocale"
        />
      </div>
    </div>

    <!-- Appearance Settings Panel -->
    <div class="glass-card panel">
      <div class="panel-header">
        <span class="panel-icon">🎨</span>
        <h2 class="panel-title">{{ t('settings.theme') }}</h2>
      </div>

      <div class="theme-segmented-group">
        <button
          :class="['theme-segment-btn', { active: resolvedTheme === 'light' }]"
          @click="changeTheme('light')"
        >
          <span class="btn-icon">☀️</span>
          <span>{{ t('settings.themeLight') }}</span>
        </button>

        <button
          :class="['theme-segment-btn', { active: resolvedTheme === 'dark' }]"
          @click="changeTheme('dark')"
        >
          <span class="btn-icon">🌙</span>
          <span>{{ t('settings.themeDark') }}</span>
        </button>
      </div>
    </div>

    <!-- System & Licensing Overview Panel -->
    <div class="glass-card panel">
      <div class="panel-header-spread">
        <div class="panel-header-left">
          <span class="panel-icon">🛡️</span>
          <h2 class="panel-title">System & Licensing</h2>
        </div>
        <div v-if="activeLicense && activeLicense.isValid" class="license-status-badge active">
          <span class="pulse-dot online"></span>
          <span>商業授權運行中 (Active)</span>
        </div>
        <div v-else class="license-status-badge inactive">
          <span class="pulse-dot offline"></span>
          <span>開發者模式 (Unlicensed)</span>
        </div>
      </div>

      <div class="info-grid">
        <div class="info-cell">
          <span class="info-label">Product Suite</span>
          <span class="info-val">VM Transfer Orchestrator</span>
        </div>
        <div class="info-cell">
          <span class="info-label">{{ t('settings.version') }}</span>
          <span class="info-val highlight">v{{ appVersion }}</span>
        </div>
        <div class="info-cell">
          <span class="info-label">License Tier</span>
          <span v-if="activeLicense && activeLicense.isValid" class="status-pill status-succeeded">
            👑 {{ activeLicense.plan }} Edition
          </span>
          <span v-else class="status-pill status-unlicensed">
            ⚪ Developer Mode
          </span>
        </div>
        <div class="info-cell">
          <span class="info-label">Max Concurrency</span>
          <span class="info-val highlight">
            {{ activeLicense && activeLicense.isValid ? `${activeLicense.maxConcurrentJobs} Jobs` : '2 Jobs (Limited)' }}
          </span>
        </div>
        <div class="info-cell">
          <span class="info-label">Valid Until</span>
          <span class="info-val">
            {{ activeLicense && activeLicense.isValid ? new Date(activeLicense.expiresAt).toLocaleDateString() : 'N/A' }}
          </span>
        </div>
        <div class="info-cell">
          <span class="info-label">Active Key</span>
          <span class="info-val mono-masked">
            {{ activeLicense && activeLicense.isValid ? maskKey(activeLicense.id) : 'Unactivated' }}
          </span>
        </div>
        <div class="info-cell full-width-cell">
          <span class="info-label">Enabled Feature Modules</span>
          <div v-if="activeLicense && activeLicense.isValid && activeLicense.features.length > 0" class="features-tags">
            <span v-for="f in activeLicense.features" :key="f" class="feature-tag">✓ {{ f }}</span>
          </div>
          <div v-else class="features-tags">
            <span class="feature-tag muted">✓ vsphere (basic)</span>
            <span class="feature-tag locked">🔒 hyperv (Commercial Only)</span>
            <span class="feature-tag locked">🔒 incremental-sync (Commercial Only)</span>
          </div>
        </div>
      </div>
    </div>

    <!-- License Activation / Renewal Section (State-Aware) -->
    <div class="glass-card panel">
      <!-- When Active and form closed: Collapsible Banner -->
      <div v-if="activeLicense && activeLicense.isValid && !showRenewalForm" class="active-license-state">
        <div class="state-left">
          <div class="state-icon">✅</div>
          <div class="state-text">
            <h3>商業授權已生效且受保護</h3>
            <p>如需更換主機、續約合約或升級並行任務數，請點擊右側按鈕展開輸入新序號。</p>
          </div>
        </div>
        <button class="neu-btn btn-secondary" @click="showRenewalForm = true">
          <span>🔄 更換 / 續約授權碼</span>
        </button>
      </div>

      <!-- Expandable Key Activation Form -->
      <div v-else class="activation-form-container">
        <div class="panel-header-spread mb-16">
          <div class="panel-header-left">
            <span class="panel-icon">🔑</span>
            <h2 class="panel-title">
              {{ activeLicense && activeLicense.isValid ? '更換或續約商業授權 (16 碼離線序號)' : '啟用商業授權 (16 碼離線序號)' }}
            </h2>
          </div>
          <button v-if="activeLicense && activeLicense.isValid" class="neu-btn btn-sm btn-secondary" @click="showRenewalForm = false">
            <span>{{ t('common.cancel') }}</span>
          </button>
        </div>

        <p class="section-desc">
          請貼入由管理端產出之 16 碼格式序號（例如 <code>4CE3-55S3-7FSA-AX92</code>），系統將直接離線驗證並即時套用。
        </p>

        <div class="activation-form">
          <div class="form-group">
            <label class="form-label">Enterprise Product Key (XXXX-XXXX-XXXX-XXXX)</label>
            <input
              v-model="licenseKey"
              class="neu-input mono-input"
              placeholder="e.g. 4CE3-55S3-7FSA-AX92"
              maxlength="25"
            />
          </div>

          <button
            class="neu-btn btn-primary"
            :disabled="!licenseKey || activating"
            @click="activateLicense"
          >
            <span>{{ activating ? '⏳ 驗證中…' : '⚡ 立即啟用授權' }}</span>
          </button>
        </div>

        <div v-if="activationResult" class="neu-banner banner-success">{{ activationResult }}</div>
        <div v-if="activationError" class="neu-banner banner-error">{{ activationError }}</div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.settings-container {
  display: flex;
  flex-direction: column;
  gap: 24px;
  max-width: 860px;
  margin: 0 auto;
}

.page-header {
  margin-bottom: 4px;
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

.glass-card {
  background: var(--bg-surface);
  backdrop-filter: var(--glass-blur);
  border: var(--glass-border);
  border-radius: 20px;
  box-shadow: var(--neu-shadow);
  padding: 28px;
  position: relative;
  overflow: visible;
}

.panel {
  position: relative;
  z-index: 1;
}

.panel:has(.neu-select-wrapper.is-open) {
  z-index: 30;
}

.panel-header {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 20px;
}

.panel-header-spread {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
  flex-wrap: wrap;
  gap: 12px;
}

.panel-header-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.panel-icon {
  font-size: 1.3rem;
}

.panel-title {
  font-size: 1.15rem;
  font-weight: 800;
}

.section-desc {
  color: var(--text-secondary);
  font-size: 0.9rem;
  margin-bottom: 18px;
  line-height: 1.5;
}

.section-desc code {
  background: var(--bg-surface-elevated);
  padding: 2px 6px;
  border-radius: 4px;
  font-family: monospace;
  color: var(--primary);
  font-weight: 700;
}

.mb-16 {
  margin-bottom: 16px;
}

.setting-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.setting-label {
  font-weight: 700;
  font-size: 0.95rem;
  color: var(--text-secondary);
}

.neu-input {
  padding: 12px 16px;
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  border-radius: 12px;
  box-shadow: var(--neu-inset);
  color: var(--text-primary);
  font-size: 0.95rem;
  outline: none;
  min-width: 260px;
}

.mono-input {
  font-family: monospace;
  font-weight: 700;
  letter-spacing: 1px;
}

.neu-input:focus {
  border-color: var(--primary);
  box-shadow: 0 0 0 3px var(--primary-glow), var(--neu-inset);
}

/* Theme Segmented Control */
.theme-segmented-group {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
  background: var(--bg-surface-elevated);
  padding: 8px;
  border-radius: 14px;
  box-shadow: var(--neu-inset);
}

.theme-segment-btn {
  padding: 14px;
  border-radius: 10px;
  border: none;
  background: transparent;
  color: var(--text-secondary);
  font-weight: 700;
  font-size: 0.9rem;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.theme-segment-btn:hover {
  color: var(--text-primary);
}

.theme-segment-btn.active {
  background: var(--primary-gradient);
  color: white;
  box-shadow: 0 4px 14px var(--primary-glow);
}

/* License Status Badges */
.license-status-badge {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 6px 14px;
  border-radius: 999px;
  font-size: 0.85rem;
  font-weight: 700;
}

.license-status-badge.active {
  background: var(--success-bg);
  color: var(--success);
  border: 1px solid var(--success);
}

.license-status-badge.inactive {
  background: rgba(148, 163, 184, 0.15);
  color: var(--text-secondary);
  border: 1px solid var(--border-color);
}

/* Info Grid */
.info-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
}

.full-width-cell {
  grid-column: span 3;
}

.info-cell {
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  border-radius: 12px;
  padding: 14px 18px;
  box-shadow: var(--neu-shadow-sm);
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.info-label { font-size: 0.75rem; color: var(--text-muted); font-weight: 700; text-transform: uppercase; }
.info-val { font-size: 0.95rem; font-weight: 700; }
.info-val.highlight { color: var(--primary); }
.mono-masked { font-family: monospace; letter-spacing: 0.5px; color: var(--text-secondary); }

.features-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 6px;
}

.feature-tag {
  background: var(--primary-glow);
  color: var(--primary);
  padding: 4px 12px;
  border-radius: 8px;
  font-size: 0.8rem;
  font-weight: 700;
}

.feature-tag.muted {
  background: var(--bg-surface);
  color: var(--text-secondary);
}

.feature-tag.locked {
  background: rgba(245, 158, 11, 0.15);
  color: #d97706;
  border: 1px dashed rgba(245, 158, 11, 0.4);
}

.status-pill {
  display: inline-flex;
  align-items: center;
  padding: 4px 10px;
  border-radius: 999px;
  font-size: 0.75rem;
  font-weight: 800;
  width: fit-content;
}

.status-succeeded { background: var(--success-bg); color: var(--success); }
.status-unlicensed { background: rgba(148, 163, 184, 0.2); color: var(--text-secondary); }

/* Active State Collapsible */
.active-license-state {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  flex-wrap: wrap;
}

.state-left {
  display: flex;
  align-items: center;
  gap: 16px;
  flex: 1;
}

.state-icon {
  width: 48px;
  height: 48px;
  border-radius: 14px;
  background: var(--success-bg);
  border: 1px solid var(--success);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
}

.state-text h3 {
  font-size: 1.05rem;
  font-weight: 800;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.state-text p {
  font-size: 0.85rem;
  color: var(--text-secondary);
  line-height: 1.4;
}

/* Activation Form */
.activation-form {
  display: flex;
  gap: 16px;
  align-items: flex-end;
}

.activation-form .form-group {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-label {
  font-size: 0.85rem;
  font-weight: 700;
  color: var(--text-secondary);
}

.neu-btn {
  padding: 12px 24px;
  border-radius: 12px;
  font-size: 0.9rem;
  font-weight: 700;
  border: none;
  cursor: pointer;
  box-shadow: 0 4px 14px var(--primary-glow);
  display: inline-flex;
  align-items: center;
  gap: 6px;
  white-space: nowrap;
  transition: all 0.2s ease;
}

.btn-primary { background: var(--primary-gradient); color: white; }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
.btn-secondary {
  background: var(--bg-surface-elevated);
  border: var(--glass-border);
  color: var(--text-primary);
  box-shadow: var(--neu-shadow-sm);
}

.btn-secondary:hover {
  background: var(--bg-surface);
}

.btn-sm {
  padding: 6px 14px;
  font-size: 0.8rem;
  border-radius: 8px;
}

.banner-success {
  margin-top: 16px;
  background: var(--success-bg);
  color: var(--success);
  border: 1px solid var(--success);
  padding: 12px 16px;
  border-radius: 10px;
  font-weight: 600;
  font-size: 0.9rem;
}

.banner-error {
  margin-top: 16px;
  background: var(--danger-bg);
  color: var(--danger);
  border: 1px solid var(--danger);
  padding: 12px 16px;
  border-radius: 10px;
  font-weight: 600;
  font-size: 0.9rem;
}

@media (max-width: 768px) {
  .setting-row { flex-direction: column; align-items: stretch; }
  .neu-input { min-width: 100%; }
  .info-grid { grid-template-columns: 1fr; }
  .full-width-cell { grid-column: span 1; }
  .active-license-state { flex-direction: column; align-items: stretch; }
  .activation-form { flex-direction: column; align-items: stretch; }
}
</style>
