import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

export type SignalRStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting' | 'manual-refresh-required'

export const useSignalRStore = defineStore('signalr', () => {
  const status = ref<SignalRStatus>('disconnected')
  const retryAttempt = ref(0)
  const reconnectInSeconds = ref(0)
  const latencyMs = ref<number | null>(null)
  const lastError = ref<string | null>(null)

  const connected = computed(() => status.value === 'connected')
  const manualRefreshRequired = computed(() => status.value === 'manual-refresh-required')
  const showReconnectBanner = computed(() => status.value === 'reconnecting')

  const setConnecting = () => {
    status.value = 'connecting'
    lastError.value = null
  }

  const setConnected = () => {
    status.value = 'connected'
    retryAttempt.value = 0
    reconnectInSeconds.value = 0
    lastError.value = null
  }

  const setDisconnected = (error?: unknown) => {
    status.value = 'disconnected'
    reconnectInSeconds.value = 0
    if (error instanceof Error) lastError.value = error.message
  }

  const setReconnecting = (attempt: number, seconds: number) => {
    status.value = 'reconnecting'
    retryAttempt.value = attempt
    reconnectInSeconds.value = seconds
  }

  const setManualRefreshRequired = (error?: unknown) => {
    status.value = 'manual-refresh-required'
    if (error instanceof Error) lastError.value = error.message
  }

  const setReconnectCountdown = (seconds: number) => {
    reconnectInSeconds.value = Math.max(0, seconds)
  }

  const setLatency = (ms: number | null) => {
    latencyMs.value = ms
  }

  return {
    status,
    retryAttempt,
    reconnectInSeconds,
    latencyMs,
    lastError,
    connected,
    manualRefreshRequired,
    showReconnectBanner,
    setConnecting,
    setConnected,
    setDisconnected,
    setReconnecting,
    setManualRefreshRequired,
    setReconnectCountdown,
    setLatency,
  }
})
