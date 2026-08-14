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

    // With an ~852 KiB precache the worker can still be 'installing' right after
    // networkidle — poll in-page (single round-trip) until it progresses past that
    // transient state, instead of widening the set of states this test accepts.
    const swState = await page.evaluate(async () => {
      if (!('serviceWorker' in navigator)) return { supported: false }

      const deadline = Date.now() + 15_000
      let registered = false
      let state = 'unknown'
      let scope = ''

      while (Date.now() < deadline) {
        const registrations = await navigator.serviceWorker.getRegistrations()
        if (registrations.length > 0) {
          registered = true
          const reg = registrations[0]
          const worker = reg.active ?? reg.installing ?? reg.waiting
          state = worker?.state ?? 'unknown'
          scope = reg.scope
          if (state !== 'installing' && state !== 'unknown') break
        }
        await new Promise((resolve) => setTimeout(resolve, 250))
      }

      return { supported: true, registered, state, scope }
    })

    expect(swState.supported, 'Service Worker API must be supported').toBe(true)
    expect(swState.registered, 'A Service Worker must be registered').toBe(true)
    expect(['activated', 'activating', 'installed']).toContain(swState.state)
  })

  test('beforeinstallprompt is intercepted to show the custom install banner (Chromium only)', async ({
    page,
    browserName,
  }) => {
    test.skip(browserName !== 'chromium', 'beforeinstallprompt is Chromium-only')

    // usePwaInstall.ts only shows PwaInstallBanner once REQUIRED_USES (2) mounts have
    // happened and there's no active dismiss cooldown — seed the use counter so this
    // single page load satisfies that gate (usesCount = stored value + 1 on mount).
    await page.addInitScript(() => localStorage.setItem('pwa-uses', '1'))

    await page.goto('/')
    await page.waitForLoadState('networkidle')

    // Inject a listener before the event would fire (it may fire on first load)
    const manifestLinked = await page.evaluate(() => {
      const link = document.querySelector<HTMLLinkElement>('link[rel="manifest"]')
      return link?.href ?? null
    })

    expect(manifestLinked, 'A <link rel="manifest"> must be present in the document').toBeTruthy()

    // Dispatch a synthetic beforeinstallprompt — usePwaInstall.ts deliberately calls
    // preventDefault() to defer the native prompt in favor of its own install UI.
    const prevented = await page.evaluate(() =>
      new Promise<boolean>((resolve) => {
        const event = new Event('beforeinstallprompt', { bubbles: true, cancelable: true })
        resolve(!document.dispatchEvent(event))
      }),
    )

    expect(prevented, 'App must intercept beforeinstallprompt to show its own install UI').toBe(true)

    // PwaInstallBanner (role="banner") should now render with the deferred prompt —
    // scoped to a <div>, since the page's <header> also carries the landmark role.
    await expect(page.locator('div[role="banner"]')).toBeVisible({ timeout: 5_000 })
  })
})
