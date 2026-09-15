// @ts-check
import { test, expect } from '@playwright/test'

const E2E_USERNAME = process.env.WALOS_E2E_USERNAME
const E2E_PASSWORD = process.env.WALOS_E2E_PASSWORD

test.describe('Authentication Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Clear storage before each test
    await page.goto('/')
    await page.evaluate(() => localStorage.clear())
  })

  test('should show login page when not authenticated', async ({ page }) => {
    await page.goto('/')
    
    // Should redirect to login or show login page
    await expect(page.getByText('Walos')).toBeVisible()
    await expect(page.getByLabel(/usuario/i)).toBeVisible()
    await expect(page.getByLabel(/contraseña/i)).toBeVisible()
    await expect(page.getByRole('button', { name: /iniciar sesión/i })).toBeVisible()
  })

  test('should show error with invalid credentials', async ({ page }) => {
    await page.goto('/')

    await page.getByLabel(/usuario/i).fill('wrong@email.com')
    await page.getByLabel(/contraseña/i).fill('wrongpassword')
    await page.getByRole('button', { name: /iniciar sesión/i }).click()

    // Should show error toast or message
    await expect(page.getByText(/credenciales|error|inválid/i)).toBeVisible({ timeout: 10_000 })
  })

  test('should show error with empty fields', async ({ page }) => {
    await page.goto('/')

    await page.getByRole('button', { name: /iniciar sesión/i }).click()

    // Should show validation error
    await expect(page.getByText(/ingresa usuario|requerido/i)).toBeVisible({ timeout: 5_000 })
  })

  test('should login successfully with valid credentials', async ({ page }) => {
    test.skip(!E2E_USERNAME || !E2E_PASSWORD, 'Configura WALOS_E2E_USERNAME y WALOS_E2E_PASSWORD')
    await page.goto('/')

    await page.getByLabel(/usuario/i).fill(E2E_USERNAME)
    await page.getByLabel(/contraseña/i).fill(E2E_PASSWORD)
    await page.getByRole('button', { name: /iniciar sesión/i }).click()

    // Should redirect to app after successful login
    await expect(page).not.toHaveURL(/login/, { timeout: 10_000 })

    // Should show authenticated UI elements (sidebar, header, etc.)
    await expect(page.getByText(/inventario|dashboard|bienvenido/i)).toBeVisible({ timeout: 10_000 })
  })

  test('should toggle password visibility', async ({ page }) => {
    await page.goto('/')

    const passwordInput = page.getByLabel(/contraseña/i)
    await passwordInput.fill('testpass')

    // Password should be hidden by default
    await expect(passwordInput).toHaveAttribute('type', 'password')

    // Click eye toggle
    await page.locator('button[type="button"]').first().click()

    // Password should now be visible
    await expect(passwordInput).toHaveAttribute('type', 'text')
  })

  test('should persist session after page reload', async ({ page }) => {
    test.skip(!E2E_USERNAME || !E2E_PASSWORD, 'Configura WALOS_E2E_USERNAME y WALOS_E2E_PASSWORD')
    await page.goto('/')

    // Login
    await page.getByLabel(/usuario/i).fill(E2E_USERNAME)
    await page.getByLabel(/contraseña/i).fill(E2E_PASSWORD)
    await page.getByRole('button', { name: /iniciar sesión/i }).click()

    // Wait for navigation
    await expect(page).not.toHaveURL(/login/, { timeout: 10_000 })

    // Reload page
    await page.reload()

    // Should still be authenticated (not redirected to login)
    await page.waitForTimeout(2000)
    await expect(page).not.toHaveURL(/login/)
  })

  test('should not expose development credentials', async ({ page }) => {
    await page.goto('/')

    await expect(page.getByText(/Credenciales de desarrollo/i)).toHaveCount(0)
  })
})
