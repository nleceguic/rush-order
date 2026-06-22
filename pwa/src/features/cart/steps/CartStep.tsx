import { useCartStore } from '@shared/store/cartStore'
import { CartItemRow } from '../components/CartItemRow'
import { PriceSummary } from '../components/PriceSummary'
import { PointsRedemption } from '../components/PointsRedemption'
import { formatCurrency } from '@shared/utils/format'

interface CartStepProps {
  generalNotes:          string
  onNotesChange:         (v: string) => void
  onCheckout:            () => void
  onClose:               () => void
  vatRate?:              number
  availableProductIds?:  Set<string>
  redeemedPoints:        number
  onRedemptionChange:    (points: number) => void
}

export function CartStep({
  generalNotes, onNotesChange, onCheckout, onClose,
  vatRate, availableProductIds, redeemedPoints, onRedemptionChange,
}: CartStepProps) {
  const items = useCartStore((s) => s.items)
  const total = useCartStore((s) => s.total())
  const clear = useCartStore((s) => s.clear)
  const round = useCartStore((s) => s.round)
  const discountEuros = redeemedPoints / 100

  const unavailable = availableProductIds !== undefined
    ? items.filter((i) => !availableProductIds.has(i.productId))
    : []

  return (
    <div className="flex flex-col h-full min-h-0">
      {/* Header */}
      <div className="flex items-center justify-between px-5 py-4 border-b flex-shrink-0">
        <div>
          <h2 className="text-lg font-bold text-rush-dark">Tu pedido</h2>
          {round > 1 && (
            <span className="text-xs text-rush-red font-medium">Ronda {round}</span>
          )}
        </div>
        <button
          onClick={onClose}
          className="h-8 w-8 rounded-full bg-gray-100 flex items-center justify-center hover:bg-gray-200"
          aria-label="Cerrar"
        >
          <svg className="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
          </svg>
        </button>
      </div>

      {/* Scrollable body */}
      <div className="flex-1 overflow-y-auto overscroll-contain">
        {unavailable.length > 0 && (
          <div className="mx-5 mt-4 p-3 bg-amber-50 border border-amber-200 rounded-xl text-sm text-amber-800">
            <strong>Atención:</strong>{' '}
            {unavailable.map((i) => i.name).join(', ')}{' '}
            {unavailable.length === 1 ? 'ya no está disponible' : 'ya no están disponibles'}.
            Elimínalos antes de continuar.
          </div>
        )}

        <div className="px-5 divide-y divide-gray-50">
          {items.map((item) => (
            <CartItemRow key={item.key} item={item} />
          ))}
        </div>

        <div className="flex justify-end px-5 mt-1">
          <button
            onClick={clear}
            className="text-xs text-gray-400 hover:text-red-500 transition-colors py-1"
          >
            Vaciar carrito
          </button>
        </div>

        <div className="px-5 mt-3">
          <label
            htmlFor="order-notes"
            className="text-xs font-semibold text-gray-400 uppercase tracking-wider block mb-1.5"
          >
            Notas del pedido
          </label>
          <textarea
            id="order-notes"
            rows={2}
            value={generalNotes}
            onChange={(e) => onNotesChange(e.target.value)}
            placeholder="Alergias, preferencias generales…"
            className="w-full rounded-xl border-gray-200 text-sm resize-none focus:ring-rush-red"
          />
        </div>

        <div className="px-5 mt-3 pb-1">
          <PointsRedemption
            cartTotal={total}
            redeemedPoints={redeemedPoints}
            onChange={onRedemptionChange}
          />
        </div>

        <div className="px-5 mt-3 pb-4">
          <PriceSummary
            subtotal={total}
            vatRate={vatRate}
            discount={discountEuros > 0 ? discountEuros : undefined}
          />
        </div>
      </div>

      {/* Footer */}
      <div className="flex-shrink-0 border-t bg-white px-5 py-4">
        <button
          onClick={onCheckout}
          disabled={items.length === 0 || unavailable.length > 0}
          className="w-full rounded-2xl bg-rush-red py-4 font-bold text-white text-base hover:bg-rush-red-hover disabled:opacity-40 transition-colors"
        >
          Confirmar pedido · {formatCurrency(total - discountEuros)}
        </button>
        <button
          onClick={onClose}
          className="w-full text-center text-sm text-gray-400 mt-2 py-1 hover:text-gray-600"
        >
          Seguir mirando el menú
        </button>
      </div>
    </div>
  )
}
