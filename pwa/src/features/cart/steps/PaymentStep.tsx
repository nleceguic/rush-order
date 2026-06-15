import type { PaymentMethod } from '../hooks/useCheckout'

interface PaymentStepProps {
  isLoading:        boolean
  error:            string | null
  onlinePayEnabled: boolean
  onSelect:         (method: PaymentMethod) => void
  onBack:           () => void
}

export function PaymentStep({ isLoading, error, onlinePayEnabled, onSelect, onBack }: PaymentStepProps) {
  return (
    <div className="flex flex-col h-full min-h-0">
      <div className="flex items-center gap-3 px-5 py-4 border-b flex-shrink-0">
        <button
          onClick={onBack}
          aria-label="Volver"
          disabled={isLoading}
          className="text-gray-400 hover:text-gray-700 disabled:opacity-40"
        >
          <svg className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M12.707 5.293a1 1 0 010 1.414L9.414 10l3.293 3.293a1 1 0 01-1.414 1.414l-4-4a1 1 0 010-1.414l4-4a1 1 0 011.414 0z" clipRule="evenodd" />
          </svg>
        </button>
        <h2 className="text-lg font-bold text-rush-dark">¿Cómo vas a pagar?</h2>
      </div>

      <div className="flex-1 flex flex-col justify-center px-5 pb-4 overflow-y-auto gap-4">
        {error !== null && (
          <div className="p-3 bg-red-50 border border-red-200 rounded-xl text-sm text-red-700">
            {error}
          </div>
        )}

        {onlinePayEnabled && (
          <PayOption
            icon="💳"
            title="Pagar ahora"
            description="Pago seguro con tarjeta vía Stripe"
            onClick={() => onSelect('online')}
            disabled={isLoading}
            highlight
          />
        )}

        <PayOption
          icon="🧾"
          title="Pagar al finalizar"
          description="Solicita la cuenta al camarero cuando termines"
          onClick={() => onSelect('cash')}
          disabled={isLoading}
        />

        {isLoading && (
          <div className="flex items-center justify-center gap-2 text-sm text-gray-500 mt-2">
            <div className="h-4 w-4 border-2 border-rush-red border-t-transparent rounded-full animate-spin" />
            Confirmando tu pedido…
          </div>
        )}
      </div>
    </div>
  )
}

interface PayOptionProps {
  icon:        string
  title:       string
  description: string
  onClick:     () => void
  disabled:    boolean
  highlight?:  boolean
}

function PayOption({ icon, title, description, onClick, disabled, highlight = false }: PayOptionProps) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className={`
        w-full text-left p-4 rounded-2xl border-2 transition-all active:scale-[0.98]
        ${highlight
          ? 'border-rush-red bg-rush-red/5 hover:bg-rush-red/10'
          : 'border-gray-200 bg-white hover:border-gray-300 hover:bg-gray-50'
        }
        ${disabled ? 'opacity-50 cursor-not-allowed' : ''}
      `}
    >
      <div className="flex items-center gap-3">
        <span className="text-2xl" aria-hidden>{icon}</span>
        <div>
          <p className={`font-semibold ${highlight ? 'text-rush-red' : 'text-rush-dark'}`}>{title}</p>
          <p className="text-sm text-gray-400 mt-0.5">{description}</p>
        </div>
      </div>
    </button>
  )
}
