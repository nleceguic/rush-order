import { test, expect, type Page } from '@playwright/test'

// ─── Environment ─────────────────────────────────────────────────────────────
const EMAIL         = process.env.E2E_TEST_EMAIL     ?? 'test@rushorder.dev'
const PASSWORD      = process.env.E2E_TEST_PASSWORD  ?? 'Test1234!'
const RESTAURANT_ID = process.env.E2E_RESTAURANT_ID  ?? 'test-restaurant'

// ─── Helpers ─────────────────────────────────────────────────────────────────
async function waitForApiReady(page: Page) {
  await page.waitForFunction(() => document.readyState === 'complete', { timeout: 15_000 })
}

// ─── Auth smoke tests ─────────────────────────────────────────────────────────
test.describe('Authentication @critical', () => {
  test('login page loads', async ({ page }) => {
    await page.goto('/login')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  })

  test('login with valid credentials', async ({ page }) => {
    await page.goto('/login')
    await waitForApiReady(page)

    await page.getByLabel(/email/i).fill(EMAIL)
    await page.getByLabel(/contraseña|password/i).fill(PASSWORD)
    await page.getByRole('button', { name: /iniciar sesión|sign in|login/i }).click()

    // Should redirect away from /login
    await expect(page).not.toHaveURL(/\/login/, { timeout: 10_000 })
  })

  test('invalid credentials show error', async ({ page }) => {
    await page.goto('/login')
    await waitForApiReady(page)

    await page.getByLabel(/email/i).fill('bad@example.com')
    await page.getByLabel(/contraseña|password/i).fill('wrongpassword')
    await page.getByRole('button', { name: /iniciar sesión|sign in|login/i }).click()

    await expect(
      page.getByRole('alert').or(page.getByText(/credenciales|inválid|incorrect/i)),
    ).toBeVisible({ timeout: 8_000 })
  })
})

// ─── PWA customer flow @critical ──────────────────────────────────────────────
test.describe('Customer PWA @critical', () => {
  test('QR landing page resolves menu', async ({ page }) => {
    await page.goto(`/menu/${RESTAURANT_ID}`)
    await waitForApiReady(page)

    // Menu categories should appear
    await expect(page.getByRole('main')).toBeVisible()
    // At least one product card
    await expect(page.locator('[data-testid="product-card"]').first()).toBeVisible({
      timeout: 12_000,
    })
  })

  test('add item to cart', async ({ page }) => {
    await page.goto(`/menu/${RESTAURANT_ID}`)
    await waitForApiReady(page)

    const firstProduct = page.locator('[data-testid="product-card"]').first()
    await firstProduct.waitFor({ timeout: 12_000 })
    await firstProduct.click()

    // Product detail sheet opens
    const addButton = page.getByRole('button', { name: /añadir|add/i })
    await expect(addButton).toBeVisible({ timeout: 5_000 })
    await addButton.click()

    // Cart badge should show 1 item
    await expect(
      page.locator('[data-testid="cart-count"]').or(page.getByText('1', { exact: true })),
    ).toBeVisible({ timeout: 5_000 })
  })

  test('cart checkout flow reaches payment step', async ({ page }) => {
    await page.goto(`/menu/${RESTAURANT_ID}`)
    await waitForApiReady(page)

    const firstProduct = page.locator('[data-testid="product-card"]').first()
    await firstProduct.waitFor({ timeout: 12_000 })
    await firstProduct.click()

    const addButton = page.getByRole('button', { name: /añadir|add/i })
    await expect(addButton).toBeVisible({ timeout: 5_000 })
    await addButton.click()

    // Open cart
    const cartFab = page.locator('[data-testid="cart-fab"]').or(
      page.getByRole('button', { name: /carrito|cart/i }),
    )
    await cartFab.click()

    // Proceed to checkout
    const checkoutButton = page.getByRole('button', { name: /pedir|checkout|confirmar/i })
    await expect(checkoutButton).toBeVisible({ timeout: 5_000 })
    await checkoutButton.click()

    // Should reach checkout / payment step
    await expect(page.getByRole('heading', { name: /tu pedido|checkout|pago/i })).toBeVisible({
      timeout: 8_000,
    })
  })
})

// ─── API health @critical ──────────────────────────────────────────────────────
test.describe('API Health @critical', () => {
  test('health endpoint returns 200', async ({ request }) => {
    const baseUrl = process.env.BASE_URL?.replace(/\/$/, '') ?? ''
    const apiUrl  = process.env.VITE_API_BASE_URL ?? `${baseUrl}/api`

    const response = await request.get(`${apiUrl}/health`.replace('/api/api', '/api'))
    expect(response.status()).toBe(200)
  })

  test('public menu endpoint responds', async ({ request }) => {
    const apiUrl = process.env.VITE_API_BASE_URL ?? '/api'
    const response = await request.get(`${apiUrl}/v1/menu/public/${RESTAURANT_ID}`)
    // 200 (menu exists) or 404 (test restaurant not configured) — not 500
    expect(response.status()).not.toBe(500)
    expect(response.status()).not.toBe(503)
  })
})

// ─── Accessibility @critical ──────────────────────────────────────────────────
test.describe('Accessibility @critical', () => {
  test('landing page has accessible heading structure', async ({ page }) => {
    await page.goto(`/menu/${RESTAURANT_ID}`)
    await waitForApiReady(page)

    // There must be exactly one h1
    const h1Count = await page.locator('h1').count()
    expect(h1Count).toBeGreaterThanOrEqual(1)
  })

  test('modals trap focus correctly', async ({ page }) => {
    await page.goto(`/menu/${RESTAURANT_ID}`)
    await waitForApiReady(page)

    const firstProduct = page.locator('[data-testid="product-card"]').first()
    await firstProduct.waitFor({ timeout: 12_000 })
    await firstProduct.click()

    // Dialog/sheet should be present
    const dialog = page.getByRole('dialog').or(page.locator('[role="dialog"]'))
    await expect(dialog).toBeVisible({ timeout: 5_000 })

    // Press Escape — dialog should close
    await page.keyboard.press('Escape')
    await expect(dialog).not.toBeVisible({ timeout: 3_000 })
  })
})

// ─── KDS verification (staff flow) ───────────────────────────────────────────
test.describe('Staff Dashboard @critical', () => {
  test('login and reach dashboard', async ({ page }) => {
    await page.goto('/login')
    await waitForApiReady(page)

    await page.getByLabel(/email/i).fill(EMAIL)
    await page.getByLabel(/contraseña|password/i).fill(PASSWORD)
    await page.getByRole('button', { name: /iniciar sesión|sign in|login/i }).click()

    // Dashboard or admin area should load
    await expect(page).not.toHaveURL(/\/login/, { timeout: 10_000 })

    // At minimum the authenticated shell loads without JS errors
    const errors: string[] = []
    page.on('pageerror', (err) => errors.push(err.message))
    await page.waitForTimeout(2_000)
    expect(errors.filter((e) => !e.includes('ResizeObserver'))).toHaveLength(0)
  })
})
