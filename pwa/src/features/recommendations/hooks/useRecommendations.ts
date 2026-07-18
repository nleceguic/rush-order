import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@shared/api/axios'
import { ENDPOINTS } from '@shared/api/endpoints'

export interface Recommendation {
  productId:       string
  name:            string
  imageUrl?:       string
  price:           number
  currency:        string
  reason:          string
  confidenceScore: number
}

interface RecommendationsApiResponse {
  recommendations: Recommendation[]
}

// Backend caches this by restaurant + cart signature for 5 min (not per
// customer) — see GetRecommendationsQueryHandler — so staleTime here just
// avoids redundant requests within the same PWA session.
export function useRecommendations(restaurantId: string | undefined, cartProductIds: string[]) {
  const productIdsParam = [...cartProductIds].sort().join(',')

  return useQuery<Recommendation[]>({
    queryKey: ['recommendations', restaurantId, productIdsParam],
    queryFn:  async () => {
      const { data } = await apiClient.get<RecommendationsApiResponse>(ENDPOINTS.recommendations.list, {
        params: {
          restaurantId,
          ...(productIdsParam.length > 0 ? { productIds: productIdsParam } : {}),
        },
      })
      return data.recommendations
    },
    enabled:   restaurantId !== undefined,
    staleTime: 5 * 60 * 1000,
  })
}
