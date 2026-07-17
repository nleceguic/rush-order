import { type Page } from '@playwright/test'

/** Maps domain status values to the Spanish UI labels shown in TrackingPage. */
const STATUS_LABELS: Record<string, string> = {
  New:       'Pendiente',
  Pending:   'Pendiente',
  Confirmed: 'Confirmado',
  Preparing: 'En preparación',
  Ready:     'Listo',
  Served:    'Servido',
  Paid:      'Pagado',
}

export class TrackingPage {
  readonly page: Page

  constructor(page: Page) {
    this.page = page
  }

  async goto(orderId: string): Promise<void> {
    await this.page.goto(`/tracking/${orderId}`)
    await this.page.waitForLoadState('networkidle')
  }

  /**
   * Returns the raw text of the currently-highlighted status in the ProgressStepper.
   * Falls back to any visible status label if no highlighted step is found.
   */
  async getCurrentStatus(): Promise<string> {
    // ProgressStepper marks the active step — try aria-current first
    const ariaCurrent = this.page.locator('[aria-current="step"]').first()
    if (await ariaCurrent.isVisible({ timeout: 2_000 }).catch(() => false)) {
      return (await ariaCurrent.textContent())?.trim() ?? ''
    }

    // Fallback: look for a step styled as active (rush-red or green)
    const activeStep = this.page
      .locator('.text-rush-red, .text-green-600, .text-amber-600')
      .filter({ hasText: /Pendiente|preparación|Listo|Servido|Pagado/ })
      .first()
    return (await activeStep.textContent())?.trim() ?? ''
  }

  /**
   * Waits until the given status label appears anywhere on the page.
   * Accepts both the domain key (e.g. 'Ready') and the Spanish UI label (e.g. 'Listo').
   */
  async waitForStatus(status: string, timeout = 20_000): Promise<void> {
    const label = STATUS_LABELS[status] ?? status
    await this.page.getByText(label, { exact: false }).first().waitFor({ state: 'visible', timeout })
  }

  /** Returns true if the ReadyBanner ("¡Listo!") is currently visible. */
  async isReadyBannerVisible(): Promise<boolean> {
    return this.page.getByText('¡Listo', { exact: false }).isVisible()
  }

  async getEstimatedTime(): Promise<string> {
    const el = this.page.locator('[class*="estimat"], text=/\\d+ minutos?/').first()
    return (await el.textContent())?.trim() ?? ''
  }
}
