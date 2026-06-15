import { useState, useEffect, useCallback } from 'react'
import { apiClient } from '@shared/api/axios'
import { ENDPOINTS } from '@shared/api/endpoints'

export interface PaymentIntentData {
  clientSecret:    string
  paymentIntentId: string
  amountCents:     number
  currency:        string
}

export function usePaymentIntent(orderId: string) {
  const [data,      setData]    = useState<PaymentIntentData | null>(null)
  const [isLoading, setLoading] = useState(true)
  const [error,     setError]   = useState<string | null>(null)

  useEffect(() => {
    if (orderId.length === 0) return
    setLoading(true)
    setError(null)
    apiClient
      .post<PaymentIntentData>(ENDPOINTS.payments.intent, { orderId })
      .then((res) => { setData(res.data); setLoading(false) })
      .catch(() => { setError('No se pudo inicializar el pago. Inténtalo de nuevo.'); setLoading(false) })
  }, [orderId])

  const updateIntent = useCallback(async (tipCents: number, splits: number): Promise<void> => {
    if (data === null) return
    try {
      const res = await apiClient.patch<PaymentIntentData>(
        ENDPOINTS.payments.intentById(data.paymentIntentId),
        { tipCents, splits },
      )
      setData(res.data)
    } catch {
      // non-critical — keep existing data
    }
  }, [data])

  return { data, isLoading, error, updateIntent }
}
