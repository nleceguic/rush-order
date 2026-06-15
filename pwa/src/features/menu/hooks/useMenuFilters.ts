import { useState, useCallback } from 'react'
import type { DietFilter, MenuCategory, MenuProduct } from '../types'

export function useMenuFilters(categories: MenuCategory[]) {
  const [activeFilters, setActiveFilters] = useState<Set<DietFilter>>(new Set())

  const toggleFilter = useCallback((filter: DietFilter) => {
    setActiveFilters((prev) => {
      const next = new Set(prev)
      if (next.has(filter)) next.delete(filter)
      else next.add(filter)
      return next
    })
  }, [])

  const clearFilters = useCallback(() => setActiveFilters(new Set()), [])

  const matchesFilters = (product: MenuProduct): boolean => {
    if (activeFilters.size === 0) return true
    for (const filter of activeFilters) {
      if (filter === 'vegetarian' && product.tags?.includes('vegetarian') !== true) return false
      if (filter === 'vegan'      && product.tags?.includes('vegan')      !== true) return false
      if (filter === 'spicy'      && product.tags?.includes('spicy')      !== true) return false
      if (filter === 'glutenFree' && product.allergens?.includes('gluten') === true) return false
      if (filter === 'dairyFree'  && product.allergens?.includes('dairy')  === true) return false
    }
    return true
  }

  const filteredCategories = categories.map((cat) => ({
    ...cat,
    products: cat.products.filter(matchesFilters),
  }))

  return { activeFilters, toggleFilter, clearFilters, filteredCategories }
}
