import { test, expect } from '@playwright/test'

// 登入頁面測試
test.describe('登入頁面', () => {
  test('應顯示登入表單', async ({ page }) => {
    await page.goto('/login')
    await expect(page.getByRole('heading')).toContainText('VMTO')
    await expect(page.getByLabel(/使用者|帳號|username/i)).toBeVisible()
    await expect(page.getByLabel(/密碼|password/i)).toBeVisible()
    await expect(page.getByRole('button', { name: /登入|login/i })).toBeVisible()
  })

  test('空白帳號密碼應顯示錯誤', async ({ page }) => {
    await page.goto('/login')
    await page.getByRole('button', { name: /登入|login/i }).click()
    // 預期顯示錯誤訊息
    await expect(page.locator('.error, [role="alert"]')).toBeVisible()
  })

  test('未登入時應重導至登入頁', async ({ page }) => {
    await page.goto('/')
    await expect(page).toHaveURL(/\/login/)
  })
})
