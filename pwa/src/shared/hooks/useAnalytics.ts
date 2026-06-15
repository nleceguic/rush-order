declare global {
  interface Window {
    plausible?: (event: string, options?: { props?: Record<string, string | number> }) => void
  }
}

export type AnalyticsEvent =
  | 'menu_viewed'
  | 'product_viewed'
  | 'product_added'
  | 'cart_opened'
  | 'order_placed'
  | 'payment_started'
  | 'payment_completed'
  | 'loyalty_redeemed'
  | 'rating_submitted'
  | 'app_installed'

export function useAnalytics() {
  const track = (event: AnalyticsEvent, props?: Record<string, string | number>): void => {
    if (typeof window === 'undefined' || typeof window.plausible !== 'function') return
    window.plausible(event, props !== undefined ? { props } : undefined)
  }

  return { track }
}
