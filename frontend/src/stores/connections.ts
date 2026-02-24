import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Connection, CreateConnectionRequest } from '@/types'
import { connectionsApi } from '@/api/connections'

export const useConnectionsStore = defineStore('connections', () => {
  const connections = ref<Connection[]>([])
  const loading = ref(false)

  const fetchConnections = async () => {
    loading.value = true
    try {
      const { data } = await connectionsApi.list()
      connections.value = data
    } finally {
      loading.value = false
    }
  }

  const createConnection = async (request: CreateConnectionRequest) => {
    const { data } = await connectionsApi.create(request)
    connections.value.push(data)
    return data
  }

  const deleteConnection = async (id: string) => {
    await connectionsApi.delete(id)
    connections.value = connections.value.filter(c => c.id !== id)
  }

  return { connections, loading, fetchConnections, createConnection, deleteConnection }
})
