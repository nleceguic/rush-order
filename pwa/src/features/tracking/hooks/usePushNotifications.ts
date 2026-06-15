import { useState, useEffect, useCallback } from 'react'
import { apiClient } from '@shared/api/axios'
import { ENDPOINTS } from '@shared/api/endpoints'

function urlBase64ToUint8Array(base64String: string): Uint8Array {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4)
  const base64  = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/')
  const rawData = atob(base64)
  return Uint8Array.from([...rawData].map((c) => c.charCodeAt(0)))
}

export type PushPermission = 'default' | 'granted' | 'denied'

export function usePushNotifications() {
  const [permission, setPermission] = useState<PushPermission>('default')
  const [subscribed, setSubscribed] = useState(false)

  useEffect(() => {
    if (!('Notification' in window)) return
    setPermission(Notification.permission as PushPermission)
  }, [])

  const requestPermission = useCallback(async (): Promise<void> => {
    if (!('Notification' in window) || !('serviceWorker' in navigator)) return

    const result = await Notification.requestPermission()
    setPermission(result as PushPermission)
    if (result !== 'granted') return

    const vapidKey = String(import.meta.env.VITE_VAPID_PUBLIC_KEY ?? '')
    if (vapidKey.length === 0) return

    try {
      const reg  = await navigator.serviceWorker.ready
      const sub  = await reg.pushManager.subscribe({
        userVisibleOnly:      true,
        applicationServerKey: urlBase64ToUint8Array(vapidKey),
      })
      await apiClient.post(ENDPOINTS.payments.subscribe, sub.toJSON())
      setSubscribed(true)
    } catch {
      // Permission granted but subscription failed — silently ignore
    }
  }, [])

  return { permission, subscribed, requestPermission }
}
