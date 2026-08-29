<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useSignalRStore } from '@/stores/signalr'
import { useAuthStore } from '@/stores/auth'
import { useTheme } from '@/composables/useTheme'
import { useNotificationsStore, type NotificationCategory } from '@/stores/notifications'

const { t } = useI18n()
const route = useRoute()
const authStore = useAuthStore()
const signalRStore = useSignalRStore()
const notificationsStore = useNotificationsStore()
const { resolvedTheme, setThemeMode } = useTheme()

const isAuthPage = computed(() => {
  return route.name === 'login' || !authStore.isAuthenticated
})

const mobileMenuOpen = ref(false)
const notificationDrawerOpen = ref(false)

// Mini-Sidebar state management (Pinned vs Floating/Hover)
const sidebarPinned = ref(localStorage.getItem('vmto_sidebar_pinned') !== 'false')
const sidebarHovered = ref(false)

const isSidebarExpanded = computed(() => {
  return sidebarPinned.value || sidebarHovered.value
})

const toggleSidebarPin = () => {
  sidebarPinned.value = !sidebarPinned.value
  localStorage.setItem('vmto_sidebar_pinned', String(sidebarPinned.value))
}

const onSidebarMouseEnter = () => {
  if (!sidebarPinned.value) {
    sidebarHovered.value = true
  }
}

const onSidebarMouseLeave = () => {
  sidebarHovered.value = false
}

const navLinks = computed(() => [
  { to: '/', icon: '📊', label: t('nav.dashboard') },
  { to: '/jobs/new', icon: '🚀', label: t('nav.newJob') },
  { to: '/connections', icon: '🔌', label: t('nav.connections') },
  { to: '/audit', icon: '🛡️', label: t('nav.audit') },
  { to: '/settings', icon: '⚙️', label: t('nav.settings') },
])

