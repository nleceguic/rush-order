import { type Page, type Locator } from '@playwright/test'

export class MenuPage {
  readonly page: Page
  readonly restaurantName: Locator
  readonly filterBar: Locator
  readonly loadingSpinner: Locator

  constructor(page: Page) {
    this.page          = page
    this.restaurantName = page.locator('header span.font-semibold').first()
    this.filterBar      = page.getByRole('group', { name: 'Filtros de dieta' })
    this.loadingSpinner = page.locator('[class*="Spinner"], [aria-label="Cargando"]')
  }

  async goto(qrToken: string): Promise<void> {
    await this.page.goto(`/menu/${qrToken}`)
    // Wait for skeleton to resolve: either restaurant name appears or error state
    await this.page.waitForFunction(() => {
      const spinner = document.querySelector('[class*="animate-spin"]')
      return spinner === null
    }, { timeout: 15_000 })
  }

  async waitForMenuLoaded(): Promise<void> {
    // Product cards are article[role="button"] — wait for at least one
    await this.page.waitForSelector('article[role="button"]', { timeout: 15_000 })
  }

  async getCategoryList(): Promise<string[]> {
    // CategoryNav renders as a nav with buttons
    const buttons = this.page
      .locator('#menu-section nav button, [id="menu-section"] [role="navigation"] button')
      .filter({ hasText: /\S/ })
    return buttons.allTextContents()
  }

  async filterBy(filterLabel: string): Promise<void> {
    await this.filterBar.getByText(filterLabel, { exact: false }).click()
  }

  async isFilterActive(filterLabel: string): Promise<boolean> {
    const btn = this.filterBar.getByText(filterLabel, { exact: false })
    const pressed = await btn.getAttribute('aria-pressed')
    return pressed === 'true'
  }

  async getVisibleProductNames(): Promise<string[]> {
    const cards = this.page.locator('article[role="button"] h3')
    return cards.allTextContents()
  }

  async addProductToCart(productName: string): Promise<void> {
    const addBtn = this.page.getByRole('button', {
      name: new RegExp(`Añadir ${productName}`, 'i'),
    })
    await addBtn.first().click()
  }

  /** Adds the first available (non-disabled) product to the cart. */
  async addFirstAvailableProduct(): Promise<string> {
    const addBtns = this.page.getByRole('button', { name: /Añadir .+ al pedido/ })
    const firstEnabled = addBtns.filter({ hasNot: this.page.locator('[disabled]') }).first()
    const ariaLabel = (await firstEnabled.getAttribute('aria-label')) ?? ''
    const name = ariaLabel.replace(/^Añadir\s+/, '').replace(/\s+al pedido$/, '')
    await firstEnabled.click()
    return name
  }

  async openCart(): Promise<void> {
    // Floating cart button appears once items are in cart
    await this.page.getByRole('button', { name: /Ver carrito/ }).click()
  }

  /** Full cart flow: CartStep → GuestCount → Review → submit order. */
  async confirmOrder(): Promise<void> {
    // Step 1 — CartStep footer button
    await this.page.getByRole('button', { name: /Confirmar pedido/ }).first().click()

    // Step 2 — GuestCountStep: pick 2 comensales then continue
    await this.page.waitForSelector('text=¿Cuántos comensales?')
    await this.page.getByRole('button', { name: '2 comensales' }).click()
    await this.page.getByRole('button', { name: 'Continuar' }).click()

    // Step 3 — ReviewStep: confirm
    await this.page.waitForSelector('text=Revisa tu pedido')
    await this.page.getByRole('button', { name: /Confirmar pedido/ }).last().click()
  }

  async waitForOrderConfirmation(): Promise<void> {
    await this.page.waitForSelector('text=¡Pedido enviado!', { timeout: 20_000 })
  }

  async navigateToTracking(): Promise<void> {
    await this.page.getByRole('button', { name: 'Seguir el pedido' }).click()
  }
}
