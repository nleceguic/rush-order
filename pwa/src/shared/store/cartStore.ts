import { create } from 'zustand'
import { persist, createJSONStorage } from 'zustand/middleware'
import type { CartItem, CartModifier, CompletedOrder } from '@shared/types'

function makeKey(productId: string, notes: string | undefined, modifiers: CartModifier[]): string {
  const modStr = [...modifiers]
    .sort((a, b) => a.name.localeCompare(b.name))
    .map((m) => `${m.name}:${m.value}`)
    .join('|')
  return `${productId}::${notes ?? ''}::${modStr}`
}

interface AddItemInput {
  productId:  string
  name:       string
  price:      number
  quantity?:  number
  notes?:     string
  modifiers?: CartModifier[]
}

interface CartStore {
  items:           CartItem[]
  restaurantId:    string | null
  tableId:         string | null
  round:           number
  completedOrders: CompletedOrder[]

  addItem:         (input: AddItemInput) => void
  removeItem:      (key: string) => void
  updateQuantity:  (key: string, quantity: number) => void
  clear:           () => void
  setRestaurantId: (id: string) => void
  setTableId:      (id: string) => void
  nextRound:       (order: CompletedOrder) => void

  total:     () => number
  itemCount: () => number
}

export const useCartStore = create<CartStore>()(
  persist(
    (set, get) => ({
      items:           [],
      restaurantId:    null,
      tableId:         null,
      round:           1,
      completedOrders: [],

      addItem: (input) =>
        set((s) => {
          const qty       = input.quantity ?? 1
          const notes     = input.notes
          const modifiers = input.modifiers ?? []
          const key       = makeKey(input.productId, notes, modifiers)
          const existing  = s.items.find((i) => i.key === key)

          if (existing !== undefined) {
            return {
              items: s.items.map((i) =>
                i.key === key ? { ...i, quantity: i.quantity + qty } : i,
              ),
            }
          }

          const base = { key, productId: input.productId, name: input.name, price: input.price, quantity: qty, modifiers }
          const newItem: CartItem = notes !== undefined ? { ...base, notes } : base
          return { items: [...s.items, newItem] }
        }),

      removeItem: (key) =>
        set((s) => ({ items: s.items.filter((i) => i.key !== key) })),

      updateQuantity: (key, quantity) =>
        set((s) => ({
          items: quantity <= 0
            ? s.items.filter((i) => i.key !== key)
            : s.items.map((i) => i.key === key ? { ...i, quantity } : i),
        })),

      clear:           () => set({ items: [] }),
      setRestaurantId: (id) => set({ restaurantId: id }),
      setTableId:      (id) => set({ tableId: id }),

      nextRound: (order) =>
        set((s) => ({
          items:           [],
          round:           s.round + 1,
          completedOrders: [...s.completedOrders, order],
        })),

      total:     () => get().items.reduce((sum, i) => sum + i.price * i.quantity, 0),
      itemCount: () => get().items.reduce((n, i) => n + i.quantity, 0),
    }),
    {
      name:    'rush-order-cart',
      storage: createJSONStorage(() => sessionStorage),
      partialize: (s) => ({
        items:           s.items,
        restaurantId:    s.restaurantId,
        tableId:         s.tableId,
        round:           s.round,
        completedOrders: s.completedOrders,
      }),
    },
  ),
)
