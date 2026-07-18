import { formatCurrency } from '@shared/utils/format'

interface PriceSummaryProps {
  subtotal:  number
  vatRate?:  number | undefined
  discount?: number | undefined
}

export function PriceSummary({ subtotal, vatRate, discount }: PriceSummaryProps) {
  const hasDiscount   = discount !== undefined && discount > 0
  const afterDiscount = hasDiscount ? subtotal - discount : subtotal
  const showVat       = vatRate !== undefined && vatRate > 0
  const vatAmount     = showVat ? afterDiscount * (vatRate / (100 + vatRate)) : 0
  const base          = showVat ? afterDiscount - vatAmount : afterDiscount

  return (
    <div className="space-y-1.5 text-sm">
      {hasDiscount && (
        <>
          <div className="flex justify-between text-gray-500">
            <span>Subtotal</span>
            <span className="tabular-nums">{formatCurrency(subtotal)}</span>
          </div>
          <div className="flex justify-between text-green-600 font-medium">
            <span>Descuento puntos</span>
            <span className="tabular-nums">-{formatCurrency(discount!)}</span>
          </div>
        </>
      )}
      {showVat ? (
        <>
          <div className="flex justify-between text-gray-500">
            <span>Base imponible</span>
            <span className="tabular-nums">{formatCurrency(base)}</span>
          </div>
          <div className="flex justify-between text-gray-500">
            <span>IVA ({vatRate}%)</span>
            <span className="tabular-nums">{formatCurrency(vatAmount)}</span>
          </div>
          <div className="flex justify-between font-bold text-base text-rush-dark pt-2 border-t">
            <span>Total</span>
            <span className="text-rush-red tabular-nums">{formatCurrency(afterDiscount)}</span>
          </div>
        </>
      ) : (
        <div className="flex justify-between font-bold text-base text-rush-dark pt-1 border-t">
          <span>Total</span>
          <span className="text-rush-red tabular-nums">{formatCurrency(afterDiscount)}</span>
        </div>
      )}
    </div>
  )
}
