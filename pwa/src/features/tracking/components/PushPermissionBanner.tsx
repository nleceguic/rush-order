import { useState } from 'react'
import type { PushPermission } from '../hooks/usePushNotifications'

const DISMISSED_KEY = 'push-banner-dismissed'

interface Props {
  permission:        PushPermission
  onRequestPermission: () => void
}

export function PushPermissionBanner({ permission, onRequestPermission }: Props) {
  const [dismissed, setDismissed] = useState(
    () => localStorage.getItem(DISMISSED_KEY) === '1',
  )

  if (permission === 'granted' || permission === 'denied' || dismissed) return null

  const handleDismiss = () => {
    localStorage.setItem(DISMISSED_KEY, '1')
    setDismissed(true)
  }

  const handleActivate = () => {
    onRequestPermission()
    handleDismiss()
  }

  return (
    <div className="rounded-2xl border border-blue-100 bg-blue-50 px-4 py-3 flex items-start gap-3">
      <span className="text-2xl flex-shrink-0 mt-0.5">🔔</span>
      <div className="flex-1 min-w-0">
        <p className="text-sm font-semibold text-rush-dark">Activa las notificaciones</p>
        <p className="text-xs text-gray-500 mt-0.5">
          Te avisaremos cuando tu pedido esté listo, incluso si cierras esta pantalla.
        </p>
        <div className="flex gap-2 mt-2">
          <button
            onClick={handleActivate}
            className="text-xs font-bold px-3 py-1.5 rounded-full bg-rush-red text-white hover:bg-rush-red-hover transition-colors"
          >
            Activar
          </button>
          <button
            onClick={handleDismiss}
            className="text-xs px-3 py-1.5 rounded-full text-gray-500 hover:text-gray-700 transition-colors"
          >
            No, gracias
          </button>
        </div>
      </div>
    </div>
  )
}
