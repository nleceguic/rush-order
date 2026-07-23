import { useState, useEffect, useMemo } from 'react'
import type { MenuCategory, MenuProduct } from '../types'

export interface SearchGroup {
  categoryId:   string
  categoryName: string
  products:     MenuProduct[]
}

export function useMenuSearch(categories: MenuCategory[]) {
  const [query, setQuery]           = useState('')
  const [debouncedQuery, setDebounced] = useState('')

  useEffect(() => {
    const t = setTimeout(() => setDebounced(query.trim()), 300)
    return () => clearTimeout(t)
  }, [query])

  const results = useMemo<SearchGroup[]>(() => {
    if (debouncedQuery.length < 2) return []
    const lower = debouncedQuery.toLowerCase()
    return categories
      .map((cat) => ({
        categoryId:   cat.id,
        categoryName: cat.name,
        products:     cat.products.filter(
          (p) =>
            p.name.toLowerCase().includes(lower) ||
            p.description?.toLowerCase().includes(lower) === true,
        ),
      }))
      .filter((g) => g.products.length > 0)
  }, [categories, debouncedQuery])

  return {
    query,
    setQuery,
    debouncedQuery,
    results,
    isSearching: debouncedQuery.length >= 2,
  }
}
