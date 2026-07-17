import type { APIRequestContext } from '@playwright/test'

export interface OrderItem {
  productId: string
  quantity:  number
  notes?:    string | null
}

export class RestaurantApiHelper {
  private readonly request: APIRequestContext
  readonly baseUrl: string

  constructor(request: APIRequestContext, baseUrl?: string) {
    this.request = request
    this.baseUrl = baseUrl ?? (process.env.API_BASE_URL ?? 'http://localhost:5000')
  }

  // ── Auth ──────────────────────────────────────────────────────────────────

  async login(email: string, password: string): Promise<string> {
    const res = await this.request.post(`${this.baseUrl}/api/v1/auth/login`, {
      data: { email, password },
    })
    if (!res.ok()) {
      throw new Error(`Login failed (${res.status()}): ${await res.text()}`)
    }
    const body = await res.json() as { accessToken: string }
    return body.accessToken
  }

  // ── Orders ────────────────────────────────────────────────────────────────

  async createOrder(token: string, tableId: string, items: OrderItem[]): Promise<string> {
    const res = await this.request.post(`${this.baseUrl}/api/v1/orders`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        tableId,
        customerId: null,
        items: items.map((i) => ({
          productId: i.productId,
          quantity:  i.quantity,
          notes:     i.notes ?? null,
          modifiers: [],
        })),
        notes:  null,
        source: 'QR',
      },
    })
    if (!res.ok()) {
      throw new Error(`Create order failed (${res.status()}): ${await res.text()}`)
    }
    const body = await res.json() as { data: { orderId: string } }
    return body.data.orderId
  }

  async updateOrderStatus(token: string, orderId: string, status: string): Promise<void> {
    const res = await this.request.patch(
      `${this.baseUrl}/api/v1/orders/${orderId}/status`,
      {
        headers: { Authorization: `Bearer ${token}` },
        data: { status, note: null },
      },
    )
    if (!res.ok()) {
      throw new Error(`Update status failed (${res.status()}): ${await res.text()}`)
    }
  }

  async getOrder(token: string, orderId: string): Promise<{ status: string; total: number }> {
    const res = await this.request.get(`${this.baseUrl}/api/v1/orders/${orderId}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    if (!res.ok()) throw new Error(`Get order failed (${res.status()}): ${await res.text()}`)
    const body = await res.json() as { data: { status: string; total: number } }
    return body.data
  }

  // ── Tables ────────────────────────────────────────────────────────────────

  async getFirstTableId(token: string, restaurantId: string): Promise<string> {
    const res = await this.request.get(
      `${this.baseUrl}/api/v1/tables?restaurantId=${restaurantId}`,
      { headers: { Authorization: `Bearer ${token}` } },
    )
    if (!res.ok()) throw new Error(`Get tables failed (${res.status()}): ${await res.text()}`)
    const body = await res.json() as { data: Array<{ id: string }> }
    if (body.data.length === 0) throw new Error('No tables found for restaurant')
    return body.data[0].id
  }

  // ── Menu ─────────────────────────────────────────────────────────────────

  async getFirstAvailableProduct(restaurantId: string): Promise<{ id: string; name: string }> {
    const res = await this.request.get(
      `${this.baseUrl}/api/v1/menu/public/${restaurantId}`,
    )
    if (!res.ok()) throw new Error(`Get menu failed (${res.status()}): ${await res.text()}`)

    const body = await res.json() as {
      data: {
        categories: Array<{ products: Array<{ id: string; name: string; isAvailable: boolean }> }>
      }
    }
    const product = body.data.categories
      .flatMap((c) => c.products)
      .find((p) => p.isAvailable)

    if (!product) throw new Error('No available products found in menu')
    return { id: product.id, name: product.name }
  }

  async getRestaurantId(token: string): Promise<string> {
    const res = await this.request.get(`${this.baseUrl}/api/v1/restaurants`, {
      headers: { Authorization: `Bearer ${token}` },
    })
    if (!res.ok()) throw new Error(`Get restaurants failed (${res.status()}): ${await res.text()}`)
    const body = await res.json() as { data: Array<{ id: string }> }
    if (body.data.length === 0) throw new Error('No restaurants found')
    return body.data[0].id
  }

  // ── Health check ──────────────────────────────────────────────────────────

  /** Returns true if the API is reachable. Tests use this to skip when the backend is down. */
  async isHealthy(): Promise<boolean> {
    try {
      const res = await this.request.get(`${this.baseUrl}/health`, { timeout: 5_000 })
      return res.ok()
    } catch {
      return false
    }
  }
}
