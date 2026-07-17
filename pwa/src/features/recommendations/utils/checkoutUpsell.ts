import type { MenuCategory, MenuProduct } from '@features/menu/types'
import type { CartItem } from '@shared/types'

// Heuristic category-type detection by name — there's no CategoryType field
// in the menu API yet, so this matches the Spanish/English names restaurants
// actually use. Good enough for a nudge; worth promoting to a real field on
// Category if this needs to be precise later.
const DESSERT_KEYWORDS = ['postre', 'dessert', 'dulce', 'tarta', 'helado']
const DRINK_KEYWORDS = ['bebida', 'drink', 'refresco', 'cerveza', 'vino', 'cóctel', 'coctel', 'copa', 'café', 'infusion', 'infusión']

const LOW_TICKET_THRESHOLD = 15

export interface UpsellPrompt {
  headline:  string
  products:  MenuProduct[]
}

function matchesCategory(name: string, keywords: string[]): boolean {
  const lower = name.toLowerCase()
  return keywords.some((keyword) => lower.includes(keyword))
}

function availableUnpicked(categories: MenuCategory[], cartProductIds: Set<string>): MenuProduct[] {
  return categories
    .flatMap((c) => c.products)
    .filter((p) => p.isAvailable && !cartProductIds.has(p.id))
}

// Antes de confirmar el pedido: sin postre pero hay postres disponibles ->
// "¿Olvidaste el postre?"; solo platos pero sin bebida -> "¿Algo para
// beber?"; ticket bajo -> sugerencias de platos más caros y populares.
// Returns null when none of the conditions apply (nothing to nudge).
export function getCheckoutUpsellPrompt(
  categories: MenuCategory[],
  cartItems: CartItem[],
  cartTotal: number,
): UpsellPrompt | null {
  const cartProductIds = new Set(cartItems.map((i) => i.productId))

  const dessertCategories = categories.filter((c) => matchesCategory(c.name, DESSERT_KEYWORDS))
  const drinkCategories = categories.filter((c) => matchesCategory(c.name, DRINK_KEYWORDS))

  const hasDessertInCart = dessertCategories.some((c) => c.products.some((p) => cartProductIds.has(p.id)))
  const hasDrinkInCart = drinkCategories.some((c) => c.products.some((p) => cartProductIds.has(p.id)))

  const availableDesserts = availableUnpicked(dessertCategories, cartProductIds)
  if (!hasDessertInCart && availableDesserts.length > 0) {
    return { headline: '¿Olvidaste el postre?', products: availableDesserts.slice(0, 3) }
  }

  const availableDrinks = availableUnpicked(drinkCategories, cartProductIds)
  if (!hasDrinkInCart && availableDrinks.length > 0) {
    return { headline: '¿Algo para beber?', products: availableDrinks.slice(0, 3) }
  }

  if (cartTotal < LOW_TICKET_THRESHOLD) {
    // Case-insensitive: the API sends the C# enum's PascalCase name ("Popular"),
    // while ProductTag here is typed lowercase — compare loosely so this
    // doesn't silently never match.
    const popular = availableUnpicked(categories, cartProductIds)
      .filter((p) => (p.tags ?? []).some((t) => String(t).toLowerCase() === 'popular'))
      .sort((a, b) => b.price - a.price)
      .slice(0, 3)

    if (popular.length > 0) {
      return { headline: 'Sugerencias para completar tu pedido', products: popular }
    }
  }

  return null
}
