import { type Page } from '@playwright/test'

export class PaymentPage {
  readonly page: Page

  constructor(page: Page) {
    this.page = page
  }

  async goto(orderId: string): Promise<void> {
    await this.page.goto(`/payment/${orderId}`)
    await this.page.waitForLoadState('networkidle')
  }

  /**
   * Fills the Stripe PaymentElement test card.
   * The Stripe Elements iframe selector varies by Stripe.js version; we try several patterns.
   */
  async fillStripeCard(number: string, expiry: string, cvc: string): Promise<void> {
    // Stripe renders a composite PaymentElement that may contain nested iframes.
    // The outermost iframe has title "Secure payment input frame" or similar.
    const stripeFrame = this.page
      .frameLocator('iframe[title*="Secure payment"], iframe[name*="__privateStripeFrame"]')
      .first()

    // Card number field
    const cardInput = stripeFrame
      .locator('[placeholder*="1234"], [data-testid="cardNumber-input"], input[autocomplete="cc-number"]')
      .first()
    await cardInput.fill(number)

    // Expiry
    const expiryInput = stripeFrame
      .locator('[placeholder*="MM / YY"], [placeholder*="MM/YY"], [data-testid="cardExpiry-input"]')
      .first()
    await expiryInput.fill(expiry)

    // CVC
    const cvcInput = stripeFrame
      .locator('[placeholder*="CVC"], [placeholder*="CVV"], [data-testid="cardCvc-input"]')
      .first()
    await cvcInput.fill(cvc)
  }

  async confirmPayment(): Promise<void> {
    await this.page.getByRole('button', { name: /Pagar|Pay now|Confirmar pago/i }).click()
  }

  /**
   * Waits for the post-payment result page and returns the receipt/order number
   * if rendered on screen.
   */
  async getReceiptNumber(): Promise<string> {
    // PaymentResultPage or TrackingPage — wait for redirect
    await this.page.waitForURL(/\/(payment\/result|tracking\/)/, { timeout: 20_000 })
    // Order number rendered as a large heading or specific element
    const el = this.page
      .locator('[data-testid="order-number"], .order-number, h1, h2')
      .filter({ hasText: /^#/ })
      .first()
    await el.waitFor({ timeout: 10_000 })
    return (await el.textContent())?.trim() ?? ''
  }
}
