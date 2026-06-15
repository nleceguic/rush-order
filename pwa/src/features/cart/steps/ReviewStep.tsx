import { useCartStore } from '@shared/store/cartStore'
import { CartItemRow } from '../components/CartItemRow'
import { PriceSummary } from '../components/PriceSummary'

interface ReviewStepProps {
  guestCount:   number
  generalNotes: string
  onConfirm:    () => void
  onBack:       () => void
  vatRate?:     number
}

export function ReviewStep({ guestCount, generalNotes, onConfirm, onBack, vatRate }: ReviewStepProps) {
  const items   = useCartStore((s) => s.items)
  const total   = useCartStore((s) => s.total())
  const tableId = useCartStore((s) => s.tableId)
  const round   = useCartStore((s) => s.round)

  return (
    <div className="flex flex-col h-full min-h-0">
      <div className="flex items-center gap-3 px-5 py-4 border-b flex-shrink-0">
        <button onClick={onBack} aria-label="Volver" className="text-gray-400 hover:text-gray-700">
          <svg className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M12.707 5.293a1 1 0 010 1.414L9.414 10l3.293 3.293a1 1 0 01-1.414 1.414l-4-4a1 1 0 010-1.414l4-4a1 1 0 011.414 0z" clipRule="evenodd" />
          </svg>
        </button>
        <h2 className="text-lg font-bold text-rush-dark">Revisa tu pedido</h2>
      </div>

      <div className="flex-1 overflow-y-auto overscroll-contain">
        <div className="px-5 py-3 bg-gray-50 border-b flex gap-6 text-sm">
          {tableId !== null && (
            <div>
              <span className="text-xs text-gray-400 block">Mesa</span>
              <span className="font-semibold text-rush-dark">{tableId}</span>
            </div>
          )}
          <div>
            <span className="text-xs text-gray-400 block">Comensales</span>
            <span className="font-semibold text-rush-dark">{guestCount}</span>
          </div>
          {round > 1 && (
            <div>
              <span className="text-xs text-gray-400 block">Ronda</span>
              <span className="font-semibold text-rush-red">{round}</span>
            </div>
          )}
        </div>

        <div className="px-5 divide-y divide-gray-50">
          {items.map((item) => (
            <CartItemRow key={item.key} item={item} readOnly />
          ))}
        </div>

        {generalNotes.length > 0 && (
          <div className="mx-5 mt-3 p-3 bg-gray-50 rounded-xl">
            <p className="text-xs text-gray-400 mb-0.5">Notas</p>
            <p className="text-sm text-gray-600 italic">"{generalNotes}"</p>
          </div>
        )}

        <div className="px-5 mt-4 pb-4">
          <PriceSummary subtotal={total} vatRate={vatRate} />
        </div>
      </div>

      <div className="flex-shrink-0 border-t bg-white px-5 py-4">
        <button
          onClick={onConfirm}
          className="w-full rounded-2xl bg-rush-red py-4 font-bold text-white text-base hover:bg-rush-red-hover transition-colors"
        >
          Elegir método de pago
        </button>
      </div>
    </div>
  )
}
