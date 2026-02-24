import { ref, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'
import type { JobProgress } from '@/types'

export function useSignalR() {
  const connection = ref<signalR.HubConnection | null>(null)
  const connected = ref(false)

  const connect = async () => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/migration')
      .withAutomaticReconnect()
      .build()

    conn.onreconnected(() => { connected.value = true })
    conn.onclose(() => { connected.value = false })

    await conn.start()
    connection.value = conn
    connected.value = true
  }

  const onJobProgress = (callback: (progress: JobProgress) => void) => {
    connection.value?.on('JobProgress', callback)
  }

  const onStepProgress = (callback: (jobId: string, stepId: string, progress: number, status: string) => void) => {
    connection.value?.on('StepProgress', callback)
  }

  onUnmounted(() => {
    connection.value?.stop()
  })

  return { connect, connected, onJobProgress, onStepProgress }
}
