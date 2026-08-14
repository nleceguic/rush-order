/**
 * E2E — API health checks (read-only, safe to run against any environment
 * including production, used by the CD post-deploy smoke gate).
 *
 * Migrated from the former pwa/tests/e2e/smoke.spec.ts "API Health" block.
 */
import { test, expect } from '../fixtures/app.fixture'
import { RestaurantApiHelper } from '../helpers/RestaurantApiHelper'

test.describe('API Health @critical', () => {
  test('health endpoint returns 200', async ({ request }) => {
    const api = new RestaurantApiHelper(request)
    const response = await request.get(`${api.baseUrl}/health`)
    expect(response.status()).toBe(200)
  })

  test('public menu endpoint responds', async ({ request }) => {
    const api = new RestaurantApiHelper(request)
    const response = await request.get(`${api.baseUrl}/api/v1/menu/public/test-restaurant`)
    // 200 (menu exists) or 404 (test restaurant not configured) — not a server error
    expect(response.status()).not.toBe(500)
    expect(response.status()).not.toBe(503)
  })
})
