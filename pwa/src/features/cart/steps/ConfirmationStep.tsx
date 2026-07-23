import { useNavigate } from 'react-router-dom'
import type { CompletedOrder } from '@shared/types'
import { formatCurrency } from '@shared/utils/format'

interface ConfirmationStepProps {
  order:      CompletedOrder
  onNewRound: () => void
}

export function ConfirmationStep({ order, onNewRound }: ConfirmationStepProps) {
  const navigate = useNavigate()

  const totalItems = order.items.reduce((n, i) => n + i.quantity, 0)

  return (
    <div className="flex flex-col items-center justify-center h-full px-6 py-8 text-center overflow-y-auto scrollbar-hide">
      <div className="mb-5" style={{ animation: 'scale-in 0.35s ease-out both' }}>
        <SuccessCheckmark />
      </div>

      <h2 className="text-2xl font-black text-rush-dark mb-1">¡Pedido enviado!</h2>
      <p className="text-3xl font-black text-rush-red mb-3 tracking-tight">{order.number}</p>

      <p className="text-sm text-gray-500 mb-1">
        Tiempo estimado de preparación
      </p>
      <p className="text-lg font-bold text-rush-dark mb-6">
        {order.estimatedMinutes !== undefined
          ? `${order.estimatedMinutes} minutos`
          : '15–20 minutos'
        }
      </p>

      <p className="text-xs text-gray-400 mb-8">
        {totalItems} {totalItems === 1 ? 'artículo' : 'artículos'} ·{' '}
        <span className="font-medium">{formatCurrency(order.total)}</span>
      </p>

      <div className="w-full space-y-3">
        <button
          onClick={onNewRound}
          className="w-full rounded-2xl bg-rush-red py-4 font-bold text-white text-base hover:bg-rush-red-hover transition-colors"
        >
          Añadir más cosas
        </button>
        <button
          onClick={() => navigate(`/tracking/${order.id}`)}
          className="w-full rounded-2xl border-2 border-gray-200 text-rush-dark font-semibold py-3.5 hover:border-gray-300 hover:bg-gray-50 transition-colors"
        >
          Seguir el pedido
        </button>
      </div>
    </div>
  )
}

function SuccessCheckmark() {
  return (
    <svg viewBox="0 0 64 64" className="h-28 w-28" aria-hidden>
      <circle
        cx="32" cy="32" r="29"
        fill="none" stroke="#E63946" strokeWidth="2" opacity="0.15"
      />
      <circle
        cx="32" cy="32" r="29"
        fill="none" stroke="#E63946" strokeWidth="2.5"
      />
      <path
        d="M 20 32 l 9 9 l 16 -16"
        fill="none" stroke="#E63946" strokeWidth="3.5"
        strokeLinecap="round" strokeLinejoin="round"
      />
    </svg>
  )
}
