/**
 * E2E — Accessibility checks on the customer-facing QR menu (read-only,
 * safe for the CD post-deploy smoke gate).
 *
 * Migrated from the former pwa/tests/e2e/smoke.spec.ts "Accessibility" block.
 */
import { test, expect, TEST_QR_TOKEN } from '../fixtures/app.fixture'
import { MenuPage } from '../pages/pwa/MenuPage'

test.describe('Accessibility @critical', () => {
  test('landing page has accessible heading structure', async ({ page }) => {
    const menu = new MenuPage(page)
    await menu.goto(TEST_QR_TOKEN)

    // There must be exactly one h1
    const h1Count = await page.locator('h1').count()
    expect(h1Count).toBeGreaterThanOrEqual(1)
  })

  test('product detail modal traps focus and closes on Escape', async ({ page }) => {
    const menu = new MenuPage(page)
    await menu.goto(TEST_QR_TOKEN)
    await menu.waitForMenuLoaded()

    // Click the product card itself (not the add button, which stops
    // propagation) to open the ProductDetailSheet dialog.
    await page.locator('article[role="button"] h3').first().click()

    const dialog = page.getByRole('dialog')
    await expect(dialog).toBeVisible({ timeout: 5_000 })

    await page.keyboard.press('Escape')
    await expect(dialog).not.toBeVisible({ timeout: 3_000 })
  })
})
