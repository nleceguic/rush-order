import { type Page, type Locator } from '@playwright/test'

export class CartPage {
  readonly page: Page
  readonly sheet: Locator
  readonly header: Locator
  readonly emptyMessage: Locator

  constructor(page: Page) {
    this.page         = page
    this.sheet        = page.locator('[class*="fixed"][class*="inset-x"]').filter({ hasText: 'Tu pedido' })
    this.header       = page.getByText('Tu pedido', { exact: true })
    this.emptyMessage = page.getByText('No hay ítems', { exact: false })
  }

  /** Returns the item count extracted from the floating cart button aria-label. */
  async getItemCount(): Promise<number> {
    const btn = this.page.getByRole('button', { name: /Ver carrito/ })
    const label = await btn.getAttribute('aria-label')
    const match = label?.match(/(\d+)\s+ítem/)
    return match ? parseInt(match[1], 10) : 0
  }

  /** Returns the total string (e.g. "15,00 €") from the floating cart button. */
  async getTotal(): Promise<string> {
    const btn = this.page.getByRole('button', { name: /Ver carrito/ })
    const text = await btn.textContent()
    const match = text?.match(/([\d]+[,.][\d]+\s*€)/)
    return match?.[0].trim() ?? '0 €'
  }

  async clearCart(): Promise<void> {
    await this.page.getByText('Vaciar carrito').click()
  }

  async proceedToPayment(): Promise<void> {
    await this.page.goto('/checkout')
  }

  /** Opens the split-bill sheet and selects the given number of splits. */
  async splitBill(parts: number): Promise<void> {
    await this.page.getByRole('button', { name: /Dividir/ }).click()
    await this.page.getByRole('button', { name: String(parts) }).click()
    await this.page.getByRole('button', { name: 'Aplicar' }).click()
  }
}
