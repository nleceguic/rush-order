import { test as base, type Page, expect } from '@playwright/test'

export { expect }

export const TEST_QR_TOKEN = 'TEST_QR_001'
export const OWNER_EMAIL   = 'owner@demo.com'
export const DEMO_PASSWORD = 'Demo1234!'
export const API_BASE_URL  = process.env.API_BASE_URL ?? 'http://localhost:5000'

interface AppFixtures {
  /** Desktop browser navigated to the QR menu landing page. */
  restaurantPage: Page
  /** Mobile-viewport browser (390×844) navigated to the QR menu landing page. */
  customerPage: Page
  /** Browser navigated to the admin panel (placeholder for future admin E2E). */
  adminPage: Page
}

export const test = base.extend<AppFixtures>({
  restaurantPage: async ({ page }, use) => {
    await page.goto(`/menu/${TEST_QR_TOKEN}`)
    await page.waitForLoadState('load')
    await use(page)
  },

  customerPage: async ({ browser }, use) => {
    const context = await browser.newContext({
      viewport: { width: 390, height: 844 },
      deviceScaleFactor: 3,
      isMobile: true,
      hasTouch: true,
    })
    const page = await context.newPage()
    await page.goto(`/menu/${TEST_QR_TOKEN}`)
    await page.waitForLoadState('load')
    await use(page)
    await context.close()
  },

  adminPage: async ({ page }, use) => {
    await page.goto('/admin')
    await page.waitForLoadState('load')
    await use(page)
  },
})
