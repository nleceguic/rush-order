import { useCartStore } from '@shared/store/cartStore'

export function useCart() {
  const store = useCartStore()
  return {
    items:           store.items,
    tableId:         store.tableId,
    restaurantId:    store.restaurantId,
    round:           store.round,
    completedOrders: store.completedOrders,
    itemCount:       store.itemCount(),
    total:           store.total(),
    addItem:         store.addItem,
    removeItem:      store.removeItem,
    updateQuantity:  store.updateQuantity,
    clear:           store.clear,
    setTableId:      store.setTableId,
    setRestaurantId: store.setRestaurantId,
    nextRound:       store.nextRound,
  }
}
