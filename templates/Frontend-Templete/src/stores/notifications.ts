import { defineStore } from 'pinia'

export type NotificationCategory = 'system' | 'job-completed' | 'job-failed' | 'step-failed'

export interface NotificationItem {
  id: string
  title: string
  message: string
  category: NotificationCategory
  read: boolean
  createdAt: string
}

export interface ToastItem {
  id: string
  type: 'info' | 'success' | 'warning' | 'error'
  message: string
}

export const useNotificationsStore = defineStore('notifications', {
  state: () => ({
    items: [
      {
        id: '1',
        title: '系統公告',
        message: '歡迎使用 Hyper-Modern Glassmorphic Dashboard 架構範本。',
        category: 'system' as NotificationCategory,
        read: false,
        createdAt: new Date().toISOString(),
      },
    ] as NotificationItem[],
    toasts: [] as ToastItem[],
  }),
  getters: {
    unreadCount: (state) => state.items.filter((i) => !i.read).length,
  },
  actions: {
    push(item: Omit<NotificationItem, 'id' | 'read' | 'createdAt'>) {
      const notice: NotificationItem = {
        id: Math.random().toString(36).substring(2, 9),
        read: false,
        createdAt: new Date().toISOString(),
        ...item,
      }
      this.items.unshift(notice)
      this.toast({
        type: item.category === 'job-failed' ? 'error' : 'info',
        message: item.message,
      })
    },
    markRead(id: string) {
      const found = this.items.find((i) => i.id === id)
      if (found) found.read = true
    },
    markAllRead() {
      this.items.forEach((i) => (i.read = true))
    },
    toast(toast: Omit<ToastItem, 'id'>) {
      const id = Math.random().toString(36).substring(2, 9)
      this.toasts.push({ id, ...toast })
      setTimeout(() => {
        this.toasts = this.toasts.filter((t) => t.id !== id)
      }, 4000)
    },
  },
})
