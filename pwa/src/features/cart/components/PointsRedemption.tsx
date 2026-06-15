import { useAuthStore } from '@shared/store/authStore'

const POINTS_PER_EURO = 100 // 100 points = 1 €

interface PointsRedemptionProps {
  cartTotal:      number // euros
  redeemedPoints: number
  onChange:       (points: number) => void
}

export function PointsRedemption({ cartTotal, redeemedPoints, onChange }: PointsRedemptionProps) {
  const user = useAuthStore((s) => s.user)

  if (user === null || user.loyaltyPoints === 0) return null

  const maxByPolicy    = Math.floor(cartTotal * 0.20 * POINTS_PER_EURO) // 20% of total
  const maxRedeemable  = Math.min(user.loyaltyPoints, maxByPolicy)
  const redeemEuros    = redeemedPoints / POINTS_PER_EURO

  if (maxRedeemable <= 0) return null

  return (
    <div className="rounded-2xl border border-amber-200 bg-amber-50 p-4 space-y-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <span className="text-lg">⭐</span>
          <div>
            <p className="text-sm font-semibold text-rush-dark">
              Tienes {user.loyaltyPoints.toLocaleString('es')} puntos
            </p>
            <p className="text-xs text-gray-500">
              Valor máximo canjeable: {(maxRedeemable / POINTS_PER_EURO).toFixed(2)} €
            </p>
          </div>
        </div>
        {redeemedPoints > 0 && (
          <span className="text-sm font-bold text-green-600">
            -{redeemEuros.toFixed(2)} €
          </span>
        )}
      </div>

      {/* Slider */}
      <div className="space-y-1.5">
        <div className="flex justify-between text-xs text-gray-500">
          <span>0 pts</span>
          <span className="font-medium text-rush-dark">
            {redeemedPoints > 0
              ? `${redeemedPoints} pts = ${redeemEuros.toFixed(2)} €`
              : 'Sin canjear'}
          </span>
          <span>{maxRedeemable} pts</span>
        </div>
        <input
          type="range"
          min={0}
          max={maxRedeemable}
          step={Math.max(1, Math.floor(maxRedeemable / 20))}
          value={redeemedPoints}
          onChange={(e) => onChange(parseInt(e.target.value, 10))}
          className="w-full accent-amber-500"
          aria-label="Puntos a canjear"
        />
      </div>

      {redeemedPoints === 0 && (
        <button
          type="button"
          onClick={() => onChange(maxRedeemable)}
          className="text-xs font-semibold text-amber-700 hover:text-amber-800"
        >
          Usar todos los puntos disponibles
        </button>
      )}
      {redeemedPoints > 0 && (
        <button
          type="button"
          onClick={() => onChange(0)}
          className="text-xs text-gray-400 hover:text-gray-600"
        >
          No canjear puntos
        </button>
      )}
    </div>
  )
}
