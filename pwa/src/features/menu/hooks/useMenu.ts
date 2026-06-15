import { useEffect } from 'react'
import { useGet } from '@shared/hooks/useApi'
import type { MenuCategory } from '../types'

interface MenuResponse {
  categories: MenuCategory[]
}

export function useMenu(restaurantId: string | undefined, locale = 'es') {
  // Prefetch images for first 2 categories once data arrives
  const query = useGet<MenuResponse>(
    ['menu', restaurantId, locale],
    restaurantId !== undefined ? `/v1/menu/public/${restaurantId}?locale=${locale}` : '',
    { enabled: restaurantId !== undefined, staleTime: 1000 * 60 * 5 },
  )

  useEffect(() => {
    if (query.data === undefined) return
    const urls = query.data.categories
      .slice(0, 2)
      .flatMap((cat) => cat.products.flatMap((p) => (p.imageUrl !== undefined ? [p.imageUrl] : [])))
      .slice(0, 12)

    for (const url of urls) {
      const img = new Image()
      img.src = url
    }
  }, [query.data])

  return query
}
