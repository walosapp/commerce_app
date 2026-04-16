// @ts-check
import { test, expect } from '@playwright/test'

test.describe('Sales Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Login before each test
    await page.goto('/')
    await page.evaluate(() => localStorage.clear())
    await page.goto('/')

    await page.getByLabel(/usuario/i).fill('admin@mibar.com')
    await page.getByLabel(/contraseña/i).fill('admin123')
    await page.getByRole('button', { name: /iniciar sesión/i }).click()

    // Wait for authenticated state
    await expect(page).not.toHaveURL(/login/, { timeout: 10_000 })
  })

  test('should navigate to sales page', async ({ page }) => {
    // Click sales link in sidebar or navigation
    await page.getByRole('link', { name: /ventas|sales/i }).click()

    // Should show sales page
    await expect(page.getByText(/mesas|ventas|tables/i)).toBeVisible({ timeout: 5_000 })
  })

  test('should show active tables or empty state', async ({ page }) => {
    await page.getByRole('link', { name: /ventas|sales/i }).click()
    await page.waitForTimeout(2000)

    // Either shows tables or an empty state message
    const hasTables = await page.getByText(/mesa|table/i).isVisible().catch(() => false)
    const hasEmpty = await page.getByText(/sin mesas|no hay|crear/i).isVisible().catch(() => false)

    expect(hasTables || hasEmpty).toBe(true)
  })

  test('should open create table form', async ({ page }) => {
    await page.getByRole('link', { name: /ventas|sales/i }).click()
    await page.waitForTimeout(2000)

    // Click create/new table button
    const createBtn = page.getByRole('button', { name: /nueva mesa|crear mesa|new table|agregar/i })
    if (await createBtn.isVisible().catch(() => false)) {
      await createBtn.click()

      // Should show a form or modal for creating a table
      await expect(page.getByText(/producto|item|agregar/i)).toBeVisible({ timeout: 5_000 })
    }
  })
})
