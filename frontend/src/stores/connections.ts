import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Connection, CreateConnectionRequest } from '@/types'
import { connectionsApi } from '@/api/connections'

interface PaginatedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export const useConnectionsStore = defineStore('connections', () => {
  const connections = ref<Connection[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchConnections = async () => {
    loading.value = true
    error.value = null
    try {
      const res = await connectionsApi.list()
      const data = res.data as unknown as (Connection[] | PaginatedResponse<Connection>)
      if (Array.isArray(data)) {
        connections.value = data
      } else if (data && Array.isArray((data as PaginatedResponse<Connection>).items)) {
        connections.value = (data as PaginatedResponse<Connection>).items
      } else {
        connections.value = []
      }
    } catch (e) {
      connections.value = []
      error.value = e instanceof Error ? e.message : 'Failed to fetch connections'
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
    connections.value = connections.value.filter((c) => c.id !== id)
  }

  return { connections, loading, error, fetchConnections, createConnection, deleteConnection }
})
