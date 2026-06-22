import type { ChangeEvent } from 'react'
import { formatCurrency } from '@shared/utils/format'

interface SplitBillSheetProps {
  open:           boolean
  onClose:        () => void
  totalCents:     number
  splits:         number
  paidCount:      number
  onApply:        (splits: number) => void
}

export function SplitBillSheet({
  open, onClose, totalCents, splits, paidCount, onApply,
}: SplitBillSheetProps) {
  if (!open) return null

  const perPersonCents = Math.ceil(totalCents / splits)
  const perPersonEuros = formatCurrency(perPersonCents / 100)

  const handleChange = (e: ChangeEvent<HTMLInputElement>) => {
    const val = parseInt(e.target.value, 10)
    if (!isNaN(val) && val >= 2 && val <= 10) onApply(val)
  }

  return (
    <>
      <div
        className="fixed inset-0 z-40 bg-black/40"
        onClick={onClose}
        aria-hidden
      />
      <div
        role="dialog"
        aria-modal
        aria-label="Dividir la cuenta"
        className="fixed inset-x-0 bottom-0 z-50 rounded-t-3xl bg-white shadow-2xl px-5 pt-4 pb-8"
      >
        <div className="flex justify-center mb-3">
          <div className="h-1 w-10 rounded-full bg-gray-200" aria-hidden />
        </div>

        <h3 className="text-lg font-bold text-rush-dark mb-1 text-center">Dividir la cuenta</h3>
        <p className="text-sm text-gray-500 text-center mb-6">
          Cada persona paga su parte desde su móvil
        </p>

        {/* Slider */}
        <div className="space-y-4">
          <div className="flex items-center justify-between text-sm text-gray-600">
            <span>¿Cuántos pagan?</span>
            <span className="font-bold text-rush-dark text-base">{splits} personas</span>
          </div>
          <input
            type="range"
            min="2" max="10"
            value={splits}
            onChange={handleChange}
            className="w-full accent-rush-red"
            aria-label="Número de personas"
          />
          <div className="flex justify-between text-xs text-gray-400">
            <span>2</span><span>10</span>
          </div>
        </div>

        {/* Per person amount */}
        <div className="mt-5 rounded-2xl bg-rush-red/5 border border-rush-red/20 p-4 text-center">
          <p className="text-xs text-gray-500 mb-0.5">Cada persona paga</p>
          <p className="text-2xl font-black text-rush-red">{perPersonEuros}</p>
        </div>

        {/* Paid count (real-time) */}
        {paidCount > 0 && (
          <div className="mt-3 flex items-center justify-center gap-2 text-sm">
            <div className="flex gap-1">
              {Array.from({ length: splits }, (_, i) => (
                <div
                  key={i}
                  className={`h-2.5 w-2.5 rounded-full ${i < paidCount ? 'bg-green-500' : 'bg-gray-200'}`}
                />
              ))}
            </div>
            <span className="text-gray-600 font-medium">
              Pagos recibidos: <strong className="text-green-600">{paidCount}/{splits}</strong>
            </span>
          </div>
        )}

        <button
          onClick={onClose}
          className="mt-5 w-full rounded-2xl bg-rush-red py-3.5 font-bold text-white hover:bg-rush-red-hover transition-colors"
        >
          Aplicar división
        </button>
      </div>
    </>
  )
}
