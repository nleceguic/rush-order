/**
 * E2E — Complete customer journey: QR scan → menu → filter → cart → confirm → tracking.
 *
 * Requires: PWA dev server on E2E_BASE_URL (default http://localhost:5173)
 *           API server on API_BASE_URL (default http://localhost:5000)
 */
import { test, expect, TEST_QR_TOKEN, OWNER_EMAIL, DEMO_PASSWORD } from '../fixtures/app.fixture'
import { MenuPage }            from '../pages/pwa/MenuPage'
import { TrackingPage }        from '../pages/pwa/TrackingPage'
import { RestaurantApiHelper } from '../helpers/RestaurantApiHelper'

test.describe('Complete customer journey — QR to order confirmation', () => {
  test.beforeEach(async ({ request }) => {
    const api = new RestaurantApiHelper(request)
    if (!(await api.isHealthy())) {
      test.skip(true, 'API server is not reachable — skipping E2E test')
    }
  })

  test('QR scan → menu loads → filter → add products → checkout → tracking shows Pendiente', async ({
    page,
    request,
  }) => {
    const menu     = new MenuPage(page)
    const tracking = new TrackingPage(page)
    const api      = new RestaurantApiHelper(request)

    // ── 1. Customer opens URL with QR token ──────────────────────────────────
    await menu.goto(TEST_QR_TOKEN)

    // ── 2. Verify menu loaded — restaurant name visible ──────────────────────
    // LandingPage shows the restaurant name from QR data in the sticky header
    await expect(menu.restaurantName).toBeVisible({ timeout: 15_000 })
    const restaurantName = await menu.restaurantName.textContent()
    expect(restaurantName?.trim().length).toBeGreaterThan(0)

    // Wait for product cards to appear (skeleton resolved)
    await menu.waitForMenuLoaded()

    // ── 3. Apply "Vegetariano" filter ────────────────────────────────────────
    await menu.filterBy('Vegetariano')
    await expect(page.getByRole('group', { name: 'Filtros de dieta' })
      .getByText('Vegetariano')).toHaveAttribute('aria-pressed', 'true')

    // Products may be reduced — check there are still visible cards or "no results" message
    const hasProducts  = await page.locator('article[role="button"]').count()
    const hasNoResults = await page.getByText('Sin resultados').isVisible().catch(() => false)
    expect(hasProducts > 0 || hasNoResults).toBe(true)

    // ── 4. Deactivate filter and add 2 products to cart ──────────────────────
    // Deactivate to ensure products are available
    await menu.filterBy('Vegetariano')
    await menu.waitForMenuLoaded()

    const product1 = await menu.addFirstAvailableProduct()
    // Add a second time to get quantity 2, or add another product
    await menu.addFirstAvailableProduct()

    // ── 5. Open cart → verify item count ────────────────────────────────────
    await menu.openCart()
    await expect(page.getByText('Tu pedido')).toBeVisible()

    // Cart should show at least 1 item
    const itemRows = page.locator('[class*="CartItemRow"], [data-testid="cart-item"]')
    const rowCount = await itemRows.count()
    // If no data-testid, count via product name rows inside the cart sheet
    if (rowCount === 0) {
      // Fallback: floating button aria-label has count
      const floatingBtn = page.getByRole('button', { name: /Ver carrito/ })
      const ariaLabel   = await floatingBtn.getAttribute('aria-label').catch(() => '')
      expect(ariaLabel).toMatch(/[1-9]\d*\s+ítem/)
    }

    // ── 6. Complete order through multi-step cart flow ───────────────────────
    await menu.confirmOrder()

    // ── 7. Order sent — see animated confirmation ────────────────────────────
    await menu.waitForOrderConfirmation()
    await expect(page.getByText('¡Pedido enviado!')).toBeVisible()

    // ── 8. Navigate to tracking page ────────────────────────────────────────
    await menu.navigateToTracking()
    await expect(page).toHaveURL(/\/tracking\/[0-9a-f-]+/)

    // ── 9. Tracking shows "Pendiente" (New/Pending status) ──────────────────
    await tracking.waitForStatus('Pendiente', 15_000)
    expect(await tracking.isReadyBannerVisible()).toBe(false)

    // ── 10. Via API: advance order to Ready ──────────────────────────────────
    // Extract orderId from URL
    const url     = page.url()
    const orderId = url.split('/tracking/')[1]?.split('?')[0] ?? ''
    expect(orderId).toMatch(/^[0-9a-f-]+$/)

    const token = await api.login(OWNER_EMAIL, DEMO_PASSWORD)

    // Walk through required status transitions: Confirmed → Preparing → Ready
    await api.updateOrderStatus(token, orderId, 'Confirmed')
    await api.updateOrderStatus(token, orderId, 'Preparing')
    await api.updateOrderStatus(token, orderId, 'Ready')

    // ── 11. Tracking auto-updates to "¡Listo!" (polling or SignalR) ──────────
    // TrackingPage polls or receives a SignalR push — wait up to 30 s for the update
    await tracking.waitForStatus('Listo', 30_000)
    await expect(page.getByText('¡Listo', { exact: false })).toBeVisible({ timeout: 5_000 })
  })
})
