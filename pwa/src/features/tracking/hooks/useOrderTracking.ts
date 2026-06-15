import { useState, useEffect, useRef } from 'react'
import { useQuery } from '@tanstack/react-query'
import * as signalR from '@microsoft/signalr'
import { apiClient } from '@shared/api/axios'
import { ENDPOINTS } from '@shared/api/endpoints'

export type OrderStatus = 'New' | 'Preparing' | 'Ready' | 'Served'

export interface TrackingItem {
  id:     string
  name:   string
  status: OrderStatus
}

export interface TrackingData {
  id:               string
  number:           string
  status:           OrderStatus
  estimatedMinutes: number | null
  createdAt:        string
  items:            TrackingItem[]
}

export function useOrderTracking(orderId: string) {
  const [status,    setStatus]    = useState<OrderStatus>('New')
  const [estimated, setEstimated] = useState<number | null>(null)
  const [items,     setItems]     = useState<TrackingItem[]>([])
  const [elapsedSec, setElapsed]  = useState(0)
  const startRef = useRef<number | null>(null)

  const { data: initial, isLoading, isError } = useQuery<TrackingData>({
    queryKey: ['tracking', orderId],
    queryFn: () =>
      apiClient.get<TrackingData>(ENDPOINTS.tracking.order(orderId)).then((r) => r.data),
    enabled: orderId.length > 0,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  })

  // Sync initial data
  useEffect(() => {
    if (initial === undefined) return
    setStatus(initial.status)
    setEstimated(initial.estimatedMinutes)
    setItems(initial.items)
    startRef.current = new Date(initial.createdAt).getTime()
  }, [initial])

  // Elapsed timer
  useEffect(() => {
    const tick = setInterval(() => {
      if (startRef.current !== null) {
        setElapsed(Math.floor((Date.now() - startRef.current) / 1000))
      }
    }, 1000)
    return () => clearInterval(tick)
  }, [])

  // SignalR — no auth (public hub for QR customers)
  useEffect(() => {
    if (orderId.length === 0) return

    const conn = new signalR.HubConnectionBuilder()
      .withUrl(ENDPOINTS.hubs.orderTracking)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    conn.on('OrderStatusChanged', (id: unknown, newStatus: unknown) => {
      if (id === orderId) setStatus(newStatus as OrderStatus)
    })

    conn.on('ItemStatusChanged', (ordId: unknown, itemId: unknown, itemStatus: unknown) => {
      if (ordId !== orderId) return
      setItems((prev) =>
        prev.map((it) =>
          it.id === itemId ? { ...it, status: itemStatus as OrderStatus } : it,
        ),
      )
    })

    conn.on('EstimatedTimeUpdated', (id: unknown, minutes: unknown) => {
      if (id === orderId) setEstimated(minutes as number)
    })

    void conn.start()
    return () => { void conn.stop() }
  }, [orderId])

  return { initial, isLoading, isError, status, estimatedMinutes: estimated, items, elapsedSec }
}
