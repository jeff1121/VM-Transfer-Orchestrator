import { test, expect } from '@playwright/test'

// 登入頁面測試
test.describe('登入頁面', () => {
  test('應顯示登入表單', async ({ page }) => {
    await page.goto('/login')
    await expect(page.locator('.login-card .brand-title')).toHaveText('VMTO')
    await expect(page.locator('#username')).toBeVisible()
    await expect(page.locator('#password')).toBeVisible()
    await expect(page.locator('button[type="submit"]')).toBeVisible()
  })

  test('空白帳號密碼應顯示錯誤', async ({ page }) => {
    await page.goto('/login')
    await page.locator('button[type="submit"]').click()
    // 預期顯示錯誤訊息
    await expect(page.locator('.banner-error, .error, [role="alert"]')).toBeVisible()
  })

  test('未登入時應重導至登入頁', async ({ page }) => {
    await page.goto('/')
    await expect(page).toHaveURL(/\/login/)
  })
})
