<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useSignalRStore } from '@/stores/signalr'
import { useTheme } from '@/composables/useTheme'
import { useNotificationsStore, type NotificationCategory } from '@/stores/notifications'

const { t } = useI18n()
const route = useRoute()
const signalRStore = useSignalRStore()
const notificationsStore = useNotificationsStore()
useTheme()

const mobileMenuOpen = ref(false)
const notificationDrawerOpen = ref(false)

const navLinks = computed(() => [
  { to: '/', label: t('nav.dashboard') },
  { to: '/jobs/new', label: t('nav.newJob') },
  { to: '/connections', label: t('nav.connections') },
  { to: '/settings', label: t('nav.settings') },
  { to: '/webhooks', label: `🔔 ${t('nav.webhooks')}` },
  { to: '/audit', label: `📋 ${t('nav.audit')}` },
])

const mobileBottomLinks = computed(() => [
  { to: '/', icon: '🏠', label: t('nav.dashboard') },
  { to: '/jobs/new', icon: '➕', label: t('nav.newJob') },
  { to: '/connections', icon: '🔗', label: t('nav.connections') },
  { to: '/settings', icon: '⚙️', label: t('nav.settings') },
])

const categoryKey = (category: NotificationCategory) => {
  switch (category) {
    case 'job-completed':
      return 'jobCompleted'
    case 'job-failed':
      return 'jobFailed'
    case 'step-failed':
      return 'stepFailed'
    default:
      return 'system'
  }
}

const refreshPage = () => {
  window.location.reload()
}

const toggleMobileMenu = () => {
  mobileMenuOpen.value = !mobileMenuOpen.value
}

const toggleNotificationDrawer = () => {
  notificationDrawerOpen.value = !notificationDrawerOpen.value
}

const markAllRead = () => {
  notificationsStore.markAllRead()
}

const handleOfflineReady = () => {
  notificationsStore.push({
    category: 'system',
    type: 'info',
    title: t('notifications.types.system'),
    message: t('notifications.offlineReady'),
  })
}

watch(() => route.fullPath, () => {
  mobileMenuOpen.value = false
})

onMounted(() => {
  window.addEventListener('vmto-offline-ready', handleOfflineReady)
})

onUnmounted(() => {
  window.removeEventListener('vmto-offline-ready', handleOfflineReady)
})
</script>

<template>
  <div id="app">
    <nav :class="['sidebar', { open: mobileMenuOpen }]">
      <div class="logo">
        <h2>VMTO</h2>
      </div>
      <RouterLink v-for="link in navLinks" :key="link.to" :to="link.to" class="nav-link" @click="mobileMenuOpen = false">
        {{ link.label }}
      </RouterLink>
    </nav>
    <div v-if="mobileMenuOpen" class="mobile-overlay" @click="mobileMenuOpen = false"></div>

    <main class="content">
      <header class="topbar">
        <button class="icon-btn mobile-only" @click="toggleMobileMenu">☰</button>
        <button class="icon-btn bell-btn" @click="toggleNotificationDrawer">
          🔔
          <span v-if="notificationsStore.unreadCount > 0" class="bell-badge">{{ notificationsStore.unreadCount }}</span>
        </button>
      </header>

      <div v-if="signalRStore.showReconnectBanner" class="signalr-banner warning">
        {{ t('signalr.reconnecting', { seconds: signalRStore.reconnectInSeconds }) }}
      </div>
      <div v-else-if="signalRStore.manualRefreshRequired" class="signalr-banner error">
        <span>{{ t('signalr.manualRefresh') }}</span>
        <button class="refresh-btn" @click="refreshPage">{{ t('signalr.refreshNow') }}</button>
      </div>
      <div class="signalr-quality">
        <span :class="signalRStore.connected ? 'dot-green' : 'dot-red'"></span>
        <span>{{ signalRStore.connected ? t('common.online') : t('common.offline') }}</span>
        <span class="latency">
          {{ signalRStore.latencyMs === null ? t('signalr.latencyUnknown') : t('signalr.latencyValue', { ms: signalRStore.latencyMs }) }}
        </span>
      </div>
      <RouterView />
    </main>

    <aside :class="['notification-drawer', { open: notificationDrawerOpen }]">
      <div class="drawer-header">
        <h3>{{ t('notifications.title') }}</h3>
        <button class="text-btn" @click="markAllRead">{{ t('notifications.markAllRead') }}</button>
      </div>
      <div v-if="notificationsStore.items.length === 0" class="drawer-empty">{{ t('notifications.empty') }}</div>
      <button
        v-for="item in notificationsStore.items"
        :key="item.id"
        :class="['notice-item', { unread: !item.read }]"
        @click="notificationsStore.markRead(item.id)">
        <div class="notice-title">{{ t(`notifications.types.${categoryKey(item.category)}`) }}</div>
        <div class="notice-message">{{ item.message }}</div>
        <div class="notice-time">{{ new Date(item.createdAt).toLocaleString() }}</div>
      </button>
    </aside>

    <div class="toast-stack">
      <div v-for="toast in notificationsStore.toasts" :key="toast.id" :class="['toast-item', toast.type]">
        {{ toast.message }}
      </div>
    </div>

    <nav class="bottom-nav">
      <RouterLink v-for="link in mobileBottomLinks" :key="link.to" :to="link.to" class="bottom-link">
        <span>{{ link.icon }}</span>
        <small>{{ link.label }}</small>
      </RouterLink>
    </nav>
  </div>
