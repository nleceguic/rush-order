import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@shared/api/axios'
import { ENDPOINTS } from '@shared/api/endpoints'
import type { Promotion } from '@shared/types'

export function usePromotions(restaurantId: string | undefined) {
  return useQuery<Promotion[]>({
    queryKey:   ['promotions', restaurantId],
    queryFn:    async () => {
      if (restaurantId === undefined) return []
      const { data } = await apiClient.get<Promotion[]>(ENDPOINTS.promotions.active(restaurantId))
      return data
    },
    enabled:         restaurantId !== undefined,
    staleTime:       5 * 60 * 1000, // 5 min
    refetchInterval: 5 * 60 * 1000,
  })
}
