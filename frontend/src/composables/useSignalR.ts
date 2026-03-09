import { computed, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'
import type { JobProgress } from '@/types'
import { useSignalRStore } from '@/stores/signalr'
import { useNotificationsStore } from '@/stores/notifications'

const reconnectDelaysMs = [0, 2000, 5000, 10000, 30000]
const maxReconnectAttempts = reconnectDelaysMs.length

let sharedConnection: signalR.HubConnection | null = null
let intentionalStop = false
let reconnectCountdownTimer: ReturnType<typeof setInterval> | null = null
let reconnectDeadline = 0
let latencyProbeTimer: ReturnType<typeof setInterval> | null = null
const notifiedJobStatus = new Map<string, string>()
const notifiedFailedStep = new Set<string>()

const clearReconnectCountdown = () => {
  if (reconnectCountdownTimer) {
    clearInterval(reconnectCountdownTimer)
    reconnectCountdownTimer = null
  }
  reconnectDeadline = 0
}

const clearLatencyProbe = () => {
  if (latencyProbeTimer) {
    clearInterval(latencyProbeTimer)
    latencyProbeTimer = null
  }
}

const startReconnectCountdown = (delayMs: number, store: ReturnType<typeof useSignalRStore>) => {
  clearReconnectCountdown()
  if (delayMs <= 0) {
    store.setReconnectCountdown(0)
    return
  }

  reconnectDeadline = Date.now() + delayMs
  store.setReconnectCountdown(Math.ceil(delayMs / 1000))
  reconnectCountdownTimer = setInterval(() => {
    const seconds = Math.ceil((reconnectDeadline - Date.now()) / 1000)
    store.setReconnectCountdown(seconds)
    if (seconds <= 0) {
      clearReconnectCountdown()
    }
  }, 250)
}

const probeLatency = async (store: ReturnType<typeof useSignalRStore>) => {
  const started = performance.now()
  try {
    const response = await fetch('/health/live', { cache: 'no-store' })
    if (!response.ok) {
      store.setLatency(null)
      return
    }

    const elapsed = Math.round(performance.now() - started)
    store.setLatency(elapsed)
  } catch {
    store.setLatency(null)
  }
}

const startLatencyProbe = (store: ReturnType<typeof useSignalRStore>) => {
  clearLatencyProbe()
  void probeLatency(store)
  latencyProbeTimer = setInterval(() => { void probeLatency(store) }, 15000)
}

export function useSignalR() {
  const store = useSignalRStore()
  const notifications = useNotificationsStore()
  const connected = computed(() => store.connected)

  const ensureConnection = () => {
    if (sharedConnection) {
      return sharedConnection
    }

    const conn = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/migration')
      .withAutomaticReconnect(reconnectDelaysMs)
      .build()

    conn.on('JobProgress', (progress: JobProgress) => {
      const lastStatus = notifiedJobStatus.get(progress.jobId)
      if (lastStatus === progress.status) {
        return
      }

      notifiedJobStatus.set(progress.jobId, progress.status)
      if (progress.status === 'Succeeded') {
        notifications.push({
          category: 'job-completed',
          type: 'success',
          title: '任務完成',
          message: `任務 ${progress.jobId.slice(0, 8)} 已完成。`,
        })
      } else if (progress.status === 'Failed') {
        notifications.push({
          category: 'job-failed',
          type: 'error',
          title: '任務失敗',
          message: `任務 ${progress.jobId.slice(0, 8)} 執行失敗。`,
        })
      }
    })

    conn.on('StepProgress', (jobId: string, stepId: string, _progress: number, status: string) => {
      if (status !== 'Failed') {
        return
      }

      const key = `${jobId}:${stepId}`
      if (notifiedFailedStep.has(key)) {
        return
      }

      notifiedFailedStep.add(key)
      notifications.push({
        category: 'step-failed',
        type: 'warning',
        title: '步驟失敗',
        message: `任務 ${jobId.slice(0, 8)} 步驟 ${stepId.slice(0, 8)} 失敗。`,
      })
    })

    conn.onreconnecting((error) => {
      const nextAttempt = store.retryAttempt + 1
      if (nextAttempt > maxReconnectAttempts) {
        store.setManualRefreshRequired(error)
        notifications.push({
          category: 'system',
          type: 'warning',
          title: '系統公告',
          message: '即時連線重試失敗，請手動重新整理頁面。',
        })
        clearReconnectCountdown()
        clearLatencyProbe()
        return
      }

      const delayMs = reconnectDelaysMs[Math.min(nextAttempt, maxReconnectAttempts) - 1] ?? 0
      store.setReconnecting(nextAttempt, Math.ceil(delayMs / 1000))
      startReconnectCountdown(delayMs, store)
      clearLatencyProbe()
    })

    conn.onreconnected(() => {
      clearReconnectCountdown()
      store.setConnected()
      startLatencyProbe(store)
    })

    conn.onclose((error) => {
      clearReconnectCountdown()
      clearLatencyProbe()
      if (intentionalStop) {
        store.setDisconnected()
        return
      }

      if (store.retryAttempt >= maxReconnectAttempts) {
        store.setManualRefreshRequired(error)
        notifications.push({
          category: 'system',
          type: 'warning',
          title: '系統公告',
          message: '即時連線已中斷，請手動重新整理頁面。',
        })
        return
      }

      store.setDisconnected(error)
    })

    sharedConnection = conn
    return conn
  }

  const connect = async () => {
    const conn = ensureConnection()

    if (conn.state === signalR.HubConnectionState.Connected) {
      store.setConnected()
      startLatencyProbe(store)
      return
    }

    if (conn.state === signalR.HubConnectionState.Connecting || conn.state === signalR.HubConnectionState.Reconnecting) {
      return
    }

    store.setConnecting()
    try {
      intentionalStop = false
      await conn.start()
      store.setConnected()
      startLatencyProbe(store)
    } catch (error) {
      store.setDisconnected(error)
      throw error
    }
  }

  const onJobProgress = (callback: (progress: JobProgress) => void) => {
    sharedConnection?.on('JobProgress', callback)
    onUnmounted(() => { sharedConnection?.off('JobProgress', callback) })
  }

  const onStepProgress = (callback: (jobId: string, stepId: string, progress: number, status: string) => void) => {
    sharedConnection?.on('StepProgress', callback)
    onUnmounted(() => { sharedConnection?.off('StepProgress', callback) })
  }

  const disconnect = async () => {
    if (!sharedConnection) return
    intentionalStop = true
    clearReconnectCountdown()
    clearLatencyProbe()
    await sharedConnection.stop()
    store.setDisconnected()
  }

  return { connect, disconnect, connected, onJobProgress, onStepProgress }
}
