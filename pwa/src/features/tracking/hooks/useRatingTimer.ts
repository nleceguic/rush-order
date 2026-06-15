import { useState, useEffect, useRef } from 'react'
import type { OrderStatus } from './useOrderTracking'

export function useRatingTimer(status: OrderStatus, delayMs = 30_000) {
  const [show, setShow]   = useState(false)
  const firedRef          = useRef(false)

  useEffect(() => {
    if (status !== 'Served' || firedRef.current) return
    firedRef.current = true
    const t = setTimeout(() => setShow(true), delayMs)
    return () => clearTimeout(t)
  }, [status, delayMs])

  const dismiss = () => setShow(false)

  return { show, dismiss }
}
