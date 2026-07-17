import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@shared/api/axios'
import { ENDPOINTS } from '@shared/api/endpoints'
import { getDeviceFingerprint } from '@shared/utils/deviceFingerprint'

export type ExperimentVariant = 'A' | 'B'
export type ExperimentEventType = 'Exposure' | 'SuggestionAdded' | 'OrderCompleted'

// Primer experimento: "Recomendaciones en el carrito" (variante B) vs "Sin
// recomendaciones" (variante A). Shared so CartStep and useCheckout always
// track/read the same experiment.
export const CART_RECOMMENDATIONS_EXPERIMENT_KEY = 'cart-recommendations'

interface AssignmentApiResponse {
  data: { experimentKey: string; variant: ExperimentVariant; bucket: number }
}

interface TrackExtra {
  orderId?:   string
  cartTotal?: number
}

// Bucket (0-99) is hashed from the device fingerprint server-side — same
// fingerprint always gets the same variant, nothing is persisted client-side
// beyond the fingerprint itself (see deviceFingerprint.ts).
export function useExperiment(restaurantId: string | undefined, experimentKey: string) {
  const deviceFingerprint = getDeviceFingerprint()

  const assignment = useQuery<ExperimentVariant>({
    queryKey: ['experiment-assignment', restaurantId, experimentKey, deviceFingerprint],
    queryFn:  async () => {
      const { data } = await apiClient.get<AssignmentApiResponse>(
        ENDPOINTS.experiments.assignment(experimentKey),
        { params: { restaurantId, deviceFingerprint } },
      )
      return data.data.variant
    },
    enabled:   restaurantId !== undefined,
    staleTime: Infinity, // deterministic per device+experiment — no need to ever refetch
  })

  const track = (eventType: ExperimentEventType, extra?: TrackExtra) => {
    if (restaurantId === undefined || assignment.data === undefined) return
    void apiClient.post(ENDPOINTS.experiments.events, {
      restaurantId,
      experimentKey,
      variant: assignment.data,
      deviceFingerprint,
      eventType,
      orderId:   extra?.orderId,
      cartTotal: extra?.cartTotal,
    })
  }

  return { variant: assignment.data, isLoading: assignment.isLoading, track }
}
