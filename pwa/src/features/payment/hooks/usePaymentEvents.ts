import { useState, useEffect } from 'react'
import * as signalR from '@microsoft/signalr'
import { ENDPOINTS } from '@shared/api/endpoints'

export function usePaymentEvents(orderId: string, enabled: boolean) {
  const [paidCount, setPaidCount] = useState(0)

  useEffect(() => {
    if (!enabled || orderId.length === 0) return

    const conn = new signalR.HubConnectionBuilder()
      .withUrl(ENDPOINTS.hubs.payments)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    conn.on('PaymentReceived', (id: unknown, count: unknown) => {
      if (id === orderId) setPaidCount(count as number)
    })

    void conn.start()
    return () => { void conn.stop() }
  }, [orderId, enabled])

  return paidCount
}
