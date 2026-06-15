interface PriceSummaryProps {
  subtotal:  number
  vatRate?:  number
  discount?: number // points / promo discount in euros
}

export function PriceSummary({ subtotal, vatRate, discount }: PriceSummaryProps) {
  const hasDiscount = discount !== undefined && discount > 0
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
            <span className="tabular-nums">{subtotal.toFixed(2)} €</span>
          </div>
          <div className="flex justify-between text-green-600 font-medium">
            <span>Descuento puntos</span>
            <span className="tabular-nums">-{discount!.toFixed(2)} €</span>
          </div>
        </>
      )}
      {showVat ? (
        <>
          <div className="flex justify-between text-gray-500">
            <span>Base imponible</span>
            <span className="tabular-nums">{base.toFixed(2)} €</span>
          </div>
          <div className="flex justify-between text-gray-500">
            <span>IVA ({vatRate}%)</span>
            <span className="tabular-nums">{vatAmount.toFixed(2)} €</span>
          </div>
          <div className="flex justify-between font-bold text-base text-rush-dark pt-2 border-t">
            <span>Total</span>
            <span className="text-rush-red tabular-nums">{afterDiscount.toFixed(2)} €</span>
          </div>
        </>
      ) : (
        <div className="flex justify-between font-bold text-base text-rush-dark pt-1 border-t">
          <span>Total</span>
          <span className="text-rush-red tabular-nums">{afterDiscount.toFixed(2)} €</span>
        </div>
      )}
    </div>
  )
}
