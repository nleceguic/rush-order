import { useEffect, useRef } from 'react'
import { useSearchParams, useNavigate, Link } from 'react-router-dom'

export default function PaymentResultPage() {
  const [params]  = useSearchParams()
  const navigate  = useNavigate()
  const redirected = useRef(false)

  const status  = params.get('redirect_status') ?? 'failed'
  const orderId = params.get('orderId') ?? ''
  const succeeded = status === 'succeeded'

  useEffect(() => {
    if (!succeeded || redirected.current) return
    redirected.current = true
    const t = setTimeout(() => {
      navigate(`/tracking/${orderId}`, { replace: true })
    }, 2000)
    return () => clearTimeout(t)
  }, [succeeded, orderId, navigate])

  if (succeeded) {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center px-4 text-center gap-6">
        {/* Animated checkmark */}
        <div className="relative h-24 w-24">
          <svg viewBox="0 0 52 52" className="h-full w-full" aria-hidden>
            <circle
              cx="26" cy="26" r="25"
              fill="none"
              stroke="#E63946"
              strokeWidth="2"
              pathLength="1"
              strokeDasharray="1"
              strokeDashoffset="1"
              style={{ animation: 'check-circle 0.4s ease forwards' }}
            />
            <path
              d="M14 27 l8 8 16-16"
              fill="none"
              stroke="#E63946"
              strokeWidth="2.5"
              strokeLinecap="round"
              strokeLinejoin="round"
              pathLength="1"
              strokeDasharray="1"
              strokeDashoffset="1"
              style={{ animation: 'check-draw 0.4s ease 0.35s forwards' }}
            />
          </svg>
        </div>

        <div>
          <h1 className="text-2xl font-black text-rush-dark">¡Pago completado!</h1>
          <p className="text-gray-500 mt-1 text-sm">
            Redirigiendo al seguimiento de tu pedido…
          </p>
        </div>

        <div className="h-1 w-32 rounded-full bg-gray-100 overflow-hidden">
          <div
            className="h-full bg-rush-red rounded-full"
            style={{ animation: 'grow-bar 2s linear forwards' }}
          />
        </div>

        <style>{`
          @keyframes grow-bar {
            from { width: 0%; }
            to   { width: 100%; }
          }
        `}</style>
      </div>
    )
  }

  const isCanceled = status === 'canceled'

  return (
    <div className="min-h-screen flex flex-col items-center justify-center px-4 text-center gap-6">
      <div className="h-20 w-20 rounded-full bg-red-50 flex items-center justify-center">
        <svg className="h-10 w-10 text-rush-red" viewBox="0 0 24 24" fill="none" stroke="currentColor" aria-hidden>
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
            d="M12 9v4m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"
          />
        </svg>
      </div>

      <div>
        <h1 className="text-2xl font-black text-rush-dark">
          {isCanceled ? 'Pago cancelado' : 'Error en el pago'}
        </h1>
        <p className="text-gray-500 mt-1 text-sm max-w-xs mx-auto">
          {isCanceled
            ? 'Cancelaste el pago. Puedes intentarlo de nuevo cuando quieras.'
            : 'No se pudo procesar el pago. Verifica tu tarjeta e inténtalo de nuevo.'}
        </p>
      </div>

      {orderId.length > 0 && (
        <div className="flex flex-col gap-3 w-full max-w-xs">
          <Link
            to={`/payment/${orderId}`}
            replace
            className="w-full rounded-2xl bg-rush-red py-3.5 font-bold text-white text-center hover:bg-rush-red-hover transition-colors"
          >
            Intentar de nuevo
          </Link>
          <Link
            to="/"
            className="w-full rounded-2xl border border-gray-200 py-3.5 font-medium text-gray-600 text-center hover:border-gray-300 transition-colors"
          >
            Volver al menú
          </Link>
        </div>
      )}
    </div>
  )
}
