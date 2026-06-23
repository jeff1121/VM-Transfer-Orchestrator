import { test, expect } from '@playwright/test'

// Connections 頁面測試
test.describe('連線管理', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('vmto_token', 'test-token')
      localStorage.setItem('vmto_role', 'Admin')
      localStorage.setItem('vmto_user', 'admin')
    })
  })

  test('應能導航至連線管理頁', async ({ page }) => {
    await page.goto('/connections')
    await expect(page.locator('h1, h2, .page-title')).toBeVisible({ timeout: 5000 })
  })
})