</template>

<style>
:root {
  --bg-primary: #f5f5f5;
  --bg-elevated: #ffffff;
  --text-primary: #111827;
  --text-secondary: #6b7280;
  --border-color: #e5e7eb;
  --sidebar-bg: #1a1a2e;
  --sidebar-text: #d1d5db;
  --sidebar-active-bg: #16213e;
  --sidebar-active-text: #ffffff;
}

:root[data-theme='dark'] {
  --bg-primary: #0b1220;
  --bg-elevated: #111827;
  --text-primary: #e5e7eb;
  --text-secondary: #9ca3af;
  --border-color: #334155;
  --sidebar-bg: #020617;
  --sidebar-text: #94a3b8;
  --sidebar-active-bg: #1e293b;
  --sidebar-active-text: #f8fafc;
}

* { margin: 0; padding: 0; box-sizing: border-box; }
body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; background: var(--bg-primary); color: var(--text-primary); }
#app { display: flex; min-height: 100vh; }
.sidebar { width: 220px; background: var(--sidebar-bg); color: var(--sidebar-active-text); padding: 20px 0; display: flex; flex-direction: column; z-index: 40; }
.logo { padding: 0 20px 20px; border-bottom: 1px solid var(--border-color); }
.logo h2 { font-size: 1.4rem; }
.nav-link { display: block; padding: 12px 20px; color: var(--sidebar-text); text-decoration: none; transition: background 0.2s; }
.nav-link:hover, .nav-link.router-link-active { background: var(--sidebar-active-bg); color: var(--sidebar-active-text); }
.content { flex: 1; padding: 24px; }
.topbar { display: flex; justify-content: flex-end; gap: 8px; margin-bottom: 12px; }
.icon-btn { width: 44px; height: 44px; border: 1px solid var(--border-color); border-radius: 10px; cursor: pointer; background: var(--bg-elevated); color: var(--text-primary); position: relative; }
.mobile-only { display: none; }
.bell-badge { position: absolute; right: -6px; top: -6px; min-width: 20px; height: 20px; border-radius: 999px; background: #ef4444; color: #fff; display: inline-flex; align-items: center; justify-content: center; font-size: 0.75rem; padding: 0 6px; }
.signalr-quality { display: inline-flex; align-items: center; gap: 8px; margin-bottom: 12px; color: var(--text-secondary); font-size: 0.85rem; }
.signalr-quality .latency { color: var(--text-secondary); }
.dot-green, .dot-red { width: 8px; height: 8px; border-radius: 999px; display: inline-block; }
.dot-green { background: #16a34a; }
.dot-red { background: #ef4444; }
.signalr-banner { display: flex; align-items: center; justify-content: space-between; padding: 10px 12px; border-radius: 8px; margin-bottom: 10px; font-size: 0.9rem; }
.signalr-banner.warning { background: #fff7ed; color: #9a3412; border: 1px solid #fed7aa; }
.signalr-banner.error { background: #fef2f2; color: #991b1b; border: 1px solid #fecaca; }
.refresh-btn { border: 0; background: #1d4ed8; color: #fff; padding: 6px 10px; border-radius: 6px; cursor: pointer; }
.refresh-btn:hover { background: #1e40af; }
.notification-drawer {
  position: fixed;
  right: 0;
  top: 0;
  bottom: 0;
  width: min(360px, 90vw);
  background: var(--bg-elevated);
  border-left: 1px solid var(--border-color);
  transform: translateX(100%);
  transition: transform 0.2s ease;
  z-index: 60;
  display: flex;
  flex-direction: column;
}
.notification-drawer.open { transform: translateX(0); }
.drawer-header { display: flex; align-items: center; justify-content: space-between; padding: 16px; border-bottom: 1px solid var(--border-color); }
.text-btn { border: 0; background: transparent; color: #2563eb; cursor: pointer; }
.drawer-empty { padding: 20px; color: var(--text-secondary); }
.notice-item { width: 100%; text-align: left; border: 0; border-bottom: 1px solid var(--border-color); background: transparent; padding: 12px 16px; cursor: pointer; color: var(--text-primary); }
.notice-item.unread { background: color-mix(in srgb, var(--bg-elevated) 88%, #2563eb 12%); }
.notice-title { font-weight: 600; font-size: 0.9rem; margin-bottom: 3px; }
.notice-message { font-size: 0.85rem; color: var(--text-secondary); margin-bottom: 4px; }
.notice-time { font-size: 0.75rem; color: var(--text-secondary); }
.toast-stack { position: fixed; top: 18px; right: 18px; z-index: 80; display: flex; flex-direction: column; gap: 8px; }
.toast-item { min-width: 260px; border-radius: 8px; padding: 10px 12px; color: #fff; box-shadow: 0 2px 8px rgba(0,0,0,.2); }
.toast-item.success { background: #059669; }
.toast-item.error { background: #dc2626; }
.toast-item.warning { background: #d97706; }
.toast-item.info { background: #2563eb; }
.bottom-nav { display: none; }
.mobile-overlay { display: none; }

@media (max-width: 768px) {
  .sidebar {
    position: fixed;
    left: 0;
    top: 0;
    bottom: 0;
    transform: translateX(-100%);
    transition: transform 0.2s ease;
    box-shadow: 0 8px 24px rgba(0,0,0,.35);
  }
  .sidebar.open { transform: translateX(0); }
  .mobile-overlay {
    display: block;
    position: fixed;
    inset: 0;
    background: rgba(0,0,0,.4);
    z-index: 30;
  }
  .content { padding: 16px 12px 84px; }
  .mobile-only { display: inline-flex; align-items: center; justify-content: center; }
  .topbar { justify-content: space-between; }
  button, .btn, .btn-sm, .btn-page, .btn-toggle { min-height: 44px; }
  .bottom-nav {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    position: fixed;
    left: 0;
    right: 0;
    bottom: 0;
    height: 70px;
    background: var(--bg-elevated);
    border-top: 1px solid var(--border-color);
    z-index: 70;
  }
  .bottom-link {
    text-decoration: none;
    color: var(--text-secondary);
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 3px;
  }
  .bottom-link.router-link-active { color: #2563eb; }
  .bottom-link small { font-size: 0.7rem; }
  .toast-stack {
    left: 12px;
    right: 12px;
    top: auto;
    bottom: 82px;
  }
  .toast-item { min-width: 100%; }
}
</style>
