<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { api } from '@/api/client'
import { useAuthStore } from '@/stores/auth'

const { t } = useI18n()
const router = useRouter()
const authStore = useAuthStore()

const userName = ref('')
const password = ref('')
const errorMessage = ref('')
const loading = ref(false)

async function handleLogin() {
  errorMessage.value = ''
  if (!userName.value || !password.value) {
    errorMessage.value = t('auth.loginFailed')
    return
  }

  loading.value = true
  try {
    const { data } = await api.post('/auth/login', {
      userName: userName.value,
      password: password.value,
    })
    authStore.login(data.token, data.role, userName.value)
    router.push('/')
  } catch (err: unknown) {
    const error = err as { response?: { data?: { message?: string } } }
    errorMessage.value = error.response?.data?.message || t('auth.loginFailed')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <!-- Ambient Background Lighting Orbs -->
    <div class="ambient-orb orb-1"></div>
    <div class="ambient-orb orb-2"></div>

    <div class="glass-card login-card">
      <div class="brand-header">
        <div class="brand-badge">
          <img src="/logo.png" alt="VMTO Logo" class="brand-logo-img" />
        </div>
        <h1 class="brand-title">VMTO</h1>
        <p class="brand-subtitle">Transfer Orchestrator</p>
      </div>

      <form class="login-form" @submit.prevent="handleLogin">
        <div class="form-group">
          <label for="username" class="form-label">{{ t('auth.username') }}</label>
          <input
            id="username"
            v-model="userName"
            type="text"
            class="neu-input"
            :placeholder="t('auth.username')"
            autocomplete="username"
          />
        </div>

        <div class="form-group">
          <label for="password" class="form-label">{{ t('auth.password') }}</label>
          <input
            id="password"
            v-model="password"
            type="password"
            class="neu-input"
            :placeholder="t('auth.password')"
            autocomplete="current-password"
          />
        </div>

        <div v-if="errorMessage" class="neu-banner banner-error">{{ errorMessage }}</div>

        <button type="submit" class="neu-btn btn-primary full-width" :disabled="loading">
          <span>{{ loading ? '⏳ ' + t('auth.loggingIn') : '🚀 ' + t('auth.login') }}</span>
        </button>
      </form>
    </div>
  </div>
</template>

<style scoped>
.login-page {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  position: relative;
  overflow: hidden;
  background: var(--bg-gradient);
  padding: 20px;
}

.ambient-orb {
  position: absolute;
  border-radius: 50%;
  filter: blur(80px);
  opacity: 0.6;
  pointer-events: none;
}

.orb-1 {
  width: 400px;
  height: 400px;
  background: var(--primary-glow);
  top: 10%;
  left: 15%;
}

.orb-2 {
  width: 350px;
  height: 350px;
  background: rgba(236, 72, 153, 0.25);
  bottom: 10%;
  right: 15%;
}

.glass-card {
  background: var(--bg-surface);
  backdrop-filter: var(--glass-blur);
  border: var(--glass-border);
  border-radius: 24px;
  box-shadow: var(--neu-shadow);
  padding: 44px 36px;
  width: 100%;
  max-width: 420px;
  position: relative;
  z-index: 10;
}

.brand-header {
  text-align: center;
  margin-bottom: 32px;
}

.brand-badge {
  width: 72px;
  height: 72px;
  border-radius: 20px;
  background: transparent;
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0 auto 16px;
  filter: drop-shadow(0 8px 24px var(--primary-glow));
}

.brand-logo-img {
  width: 100%;
  height: 100%;
  object-fit: contain;
}

.brand-title {
  font-size: 1.8rem;
  font-weight: 800;
  letter-spacing: -0.5px;
  background: var(--primary-gradient);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.brand-subtitle {
  font-size: 0.85rem;
  color: var(--text-muted);
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.login-form {
  display: flex;
  flex-direction: column;
  gap: 20px;
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
  padding: 14px 18px;
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  border-radius: 14px;
  box-shadow: var(--neu-inset);
  color: var(--text-primary);
  font-size: 1rem;
  outline: none;
  transition: all 0.2s ease;
}

.neu-input:focus {
  border-color: var(--primary);
  box-shadow: 0 0 0 3px var(--primary-glow), var(--neu-inset);
}

.neu-btn {
  padding: 14px;
  border-radius: 14px;
  font-size: 1rem;
  font-weight: 700;
  border: none;
  cursor: pointer;
  box-shadow: 0 6px 20px var(--primary-glow);
  transition: all 0.25s ease;
}

.btn-primary { background: var(--primary-gradient); color: white; }
.btn-primary:hover:not(:disabled) { transform: translateY(-2px); box-shadow: 0 8px 24px var(--primary-glow); }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }

.full-width {
  width: 100%;
}
</style>
