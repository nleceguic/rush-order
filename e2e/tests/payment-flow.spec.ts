/**
 * E2E — Payment flow with Stripe test card.
 *
 * Requires: PWA + API running, Stripe configured in test mode (sk_test_... / pk_test_...)
 *
 * Stripe test card: 4242 4242 4242 4242, any future date, any 3-digit CVC.
 *
 * NOTE: Stripe's PaymentElement renders inside sandboxed iframes. Playwright accesses
 * them via frameLocator. If VITE_STRIPE_PUBLISHABLE_KEY is not set the PaymentPage
 * renders a fallback — this test skips gracefully in that case.
 */
import { test, expect, OWNER_EMAIL, DEMO_PASSWORD } from '../fixtures/app.fixture'
import { PaymentPage }         from '../pages/pwa/PaymentPage'
import { TrackingPage }        from '../pages/pwa/TrackingPage'
import { RestaurantApiHelper } from '../helpers/RestaurantApiHelper'

const STRIPE_TEST_CARD   = '4242424242424242'
const STRIPE_TEST_EXPIRY = '12/34'
const STRIPE_TEST_CVC    = '123'

test.describe('Payment flow — Stripe test card', () => {
  let ownerToken:  string
  let orderId:     string

  test.beforeEach(async ({ request, page }) => {
    const api = new RestaurantApiHelper(request)

    if (!(await api.isHealthy())) {
      test.skip(true, 'API server is not reachable — skipping payment E2E test')
    }

    // Verify Stripe key is configured in the app
    await page.goto('/')
    const stripeConfigured = await page.evaluate(() =>
      Boolean((window as unknown as Record<string, string>).__STRIPE_KEY__ ??
        document.querySelector('[data-stripe]')),
    ).catch(() => false)

    // Set up: create an order via API so we have a valid orderId for the payment page
    ownerToken = await api.login(OWNER_EMAIL, DEMO_PASSWORD)
    const restaurantId = await api.getRestaurantId(ownerToken)
    const tableId      = await api.getFirstTableId(ownerToken, restaurantId)
    const product      = await api.getFirstAvailableProduct(restaurantId)

    orderId = await api.createOrder(ownerToken, tableId, [
      { productId: product.id, quantity: 2 },
    ])

    // Advance to Served so payment is valid
    await api.updateOrderStatus(ownerToken, orderId, 'Confirmed')
    await api.updateOrderStatus(ownerToken, orderId, 'Preparing')
    await api.updateOrderStatus(ownerToken, orderId, 'Ready')
    await api.updateOrderStatus(ownerToken, orderId, 'Served')
  })

  test('fills Stripe test card and confirms payment, then tracking shows Paid', async ({ page }) => {
    const paymentPage = new PaymentPage(page)
    const trackingPage = new TrackingPage(page)

    // ── 1. Navigate to payment page ──────────────────────────────────────────
    await paymentPage.goto(orderId)

    // ── 2. Wait for payment form / Stripe iframe ─────────────────────────────
    // If Stripe key is missing the page shows an error banner — detect and skip
    const stripeError = page.getByText(/Stripe.*no.*configur|payment.*unavailable/i)
    if (await stripeError.isVisible({ timeout: 5_000 }).catch(() => false)) {
      test.skip(true, 'Stripe is not configured in this environment')
    }

    // Wait for the PaymentElement iframe to render
    const stripeIframe = page.locator(
      'iframe[title*="Secure payment"], iframe[name*="__privateStripeFrame"]',
    ).first()
    await stripeIframe.waitFor({ state: 'attached', timeout: 20_000 })

    // ── 3. Fill Stripe test card ─────────────────────────────────────────────
    await paymentPage.fillStripeCard(STRIPE_TEST_CARD, STRIPE_TEST_EXPIRY, STRIPE_TEST_CVC)

    // ── 4. Confirm payment ───────────────────────────────────────────────────
    await paymentPage.confirmPayment()

    // ── 5. Verify redirect → tracking with Paid / success status ────────────
    await expect(page).toHaveURL(/\/(payment\/result|tracking\/)/, { timeout: 30_000 })

    // If redirected to tracking, verify Paid or success state
    if (page.url().includes('/tracking/')) {
      await trackingPage.waitForStatus('Paid', 20_000)
    } else {
      // PaymentResultPage — verify success indicator
      await expect(
        page.getByText(/pago.*realizado|pago.*confirmado|¡gracias|success/i),
      ).toBeVisible({ timeout: 10_000 })
    }
  })

  test('shows order total on payment page', async ({ page }) => {
    const paymentPage = new PaymentPage(page)
    await paymentPage.goto(orderId)

    // PaymentPage renders the order total
    const totalEl = page.locator('[class*="total"], [class*="amount"]').filter({ hasText: /€/ }).first()
    await expect(totalEl).toBeVisible({ timeout: 10_000 })
    const totalText = await totalEl.textContent()
    expect(totalText).toMatch(/\d+[,.]?\d*\s*€/)
  })
})
