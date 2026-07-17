import { useState } from 'react'
import { useCartStore } from '@shared/store/cartStore'
import { apiClient } from '@shared/api/axios'
import { useExperiment, CART_RECOMMENDATIONS_EXPERIMENT_KEY } from '@features/recommendations/hooks/useExperiment'
import type { CompletedOrder } from '@shared/types'

export type CheckoutStep   = 'cart' | 'guestCount' | 'review' | 'payment' | 'confirmation'
export type PaymentMethod  = 'online' | 'cash'

interface OrderResponse {
  orderId:           string
  orderNumber:       string
  estimatedMinutes?: number
}

function generateOrderNumber(): string {
  const year = new Date().getFullYear()
  const num  = String(Math.floor(Math.random() * 9000) + 1000)
  return `#${year}-${num}`
}

export interface UseCheckoutReturn {
  step:            CheckoutStep
  guestCount:      number
  generalNotes:    string
  redeemedPoints:  number
  isLoading:       boolean
  error:           string | null
  confirmedOrder:  CompletedOrder | null

  goTo:               (step: CheckoutStep) => void
  setGuestCount:      (n: number) => void
  setGeneralNotes:    (v: string) => void
  setRedeemedPoints:  (n: number) => void
  confirmOrder:       (method: PaymentMethod) => Promise<void>
  reset:              () => void
}

export function useCheckout(): UseCheckoutReturn {
  const [step,            setStep]           = useState<CheckoutStep>('cart')
  const [guestCount,      setGuestCount]     = useState(1)
  const [generalNotes,    setGeneralNotes]   = useState('')
  const [redeemedPoints,  setRedeemedPoints] = useState(0)
  const [isLoading,       setLoading]        = useState(false)
  const [error,           setError]          = useState<string | null>(null)
  const [confirmedOrder,  setConfirmedOrder] = useState<CompletedOrder | null>(null)

  const nextRound    = useCartStore((s) => s.nextRound)
  const restaurantId = useCartStore((s) => s.restaurantId)
  const { track } = useExperiment(restaurantId ?? undefined, CART_RECOMMENDATIONS_EXPERIMENT_KEY)

  const confirmOrder = async (method: PaymentMethod) => {
    setLoading(true)
    setError(null)
    try {
      const cart = useCartStore.getState()
      const { data } = await apiClient.post<OrderResponse>('/orders', {
        restaurantId:  cart.restaurantId,
        tableId:       cart.tableId,
        guestCount,
        round:         cart.round,
        paymentMethod: method,
        items: cart.items.map((i) => ({
          productId: i.productId,
          name:      i.name,
          quantity:  i.quantity,
          unitPrice: i.price,
          modifiers: i.modifiers,
          notes:     i.notes,
        })),
        ...(generalNotes.length > 0    ? { notes: generalNotes }    : {}),
        ...(redeemedPoints > 0        ? { redeemedPoints }          : {}),
      })

      const base: Omit<CompletedOrder, 'estimatedMinutes'> = {
        id:        data.orderId,
        number:    data.orderNumber ?? generateOrderNumber(),
        round:     cart.round,
        items:     [...cart.items],
        total:     cart.total(),
        createdAt: new Date().toISOString(),
      }
      const completed: CompletedOrder = data.estimatedMinutes !== undefined
        ? { ...base, estimatedMinutes: data.estimatedMinutes }
        : base

      nextRound(completed)
      setConfirmedOrder(completed)
      setStep('confirmation')
      track('OrderCompleted', { orderId: data.orderId, cartTotal: completed.total })
    } catch {
      setError('No se pudo confirmar el pedido. Inténtalo de nuevo.')
    } finally {
      setLoading(false)
    }
  }

  const reset = () => {
    setStep('cart')
    setGuestCount(1)
    setGeneralNotes('')
    setRedeemedPoints(0)
    setError(null)
    setConfirmedOrder(null)
    setLoading(false)
  }

  return {
    step, guestCount, generalNotes, redeemedPoints, isLoading, error, confirmedOrder,
    goTo: setStep, setGuestCount, setGeneralNotes, setRedeemedPoints, confirmOrder, reset,
  }
}
