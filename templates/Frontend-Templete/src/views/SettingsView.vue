<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useTheme, type ThemeMode } from '@/composables/useTheme'
import NeuSelect, { type SelectOption } from '@/components/common/NeuSelect.vue'

const { t, locale } = useI18n()
const { setThemeMode, resolvedTheme } = useTheme()
const appVersion = __APP_VERSION__
const currentLocale = ref(locale.value)

const languageOptions: SelectOption[] = [
  { value: 'zh-TW', label: '繁體中文 (Traditional Chinese)', icon: '🇹🇼' },
  { value: 'en-US', label: 'English (US)', icon: '🇺🇸' },
]

const changeLocale = () => {
  locale.value = currentLocale.value
  localStorage.setItem('locale', currentLocale.value)
}

const changeTheme = (newMode: ThemeMode) => {
  setThemeMode(newMode)
}
</script>

<template>
  <div class="settings-container">
    <div class="page-header">
      <div>
        <h1 class="page-title">⚙️ {{ t('settings.title') }}</h1>
        <p class="page-subtitle">Configure system localization, visual themes, and enterprise preferences.</p>
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

    <!-- System Info Panel -->
    <div class="glass-card panel">
      <div class="panel-header">
        <span class="panel-icon">ℹ️</span>
        <h2 class="panel-title">System & Environment</h2>
      </div>

      <div class="info-grid">
        <div class="info-cell">
          <span class="info-label">Product Suite</span>
          <span class="info-val">Hyper-Modern Dashboard Template</span>
        </div>
        <div class="info-cell">
          <span class="info-label">{{ t('settings.version') }}</span>
          <span class="info-val highlight">v{{ appVersion }}</span>
        </div>
        <div class="info-cell">
          <span class="info-label">Framework Stack</span>
          <span class="info-val">Vue 3 + Vite + Pinia</span>
        </div>
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
  -webkit-backdrop-filter: var(--glass-blur);
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

.panel-icon {
  font-size: 1.3rem;
}

.panel-title {
  font-size: 1.15rem;
  font-weight: 800;
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

/* Info Grid */
.info-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
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

.info-label {
  font-size: 0.75rem;
  color: var(--text-muted);
  font-weight: 700;
  text-transform: uppercase;
}

.info-val {
  font-size: 0.95rem;
  font-weight: 700;
}

.info-val.highlight {
  color: var(--primary);
}

@media (max-width: 768px) {
  .setting-row { flex-direction: column; align-items: stretch; }
  .info-grid { grid-template-columns: 1fr; }
}
</style>
