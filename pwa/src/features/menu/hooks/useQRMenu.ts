import { useEffect } from 'react'
import { useGet } from '@shared/hooks/useApi'
import { useCartStore } from '@shared/store/cartStore'
import type { QRData } from '../types'

export function useQRMenu(token: string) {
  const setTableId      = useCartStore((s) => s.setTableId)
  const setRestaurantId = useCartStore((s) => s.setRestaurantId)

  const query = useGet<QRData>(
    ['qr', token],
    `/v1/qr/${token}`,
    { enabled: token.length > 0, staleTime: Infinity },
  )

  useEffect(() => {
    const tableId = query.data?.tableId
    if (tableId !== undefined) setTableId(tableId)
  }, [query.data?.tableId, setTableId])

  useEffect(() => {
    const restaurantId = query.data?.restaurantId
    if (restaurantId !== undefined) setRestaurantId(restaurantId)
  }, [query.data?.restaurantId, setRestaurantId])

  return query
}
