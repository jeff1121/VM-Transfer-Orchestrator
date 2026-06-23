import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

export type NotificationType = 'success' | 'error' | 'warning' | 'info'
export type NotificationCategory = 'job-completed' | 'job-failed' | 'step-failed' | 'system'

export interface NotificationItem {
  id: string
  category: NotificationCategory
  type: NotificationType
  title: string
  message: string
  createdAt: string
  read: boolean
}

export interface ToastItem {
  id: string
  type: NotificationType
  message: string
}

const MAX_HISTORY = 200
const TOAST_TTL_MS = 3000

export const useNotificationsStore = defineStore('notifications', () => {
  const items = ref<NotificationItem[]>([])
  const toasts = ref<ToastItem[]>([])
  const unreadCount = computed(() => items.value.filter(item => !item.read).length)

  const push = (payload: Omit<NotificationItem, 'id' | 'createdAt' | 'read'>) => {
    const id = crypto.randomUUID()
    items.value.unshift({
      id,
      createdAt: new Date().toISOString(),
      read: false,
      ...payload,
    })
    if (items.value.length > MAX_HISTORY) {
      items.value.length = MAX_HISTORY
    }

    toasts.value.unshift({
      id,
      type: payload.type,
      message: payload.message,
    })

    setTimeout(() => {
      toasts.value = toasts.value.filter(toast => toast.id !== id)
    }, TOAST_TTL_MS)
  }

  const markRead = (id: string) => {
    const item = items.value.find(entry => entry.id === id)
    if (item) {
      item.read = true
    }
  }

  const markAllRead = () => {
    for (const item of items.value) {
      item.read = true
    }
  }

  return {
    items,
    toasts,
    unreadCount,
    push,
    markRead,
    markAllRead,
  }
})
