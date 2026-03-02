import { createRouter, createWebHistory } from 'vue-router'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'login', component: () => import('@/views/LoginView.vue'), meta: { public: true } },
    { path: '/', name: 'dashboard', component: () => import('@/views/DashboardView.vue') },
    { path: '/jobs/new', name: 'new-job', component: () => import('@/views/NewJobView.vue') },
    { path: '/jobs/:id', name: 'job-detail', component: () => import('@/views/JobDetailView.vue') },
    { path: '/connections', name: 'connections', component: () => import('@/views/ConnectionsView.vue') },
    { path: '/settings', name: 'settings', component: () => import('@/views/SettingsView.vue') },
  ],
})

// 導航守衛：未登入時導向登入頁
router.beforeEach((to) => {
  const token = localStorage.getItem('vmto_token')
  if (!to.meta.public && !token) {
    return { name: 'login' }
  }
})
