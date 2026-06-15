import { useEffect, useRef } from 'react'
import { useParams, Link } from 'react-router-dom'
import confetti from 'canvas-confetti'
import { Spinner } from '@shared/components/Spinner'
import { useOrderTracking }     from './hooks/useOrderTracking'
import { usePushNotifications } from './hooks/usePushNotifications'
import { useRatingTimer }       from './hooks/useRatingTimer'
import { ProgressStepper }      from './components/ProgressStepper'
import { ReadyBanner }          from './components/ReadyBanner'
import { PushPermissionBanner } from './components/PushPermissionBanner'
import { RatingSheet }          from './components/RatingSheet'
import type { OrderStatus } from './hooks/useOrderTracking'

const ITEM_STATUS_LABEL: Record<OrderStatus, string> = {
  New:       'Pendiente',
  Preparing: 'En preparación',
  Ready:     'Listo',
  Served:    'Servido',
}

const ITEM_STATUS_COLOR: Record<OrderStatus, string> = {
  New:       'bg-gray-100 text-gray-500',
  Preparing: 'bg-amber-100 text-amber-700',
  Ready:     'bg-green-100 text-green-700',
  Served:    'bg-blue-100 text-blue-700',
}

function formatElapsed(sec: number): string {
  const m = Math.floor(sec / 60)
  const s = sec % 60
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
}

export default function TrackingPage() {
  const { orderId = '' } = useParams<{ orderId: string }>()

  const {
    initial, isLoading, isError,
    status, estimatedMinutes, items, elapsedSec,
  } = useOrderTracking(orderId)

  const { permission, requestPermission } = usePushNotifications()
  const { show: showRating, dismiss: dismissRating } = useRatingTimer(status)

  // Confetti + vibration + local notification — fire once on Ready
  const readyFiredRef = useRef(false)
  useEffect(() => {
    if (status !== 'Ready' || readyFiredRef.current) return
    readyFiredRef.current = true

    void confetti({
      particleCount: 120,
      spread: 80,
      origin: { y: 0.55 },
      colors: ['#E63946', '#1D3557', '#FFD700', '#ffffff'],
    })

    if ('vibrate' in navigator) {
      navigator.vibrate([200, 100, 200, 100, 400])
    }

    if (Notification.permission === 'granted') {
      void navigator.serviceWorker.ready.then((reg) => {
        void reg.showNotification('¡Tu pedido está listo! 🔔', {
          body: 'El camarero se dirige a tu mesa.',
          icon: '/icons/icon-192.png',
          badge: '/icons/badge-72.png',
        })
      })
    }
  }, [status])

  // ── Loading / Error ───────────────────────────────────────────
  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <Spinner size="lg" />
      </div>
    )
  }

  if (isError || initial === undefined) {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center gap-4 px-6 text-center">
        <p className="text-rush-red font-semibold">No pudimos cargar tu pedido.</p>
        <Link to="/" className="text-sm text-gray-500 underline">Volver al inicio</Link>
      </div>
    )
  }

  // ── Main UI ───────────────────────────────────────────────────
  return (
    <>
      {/* Header */}
      <header className="sticky top-0 z-30 bg-white border-b px-4 py-3 flex items-center gap-3 shadow-sm">
        <Link to="/" aria-label="Volver al menú">
          <svg className="h-6 w-6 text-gray-600" viewBox="0 0 24 24" fill="none" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
        </Link>
        <div className="flex-1">
          <h1 className="text-base font-bold text-rush-dark leading-tight">Seguimiento del pedido</h1>
          <p className="text-xs text-gray-400">#{initial.number}</p>
        </div>
        {/* Elapsed timer */}
        <div className="text-right">
          <p className="text-xs text-gray-400">Tiempo</p>
          <p className="text-sm font-mono font-semibold text-rush-dark">{formatElapsed(elapsedSec)}</p>
        </div>
      </header>

      <main className="max-w-md mx-auto px-4 py-6 space-y-6">

        {/* Ready banner */}
        <ReadyBanner visible={status === 'Ready'} />

        {/* Push permission banner */}
        <PushPermissionBanner
          permission={permission}
          onRequestPermission={requestPermission}
        />

        {/* Progress stepper */}
        <section aria-label="Estado del pedido">
          <ProgressStepper status={status} />
        </section>

        {/* Estimated time card */}
        <div className="rounded-2xl border border-gray-100 bg-gray-50 px-5 py-4 flex items-center justify-between">
          <div>
            <p className="text-xs text-gray-400">Tiempo estimado</p>
            <p className="text-lg font-bold text-rush-dark">
              {estimatedMinutes !== null
                ? `${estimatedMinutes} min`
                : status === 'Ready' || status === 'Served'
                  ? '—'
                  : 'Calculando…'}
            </p>
          </div>
          <div className="text-right">
            <p className="text-xs text-gray-400">Tiempo transcurrido</p>
            <p className="text-lg font-bold text-rush-dark font-mono">{formatElapsed(elapsedSec)}</p>
          </div>
        </div>

        {/* Items list */}
        {items.length > 0 && (
          <section>
            <h2 className="text-sm font-semibold text-gray-500 mb-3 uppercase tracking-wide">
              Tu pedido
            </h2>
            <ul className="space-y-2">
              {items.map((item) => (
                <li
                  key={item.id}
                  className="flex items-center justify-between rounded-xl border border-gray-100 bg-white px-4 py-3"
                >
                  <p className="text-sm font-medium text-rush-dark">{item.name}</p>
                  <span className={`text-xs font-semibold px-2.5 py-1 rounded-full ${ITEM_STATUS_COLOR[item.status]}`}>
                    {ITEM_STATUS_LABEL[item.status]}
                  </span>
                </li>
              ))}
            </ul>
          </section>
        )}

        {/* CTA when served */}
        {status === 'Served' && (
          <div
            className="rounded-2xl bg-green-50 border border-green-200 px-5 py-4 text-center space-y-1"
            style={{ animation: 'scale-in 0.35s ease' }}
          >
            <p className="text-2xl">🍽️</p>
            <p className="font-bold text-green-700">¡Buen provecho!</p>
            <p className="text-xs text-gray-500">Tu pedido ha sido entregado.</p>
          </div>
        )}

        {/* Extra padding for bottom sheets */}
        <div className="h-8" />
      </main>

      {/* Rating sheet — 30s after Served */}
      {showRating && (
        <RatingSheet orderId={orderId} onClose={dismissRating} />
      )}
    </>
  )
}
