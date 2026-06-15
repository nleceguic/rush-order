import { useEffect, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '@shared/store/authStore'

interface SignalROptions {
  hubUrl:   string
  events:   Record<string, (...args: unknown[]) => void>
  enabled?: boolean
}

export function useSignalR({ hubUrl, events, enabled = true }: SignalROptions) {
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  const accessToken   = useAuthStore((s) => s.accessToken)

  useEffect(() => {
    if (!enabled || accessToken === null) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => accessToken,
        transport: signalR.HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connectionRef.current = connection

    for (const [event, handler] of Object.entries(events)) {
      connection.on(event, handler)
    }

    connection.start().catch((err: unknown) => {
      console.error('[SignalR] connection failed:', err)
    })

    return () => {
      void connection.stop()
    }
    // events is intentionally omitted — callers should memoize if needed
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hubUrl, enabled, accessToken])

  return connectionRef
}
