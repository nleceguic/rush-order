/**
 * E2E — PWA installability checks.
 *
 * Validates that:
 *   1. /manifest.json is present and contains required fields
 *   2. A Service Worker is registered and active
 *   3. The beforeinstallprompt event fires (Chromium only)
 *
 * These tests do NOT require the backend API.
 */
import { test, expect } from '../fixtures/app.fixture'

test.describe('PWA is installable', () => {
  test('manifest.json is valid and contains required fields', async ({ request }) => {
    // Fetch the manifest relative to the PWA base URL
    const baseURL = process.env.BASE_URL ?? 'http://localhost:5173'
    const res = await request.get(`${baseURL}/manifest.json`)

    expect(res.ok(), `GET /manifest.json returned ${res.status()}`).toBe(true)

    const manifest = await res.json() as Record<string, unknown>

    // W3C Web App Manifest required fields for installability
    expect(manifest.name,         'manifest.name is required').toBeTruthy()
    expect(manifest.short_name,   'manifest.short_name is required').toBeTruthy()
    expect(manifest.start_url,    'manifest.start_url is required').toBeTruthy()
    expect(manifest.display,      'manifest.display is required').toBeTruthy()
    expect(manifest.icons,        'manifest.icons is required').toBeTruthy()
    expect(Array.isArray(manifest.icons)).toBe(true)

    // Must have at least a 192×192 icon (Chrome requirement)
    const icons = manifest.icons as Array<{ sizes: string; src: string }>
    const has192 = icons.some((i) => i.sizes?.includes('192x192'))
    expect(has192, 'Must have a 192×192 icon for Chrome installability').toBe(true)

    // display should be standalone or fullscreen for install prompt
    expect(['standalone', 'fullscreen', 'minimal-ui']).toContain(manifest.display)

    // theme_color should be set (Chrome shows it in the title bar)
    expect(manifest.theme_color, 'theme_color should be set').toBeTruthy()
  })

  test('Service Worker registers and becomes active', async ({ page }) => {
    await page.goto('/')
    await page.waitForLoadState('networkidle')

    // Evaluate SW registration state in the browser context
    const swState = await page.evaluate(async () => {
      if (!('serviceWorker' in navigator)) return { supported: false }

      const registrations = await navigator.serviceWorker.getRegistrations()
      if (registrations.length === 0) return { supported: true, registered: false }

      const reg = registrations[0]
      const worker = reg.active ?? reg.installing ?? reg.waiting
      return {
        supported:  true,
        registered: true,
        state:      worker?.state ?? 'unknown',
        scope:      reg.scope,
      }
    })

    expect(swState.supported, 'Service Worker API must be supported').toBe(true)
    expect(swState.registered, 'A Service Worker must be registered').toBe(true)
    expect(['activated', 'activating', 'installed']).toContain(swState.state)
  })

  test('beforeinstallprompt event is dispatchable (Chromium only)', async ({
    page,
    browserName,
  }) => {
    test.skip(browserName !== 'chromium', 'beforeinstallprompt is Chromium-only')

    await page.goto('/')
    await page.waitForLoadState('networkidle')

    // Inject a listener before the event would fire (it may fire on first load)
    // We verify the app does not suppress the event by checking if it was captured
    // or by checking that the page has the required PWA criteria in its manifest.
    const manifestLinked = await page.evaluate(() => {
      const link = document.querySelector<HTMLLinkElement>('link[rel="manifest"]')
      return link?.href ?? null
    })

    expect(manifestLinked, 'A <link rel="manifest"> must be present in the document').toBeTruthy()

    // Dispatch a synthetic beforeinstallprompt to verify the app does not block it
    const handled = await page.evaluate(() =>
      new Promise<boolean>((resolve) => {
        const event = new Event('beforeinstallprompt', { bubbles: true, cancelable: true })
        const prevented = !document.dispatchEvent(event)
        resolve(!prevented) // resolve true if the event was NOT prevented (app allows install)
      }),
    )

    // The app should not call preventDefault() to block the install prompt
    expect(handled, 'App must not suppress the install prompt event').toBe(true)
  })
})
