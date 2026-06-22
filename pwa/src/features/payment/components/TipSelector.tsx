import { useState, useEffect, useCallback } from 'react'
import { formatCurrency } from '@shared/utils/format'

type TipMode = 'none' | 5 | 10 | 15 | 'custom'

interface TipSelectorProps {
  baseAmountCents: number
  onConfirm:       (tipCents: number) => void
}

const PRESET_OPTIONS: { label: string; mode: TipMode }[] = [
  { label: 'Sin propina', mode: 'none' },
  { label: '5%',          mode: 5 },
  { label: '10%',         mode: 10 },
  { label: '15%',         mode: 15 },
  { label: 'Otro',        mode: 'custom' },
]

export function TipSelector({ baseAmountCents, onConfirm }: TipSelectorProps) {
  const [mode,         setMode]         = useState<TipMode>('none')
  const [customEuros,  setCustomEuros]  = useState('')

  const calcTipCents = useCallback((m: TipMode, custom: string): number => {
    if (m === 'none')   return 0
    if (m === 'custom') return Math.round((parseFloat(custom) || 0) * 100)
    return Math.round(baseAmountCents * m / 100)
  }, [baseAmountCents])

  // Confirm on preset selection immediately
  const handleSelect = (m: TipMode) => {
    setMode(m)
    if (m !== 'custom') onConfirm(calcTipCents(m, customEuros))
  }

  // Confirm custom on blur
  const handleCustomBlur = () => {
    onConfirm(calcTipCents('custom', customEuros))
  }

  // Recalculate when base changes
  useEffect(() => {
    if (mode !== 'custom') onConfirm(calcTipCents(mode, ''))
  }, [baseAmountCents]) // eslint-disable-line react-hooks/exhaustive-deps

  const tipCents = calcTipCents(mode, customEuros)

  return (
    <div className="mb-5">
      <div className="flex items-center justify-between mb-2">
        <p className="text-sm font-semibold text-rush-dark">Propina</p>
        {tipCents > 0 && (
          <span className="text-sm font-bold text-green-600">
            +{formatCurrency(tipCents / 100)}
          </span>
        )}
      </div>

      <div className="flex gap-2 flex-wrap">
        {PRESET_OPTIONS.map((opt) => (
          <button
            key={String(opt.mode)}
            type="button"
            onClick={() => handleSelect(opt.mode)}
            className={`
              px-3 py-1.5 rounded-full text-sm font-medium transition-all border
              ${mode === opt.mode
                ? 'bg-rush-red text-white border-rush-red'
                : 'bg-white text-gray-600 border-gray-200 hover:border-rush-red hover:text-rush-red'
              }
            `}
          >
            {opt.label}
            {opt.mode !== 'none' && opt.mode !== 'custom' && (
              <span className="ml-1 text-xs opacity-75">
                {formatCurrency(Math.round(baseAmountCents * opt.mode / 100) / 100)}
              </span>
            )}
          </button>
        ))}
      </div>

      {mode === 'custom' && (
        <div className="mt-2 flex items-center gap-2">
          <input
            type="number"
            min="0"
            step="0.50"
            value={customEuros}
            onChange={(e) => setCustomEuros(e.target.value)}
            onBlur={handleCustomBlur}
            placeholder="0.00"
            className="w-28 rounded-xl border-gray-200 text-sm focus:ring-rush-red"
            aria-label="Importe de propina en euros"
          />
          <span className="text-sm text-gray-500">€</span>
        </div>
      )}
    </div>
  )
}
