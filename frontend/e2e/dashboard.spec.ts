import { test, expect } from '@playwright/test'

// 模擬登入的 helper
async function login(page: import('@playwright/test').Page) {
  await page.goto('/login')
  // 使用 input type 或 placeholder 定位，避免依賴特定標籤
  const usernameInput = page.locator('input[type="text"], input[placeholder*="使用者"], input[placeholder*="帳號"]').first()
  const passwordInput = page.locator('input[type="password"]').first()

  await usernameInput.fill('admin')
  await passwordInput.fill('admin123')
  await page.getByRole('button', { name: /登入|login/i }).click()

  // 等待導航至 Dashboard
  await page.waitForURL('**/', { timeout: 5000 }).catch(() => {
    // 若 API 未啟動，login 可能不會實際導航
  })
}

// Dashboard 頁面測試
test.describe('Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    // 模擬 token 直接注入 localStorage（跳過實際 API 呼叫）
    await page.addInitScript(() => {
      localStorage.setItem('vmto_token', 'test-token')
      localStorage.setItem('vmto_role', 'Admin')
      localStorage.setItem('vmto_user', 'admin')
    })
  })

  test('應顯示 Dashboard 標題', async ({ page }) => {
    await page.goto('/')
    // 檢查頁面標題或主要元素
    await expect(page.locator('h1, h2, .dashboard-title')).toBeVisible({ timeout: 5000 })
  })

  test('應包含導航選項', async ({ page }) => {
    await page.goto('/')
    // 應有連結到主要功能頁面
    const nav = page.locator('nav, .sidebar, .nav')
    await expect(nav).toBeVisible({ timeout: 5000 })
  })
})
