// @ts-check
import { test, expect } from '@playwright/test'

test.describe('Navigation', () => {
  test.beforeEach(async ({ page }) => {
    // Login before each test
    await page.goto('/')
    await page.evaluate(() => localStorage.clear())
    await page.goto('/')

    await page.getByLabel(/usuario/i).fill('admin@mibar.com')
    await page.getByLabel(/contraseña/i).fill('admin123')
    await page.getByRole('button', { name: /iniciar sesión/i }).click()

    await expect(page).not.toHaveURL(/login/, { timeout: 10_000 })
  })

  test('should navigate to inventory', async ({ page }) => {
    await page.getByRole('link', { name: /inventario|inventory/i }).click()
    await expect(page.getByText(/producto|inventario|stock/i)).toBeVisible({ timeout: 5_000 })
  })

  test('should navigate to finance', async ({ page }) => {
    await page.getByRole('link', { name: /finanzas|finance/i }).click()
    await expect(page.getByText(/finanz|gastos|ingresos|expense/i)).toBeVisible({ timeout: 5_000 })
  })

  test('should navigate to settings', async ({ page }) => {
    await page.getByRole('link', { name: /config|settings|ajustes/i }).click()
    await expect(page.getByText(/config|empresa|company|tema|theme/i)).toBeVisible({ timeout: 5_000 })
  })

  test('should show sidebar with main modules', async ({ page }) => {
    // Check sidebar has links to main modules
    const sidebar = page.locator('nav, aside, [role="navigation"]')
    await expect(sidebar.getByText(/inventario|inventory/i)).toBeVisible()
    await expect(sidebar.getByText(/ventas|sales/i)).toBeVisible()
    await expect(sidebar.getByText(/finanzas|finance/i)).toBeVisible()
  })
})