const mobileBottomLinks = computed(() => [
  { to: '/', icon: '📊', label: t('nav.dashboard') },
  { to: '/jobs/new', icon: '🚀', label: t('nav.newJob') },
  { to: '/connections', icon: '🔌', label: t('nav.connections') },
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

const toggleTheme = () => {
  setThemeMode(resolvedTheme.value === 'dark' ? 'light' : 'dark')
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

const closeNotificationDrawer = () => {
  notificationDrawerOpen.value = false
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

const handleKeyDown = (e: KeyboardEvent) => {
  if (e.key === 'Escape') {
    if (notificationDrawerOpen.value) {
      notificationDrawerOpen.value = false
    }
    if (mobileMenuOpen.value) {
      mobileMenuOpen.value = false
    }
  }
}

watch(() => route.fullPath, () => {
  mobileMenuOpen.value = false
  notificationDrawerOpen.value = false
})

onMounted(() => {
  window.addEventListener('vmto-offline-ready', handleOfflineReady)
  window.addEventListener('keydown', handleKeyDown)
})

onUnmounted(() => {
  window.removeEventListener('vmto-offline-ready', handleOfflineReady)
  window.removeEventListener('keydown', handleKeyDown)
})
</script>

<template>
  <div id="app" :class="['app-layout', { 'auth-layout': isAuthPage }]">
    <!-- Unauthenticated / Login View: Pure Centered Layout -->
    <template v-if="isAuthPage">
      <main class="auth-page-wrapper">
        <RouterView />
      </main>
    </template>

    <!-- Authenticated Console Layout -->
    <template v-else>
      <!-- Dynamic Mini / Expanded Sidebar Navigation -->
      <aside
        :class="[
          'glass-sidebar',
          {
            open: mobileMenuOpen,
            'is-mini': !isSidebarExpanded,
            'is-expanded': isSidebarExpanded,
            'is-pinned': sidebarPinned,
          },
        ]"
        @mouseenter="onSidebarMouseEnter"
        @mouseleave="onSidebarMouseLeave"
      >
        <div class="sidebar-header">
          <div class="sidebar-brand">
            <div class="brand-badge">
              <div class="brand-logo-container">
                <img src="/logo.png" alt="VMTO Logo" class="brand-sidebar-logo" />
              </div>
              <div class="brand-text">
                <span class="brand-title">VMTO</span>
                <span class="brand-subtitle">Transfer Orchestrator</span>
              </div>
            </div>
          </div>
          <button
            class="pin-toggle-btn"
            :title="sidebarPinned ? '解除釘選 (進入迷你模式)' : '釘選側邊欄'"
            @click="toggleSidebarPin"
          >
            <span>{{ sidebarPinned ? '📌' : '📍' }}</span>
          </button>
        </div>

        <nav class="sidebar-nav">
          <RouterLink
            v-for="link in navLinks"
            :key="link.to"
            :to="link.to"
            class="nav-item"
            :title="!isSidebarExpanded ? link.label : ''"
            @click="mobileMenuOpen = false"
          >
            <span class="nav-icon">{{ link.icon }}</span>
            <span class="nav-label">{{ link.label }}</span>
            <div class="nav-active-glow"></div>
          </RouterLink>
        </nav>

        <div class="sidebar-footer">
          <div class="cluster-status-card" :title="!isSidebarExpanded ? (signalRStore.connected ? '即時連線中' : '連線中斷') : ''">
            <div class="status-indicator">
              <span :class="['pulse-dot', signalRStore.connected ? 'online' : 'offline']"></span>
              <span class="status-text">{{ signalRStore.connected ? t('common.online') : t('common.offline') }}</span>
            </div>
            <div class="latency-text">
              {{ signalRStore.latencyMs === null ? '0ms' : `${signalRStore.latencyMs}ms latency` }}
            </div>
          </div>
        </div>
      </aside>

      <div v-if="mobileMenuOpen" class="mobile-overlay" @click="mobileMenuOpen = false"></div>

      <!-- Main Content Container -->
      <div :class="['main-wrapper', { 'sidebar-mini-offset': !sidebarPinned }]">
        <header class="glass-topbar">
          <div class="topbar-left">
            <button class="neu-icon-btn mobile-toggle" @click="toggleMobileMenu">
              <span>☰</span>
            </button>
            <div class="breadcrumb-trail">
              <span class="breadcrumb-root">VMTO Engine</span>
              <span class="breadcrumb-sep">/</span>
              <span class="breadcrumb-current">{{ route.name?.toString() || 'Console' }}</span>
            </div>
          </div>

          <div class="topbar-right">
            <!-- Theme Toggle -->
            <button
              class="neu-icon-btn theme-toggle-btn"
              :title="resolvedTheme === 'dark' ? 'Switch to Light' : 'Switch to Dark'"
              @click="toggleTheme"
            >
              <span>{{ resolvedTheme === 'dark' ? '🌙' : '☀️' }}</span>
            </button>

            <!-- Notification Bell -->
            <button
              class="neu-icon-btn bell-btn"
              title="通知中心"
              @click="toggleNotificationDrawer"
            >
              <span>🔔</span>
              <span v-if="notificationsStore.unreadCount > 0" class="bell-badge">
                {{ notificationsStore.unreadCount }}
              </span>
            </button>
          </div>
        </header>

        <!-- Reconnect Banner -->
        <transition name="fade">
          <div v-if="signalRStore.showReconnectBanner" class="neu-banner banner-warning">
            <span>⚠️ {{ t('signalr.reconnecting', { seconds: signalRStore.reconnectInSeconds }) }}</span>
          </div>
          <div v-else-if="signalRStore.manualRefreshRequired" class="neu-banner banner-error">
            <span>🚨 {{ t('signalr.manualRefresh') }}</span>
            <button class="neu-btn btn-sm btn-primary" @click="refreshPage">{{ t('signalr.refreshNow') }}</button>
          </div>
        </transition>

        <!-- Router View Content -->
        <main class="page-content">
          <RouterView />
        </main>
      </div>

      <!-- Notification Drawer Backdrop (Click-Outside Handler) -->
      <transition name="fade">
        <div
          v-if="notificationDrawerOpen"
          class="drawer-backdrop"
          @click="closeNotificationDrawer"
        ></div>
      </transition>

      <!-- Notification Drawer -->
      <aside :class="['glass-drawer', { open: notificationDrawerOpen }]">
        <div class="drawer-header">
          <div class="drawer-title">
            <span>🔔</span>
            <h3>{{ t('notifications.title') }}</h3>
          </div>
          <div class="drawer-actions">
            <button class="neu-text-btn" @click="markAllRead">{{ t('notifications.markAllRead') }}</button>
            <button class="drawer-close-btn" title="關閉通知 (Esc)" @click="closeNotificationDrawer">✕</button>
          </div>
        </div>

        <div v-if="notificationsStore.items.length === 0" class="drawer-empty">
          <span class="empty-icon">✨</span>
          <p>{{ t('notifications.empty') }}</p>
        </div>

        <div class="drawer-list">
          <div
            v-for="item in notificationsStore.items"
            :key="item.id"
            :class="['notice-card', { unread: !item.read }]"
            @click="notificationsStore.markRead(item.id)"
          >
            <div class="notice-header">
              <span class="notice-tag">{{ t(`notifications.types.${categoryKey(item.category)}`) }}</span>
              <span class="notice-time">{{ new Date(item.createdAt).toLocaleTimeString() }}</span>
            </div>
            <div class="notice-message">{{ item.message }}</div>
          </div>
        </div>
      </aside>

      <!-- Mobile Bottom Navigation -->
      <nav class="glass-bottom-nav">
        <RouterLink v-for="link in mobileBottomLinks" :key="link.to" :to="link.to" class="bottom-item">
          <span class="bottom-icon">{{ link.icon }}</span>
          <span class="bottom-label">{{ link.label }}</span>
        </RouterLink>
      </nav>
    </template>

    <!-- Toast Notifications (Always Global) -->
    <div class="toast-stack">
      <transition-group name="toast">
        <div v-for="toast in notificationsStore.toasts" :key="toast.id" :class="['glass-toast', toast.type]">
          <span class="toast-icon">{{ toast.type === 'success' ? '✅' : toast.type === 'error' ? '❌' : 'ℹ️' }}</span>
          <span class="toast-text">{{ toast.message }}</span>
        </div>
      </transition-group>
    </div>
  </div>
</template>

<style>
/* ==========================================================================
   Hyper-Modern Glassmorphism & Neumorphic Design System (Light & Dark)
   ========================================================================== */

:root {
  /* Color Palette - Light */
  --bg-gradient: radial-gradient(at 0% 0%, #f0f4ff 0, transparent 50%),
                 radial-gradient(at 100% 0%, #fdf2f8 0, transparent 50%),
                 radial-gradient(at 100% 100%, #f0fdf4 0, transparent 50%),
                 radial-gradient(at 0% 100%, #faf5ff 0, transparent 50%),
                 #f3f6fc;
  --bg-primary: #f3f6fc;
  --bg-surface: rgba(255, 255, 255, 0.7);
  --bg-surface-elevated: rgba(255, 255, 255, 0.85);
  --bg-surface-solid: #ffffff;

  --glass-border: 1px solid rgba(255, 255, 255, 0.8);
  --glass-border-subtle: 1px solid rgba(226, 232, 240, 0.8);
  --glass-blur: blur(20px);
  --glass-blur-sm: blur(12px);

  --neu-shadow: 6px 6px 16px rgba(166, 179, 203, 0.35), -6px -6px 16px rgba(255, 255, 255, 0.9);
  --neu-shadow-sm: 3px 3px 8px rgba(166, 179, 203, 0.3), -3px -3px 8px rgba(255, 255, 255, 0.8);
  --neu-shadow-hover: 8px 8px 20px rgba(166, 179, 203, 0.45), -8px -8px 20px rgba(255, 255, 255, 0.95);
  --neu-inset: inset 2px 2px 5px rgba(166, 179, 203, 0.35), inset -2px -2px 5px rgba(255, 255, 255, 0.9);

  --text-primary: #0f172a;
  --text-secondary: #475569;
  --text-muted: #94a3b8;

  --primary: #4f46e5;
  --primary-hover: #4338ca;
  --primary-gradient: linear-gradient(135deg, #6366f1 0%, #4f46e5 100%);
  --primary-glow: rgba(99, 102, 241, 0.3);

  --success: #10b981;
  --success-bg: rgba(16, 185, 129, 0.12);
  --warning: #f59e0b;
  --warning-bg: rgba(245, 158, 11, 0.12);
  --danger: #ef4444;
  --danger-bg: rgba(239, 68, 68, 0.12);
  --info: #0ea5e9;
  --info-bg: rgba(14, 165, 233, 0.12);
}

:root[data-theme='dark'] {
  /* Color Palette - Dark */
  --bg-gradient: radial-gradient(at 0% 0%, #1e1b4b 0, transparent 40%),
                 radial-gradient(at 100% 0%, #31103f 0, transparent 40%),
                 radial-gradient(at 50% 100%, #064e3b 0, transparent 40%),
                 #0a0e17;
  --bg-primary: #0a0e17;
  --bg-surface: rgba(18, 24, 38, 0.65);
  --bg-surface-elevated: rgba(24, 32, 50, 0.85);
  --bg-surface-solid: #151d2f;

  --glass-border: 1px solid rgba(255, 255, 255, 0.08);
  --glass-border-subtle: 1px solid rgba(255, 255, 255, 0.05);
  --glass-blur: blur(24px);
  --glass-blur-sm: blur(14px);

  --neu-shadow: 6px 6px 18px rgba(0, 0, 0, 0.6), -4px -4px 14px rgba(255, 255, 255, 0.03);
  --neu-shadow-sm: 3px 3px 10px rgba(0, 0, 0, 0.5), -2px -2px 8px rgba(255, 255, 255, 0.02);
  --neu-shadow-hover: 8px 8px 24px rgba(0, 0, 0, 0.8), -6px -6px 18px rgba(255, 255, 255, 0.04);
  --neu-inset: inset 2px 2px 6px rgba(0, 0, 0, 0.7), inset -2px -2px 6px rgba(255, 255, 255, 0.03);

  --text-primary: #f8fafc;
  --text-secondary: #cbd5e1;
  --text-muted: #64748b;

  --primary: #6366f1;
  --primary-hover: #818cf8;
  --primary-gradient: linear-gradient(135deg, #818cf8 0%, #6366f1 100%);
  --primary-glow: rgba(99, 102, 241, 0.4);

  --success: #34d399;
  --success-bg: rgba(52, 211, 153, 0.15);
  --warning: #fbbf24;
  --warning-bg: rgba(251, 191, 36, 0.15);
  --danger: #f87171;
  --danger-bg: rgba(248, 113, 113, 0.15);
  --info: #38bdf8;
  --info-bg: rgba(56, 189, 248, 0.15);
}

/* ==========================================================================
   Global Reset & Base Elements
   ========================================================================== */

* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
  font-family: 'Plus Jakarta Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
}

body {
  background: var(--bg-gradient);
  background-attachment: fixed;
  color: var(--text-primary);
  min-height: 100vh;
  overflow-x: hidden;
}

/* Main Layout */
.app-layout {
  display: flex;
  min-height: 100vh;
  position: relative;
}

.auth-layout {
  display: block;
}

.auth-page-wrapper {
  width: 100%;
  min-height: 100vh;
}

/* ==========================================================================
   Dynamic Mini / Expanded Sidebar
   ========================================================================== */

.glass-sidebar {
  width: 260px;
  background: var(--bg-surface);
  backdrop-filter: var(--glass-blur);
  -webkit-backdrop-filter: var(--glass-blur);
  border-right: var(--glass-border);
  display: flex;
  flex-direction: column;
  padding: 24px 16px;
  z-index: 50;
  box-shadow: var(--neu-shadow-sm);
  transition: width 0.3s cubic-bezier(0.4, 0, 0.2, 1), transform 0.3s cubic-bezier(0.4, 0, 0.2, 1), box-shadow 0.3s ease;
  position: relative;
}

/* Mini Sidebar Mode */
.glass-sidebar.is-mini:not(.open) {
  width: 76px;
  padding: 24px 12px;
}

.glass-sidebar.is-mini .brand-text,
.glass-sidebar.is-mini .nav-label,
.glass-sidebar.is-mini .pin-toggle-btn,
.glass-sidebar.is-mini .status-text,
.glass-sidebar.is-mini .latency-text {
  opacity: 0;
  pointer-events: none;
  display: none;
}

.glass-sidebar.is-mini .nav-item {
  justify-content: center;
  padding: 12px;
}

.glass-sidebar.is-mini .cluster-status-card {
  padding: 10px 0;
  display: flex;
  justify-content: center;
}

.glass-sidebar.is-mini .status-indicator {
  margin-bottom: 0;
}

/* Hover Expanded Mode (when unpinned) */
.glass-sidebar.is-expanded:not(.is-pinned) {
  position: absolute;
  top: 0;
  bottom: 0;
  left: 0;
  width: 260px;
  box-shadow: var(--neu-shadow-hover);
  background: var(--bg-surface-solid);
  z-index: 60;
}

.sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 28px;
  padding: 0 4px;
}

.sidebar-brand {
  flex: 1;
  min-width: 0;
}

.brand-badge {
  display: flex;
  align-items: center;
  gap: 12px;
}

.brand-logo-container {
  width: 44px;
  height: 44px;
  min-width: 44px;
  display: flex;
  align-items: center;
  justify-content: center;
  filter: drop-shadow(0 4px 12px var(--primary-glow));
}

.brand-sidebar-logo {
  width: 100%;
  height: 100%;
  object-fit: contain;
}

.brand-text {
  transition: opacity 0.2s ease;
  white-space: nowrap;
  overflow: hidden;
}

.brand-title {
  font-size: 1.3rem;
  font-weight: 800;
  letter-spacing: -0.5px;
  background: var(--primary-gradient);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  display: block;
}

.brand-subtitle {
  font-size: 0.75rem;
  color: var(--text-muted);
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.pin-toggle-btn {
  background: transparent;
  border: none;
  cursor: pointer;
  font-size: 1.1rem;
  padding: 6px;
  border-radius: 8px;
  transition: all 0.2s ease;
  opacity: 0.7;
}

.pin-toggle-btn:hover {
  opacity: 1;
  background: var(--bg-surface-elevated);
  transform: scale(1.1);
}

.sidebar-nav {
  display: flex;
  flex-direction: column;
  gap: 8px;
  flex: 1;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  border-radius: 12px;
  color: var(--text-secondary);
  text-decoration: none;
  font-size: 0.95rem;
  font-weight: 600;
  position: relative;
  transition: all 0.2s ease;
  white-space: nowrap;
}

.nav-icon {
  font-size: 1.25rem;
  min-width: 24px;
  text-align: center;
}

.nav-label {
  transition: opacity 0.2s ease;
}

.nav-item:hover {
  background: var(--bg-surface-elevated);
  color: var(--text-primary);
  box-shadow: var(--neu-shadow-sm);
  transform: translateX(4px);
}

.nav-item.router-link-active {
  background: var(--primary-gradient);
  color: #ffffff;
  box-shadow: 0 6px 18px var(--primary-glow);
}

.sidebar-footer {
  padding-top: 16px;
}

.cluster-status-card {
  background: var(--bg-surface-elevated);
  border: var(--glass-border-subtle);
  border-radius: 12px;
  padding: 12px 16px;
  box-shadow: var(--neu-inset);
  transition: all 0.2s ease;
}

.status-indicator {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}

.pulse-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  position: relative;
}

.pulse-dot.online {
  background: var(--success);
  box-shadow: 0 0 10px var(--success);
}

.pulse-dot.offline {
  background: var(--danger);
  box-shadow: 0 0 10px var(--danger);
}

.status-text {
  font-size: 0.85rem;
  font-weight: 700;
}

.latency-text {
  font-size: 0.75rem;
  color: var(--text-muted);
}

/* ==========================================================================
   Main Wrapper & Topbar
   ========================================================================== */

.main-wrapper {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  position: relative;
  transition: padding-left 0.3s ease;
}

.sidebar-mini-offset {
  margin-left: 0;
}

.glass-topbar {
  height: 72px;
  padding: 0 32px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: var(--bg-surface);
  backdrop-filter: var(--glass-blur);
  -webkit-backdrop-filter: var(--glass-blur);
  border-bottom: var(--glass-border);
  position: sticky;
  top: 0;
  z-index: 40;
}

.topbar-left {
  display: flex;
  align-items: center;
  gap: 16px;
}

.breadcrumb-trail {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 0.9rem;
  font-weight: 600;
}

.breadcrumb-root {
  color: var(--text-muted);
}

.breadcrumb-sep {
  color: var(--text-muted);
}

.breadcrumb-current {
  color: var(--text-primary);
  font-weight: 700;
}

.topbar-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

/* Neumorphic Buttons */
.neu-icon-btn {
  width: 44px;
  height: 44px;
  border-radius: 12px;
  border: var(--glass-border);
  background: var(--bg-surface-elevated);
  box-shadow: var(--neu-shadow-sm);
  color: var(--text-primary);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  position: relative;
  font-size: 1.1rem;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}

.neu-icon-btn:hover {
  transform: translateY(-2px);
  box-shadow: var(--neu-shadow-hover);
}

.neu-icon-btn:active {
  transform: translateY(0);
  box-shadow: var(--neu-inset);
}

.mobile-toggle {
  display: none;
}

.bell-badge {
  position: absolute;
  top: -4px;
  right: -4px;
  min-width: 18px;
  height: 18px;
  background: var(--danger);
  color: #fff;
  border-radius: 999px;
  font-size: 0.7rem;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0 4px;
  box-shadow: 0 2px 6px rgba(239, 68, 68, 0.5);
}

/* Content Area */
.page-content {
  flex: 1;
  padding: 32px;
  max-width: 1360px;
  width: 100%;
  margin: 0 auto;
}

/* Banners */
.neu-banner {
  margin: 16px 32px 0;
  padding: 14px 20px;
  border-radius: 14px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  backdrop-filter: var(--glass-blur);
  border: var(--glass-border);
  box-shadow: var(--neu-shadow-sm);
  font-size: 0.95rem;
  font-weight: 600;
}

.banner-warning {
  background: rgba(245, 158, 11, 0.15);
  color: #d97706;
  border-color: rgba(245, 158, 11, 0.3);
}

.banner-error {
  background: rgba(239, 68, 68, 0.15);
  color: #dc2626;
  border-color: rgba(239, 68, 68, 0.3);
}

/* ==========================================================================
   Notification Drawer & Backdrop
   ========================================================================== */

.drawer-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.35);
  backdrop-filter: blur(4px);
  -webkit-backdrop-filter: blur(4px);
  z-index: 90;
}

.glass-drawer {
  position: fixed;
  top: 0;
  right: 0;
  bottom: 0;
  width: 380px;
  background: var(--bg-surface-elevated);
  backdrop-filter: var(--glass-blur);
  -webkit-backdrop-filter: var(--glass-blur);
  border-left: var(--glass-border);
  box-shadow: var(--neu-shadow-hover);
  transform: translateX(100%);
  transition: transform 0.35s cubic-bezier(0.4, 0, 0.2, 1);
  z-index: 100;
  display: flex;
  flex-direction: column;
}

.glass-drawer.open {
  transform: translateX(0);
}

.drawer-header {
  padding: 20px 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: var(--glass-border-subtle);
}

.drawer-title {
  display: flex;
  align-items: center;
  gap: 8px;
}

.drawer-title h3 {
  font-size: 1.1rem;
  font-weight: 700;
}

.drawer-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.drawer-close-btn {
  background: transparent;
  border: none;
  font-size: 1.2rem;
  color: var(--text-muted);
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 6px;
  transition: all 0.2s ease;
}

.drawer-close-btn:hover {
  color: var(--text-primary);
  background: var(--bg-surface);
}

.neu-text-btn {
  background: transparent;
  border: none;
  color: var(--primary);
  font-weight: 600;
  font-size: 0.85rem;
  cursor: pointer;
}

.drawer-empty {
  padding: 60px 24px;
  text-align: center;
  color: var(--text-muted);
}

.drawer-empty .empty-icon {
  font-size: 2.5rem;
  display: block;
  margin-bottom: 12px;
}

.drawer-list {
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  overflow-y: auto;
  flex: 1;
}

.notice-card {
  background: var(--bg-surface);
  border: var(--glass-border-subtle);
  border-radius: 12px;
  padding: 14px 16px;
  box-shadow: var(--neu-shadow-sm);
  cursor: pointer;
  transition: all 0.2s ease;
}

.notice-card.unread {
  border-left: 4px solid var(--primary);
  background: var(--bg-surface-elevated);
}

.notice-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}

.notice-tag {
  font-size: 0.75rem;
  font-weight: 700;
  color: var(--primary);
  text-transform: uppercase;
}

.notice-time {
  font-size: 0.75rem;
  color: var(--text-muted);
}

.notice-message {
  font-size: 0.85rem;
  color: var(--text-secondary);
  line-height: 1.4;
}

/* Glass Toasts */
.toast-stack {
  position: fixed;
  top: 24px;
  right: 24px;
  z-index: 120;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.glass-toast {
  min-width: 320px;
  padding: 14px 20px;
  border-radius: 14px;
  background: var(--bg-surface-elevated);
  backdrop-filter: var(--glass-blur);
  border: var(--glass-border);
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.2);
  display: flex;
  align-items: center;
  gap: 12px;
  color: var(--text-primary);
  font-weight: 600;
  font-size: 0.95rem;
}

.glass-toast.success { border-left: 5px solid var(--success); }
.glass-toast.error { border-left: 5px solid var(--danger); }
.glass-toast.warning { border-left: 5px solid var(--warning); }
.glass-toast.info { border-left: 5px solid var(--info); }

/* Glass Bottom Nav */
.glass-bottom-nav {
  display: none;
}

/* ==========================================================================
   Responsive Rules
   ========================================================================== */

@media (max-width: 992px) {
  .glass-sidebar {
    position: fixed;
    left: 0;
    top: 0;
    bottom: 0;
    width: 260px !important;
    padding: 24px 16px !important;
    transform: translateX(-100%);
    box-shadow: 0 0 40px rgba(0, 0, 0, 0.4);
  }

  .glass-sidebar .brand-text,
  .glass-sidebar .nav-label,
  .glass-sidebar .status-text,
  .glass-sidebar .latency-text {
    opacity: 1 !important;
    display: block !important;
  }

  .glass-sidebar .pin-toggle-btn {
    display: none !important;
  }

  .glass-sidebar.open {
    transform: translateX(0);
  }

  .mobile-overlay {
    display: block;
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.5);
    backdrop-filter: blur(4px);
    z-index: 45;
  }

  .mobile-toggle {
    display: flex;
  }

  .page-content {
    padding: 20px 16px 84px;
  }

  .glass-topbar {
    padding: 0 16px;
  }

  .glass-bottom-nav {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    position: fixed;
    bottom: 0;
    left: 0;
    right: 0;
    height: 68px;
    background: var(--bg-surface-elevated);
    backdrop-filter: var(--glass-blur);
    border-top: var(--glass-border);
    z-index: 50;
    box-shadow: 0 -4px 20px rgba(0, 0, 0, 0.1);
  }

  .bottom-item {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 4px;
    color: var(--text-secondary);
    text-decoration: none;
    font-size: 0.75rem;
    font-weight: 600;
  }

  .bottom-item.router-link-active {
    color: var(--primary);
  }

  .bottom-icon {
    font-size: 1.25rem;
  }
}

/* Animations */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.25s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.toast-enter-active,
.toast-leave-active {
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.toast-enter-from {
  opacity: 0;
  transform: translateY(-20px) scale(0.95);
}

.toast-leave-to {
  opacity: 0;
  transform: translateX(100px);
}
</style>
