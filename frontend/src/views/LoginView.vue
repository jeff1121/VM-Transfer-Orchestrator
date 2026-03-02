<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '@/api/client'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const userName = ref('')
const password = ref('')
const errorMessage = ref('')
const loading = ref(false)

// 處理登入表單提交
async function handleLogin() {
  errorMessage.value = ''
  if (!userName.value || !password.value) {
    errorMessage.value = '請輸入使用者名稱與密碼'
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
    errorMessage.value = error.response?.data?.message || '登入失敗，請重試'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-container">
    <div class="login-card">
      <h1>VMTO</h1>
      <p class="subtitle">VM Transfer Orchestrator</p>
      <form @submit.prevent="handleLogin">
        <div class="field">
          <label for="username">使用者名稱</label>
          <input
            id="username"
            v-model="userName"
            type="text"
            placeholder="請輸入使用者名稱"
            autocomplete="username"
          />
        </div>
        <div class="field">
          <label for="password">密碼</label>
          <input
            id="password"
            v-model="password"
            type="password"
            placeholder="請輸入密碼"
            autocomplete="current-password"
          />
        </div>
        <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
        <button type="submit" :disabled="loading">
          {{ loading ? '登入中...' : '登入' }}
        </button>
      </form>
    </div>
  </div>
</template>

<style scoped>
.login-container {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  background: #1a1a2e;
}
.login-card {
  background: white;
  padding: 40px;
  border-radius: 8px;
  width: 360px;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.2);
}
.login-card h1 {
  text-align: center;
  color: #1a1a2e;
  margin-bottom: 4px;
}
.subtitle {
  text-align: center;
  color: #888;
  margin-bottom: 24px;
  font-size: 0.9rem;
}
.field {
  margin-bottom: 16px;
}
.field label {
  display: block;
  margin-bottom: 6px;
  font-size: 0.9rem;
  color: #333;
}
.field input {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 1rem;
  box-sizing: border-box;
}
.field input:focus {
  outline: none;
  border-color: #1a1a2e;
}
.error {
  color: #e74c3c;
  font-size: 0.85rem;
  margin-bottom: 12px;
}
button {
  width: 100%;
  padding: 12px;
  background: #1a1a2e;
  color: white;
  border: none;
  border-radius: 4px;
  font-size: 1rem;
  cursor: pointer;
  transition: background 0.2s;
}
button:hover:not(:disabled) {
  background: #16213e;
}
button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>
