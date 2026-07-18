import { useCartStore } from '@shared/store/cartStore'
import { formatCurrency } from '@shared/utils/format'
import type { Recommendation } from '../hooks/useRecommendations'

interface RecommendationsRailProps {
  title:           string
  recommendations: Recommendation[]
  onAdd?:          (recommendation: Recommendation) => void
}

// Reused by ProductDetailSheet ("También te puede gustar") and the cart
// drawer's CartStep ("¿Añadirías algo más?") — same card, same quick-add
// behavior, different title/slice of the recommendation list.
export function RecommendationsRail({ title, recommendations, onAdd }: RecommendationsRailProps) {
  const addItem        = useCartStore((s) => s.addItem)
  const updateQuantity = useCartStore((s) => s.updateQuantity)
  const cartItems       = useCartStore((s) => s.items)

  if (recommendations.length === 0) return null

  const handleAdd = (recommendation: Recommendation) => {
    addItem({
      productId: recommendation.productId,
      name:      recommendation.name,
      price:     recommendation.price,
      quantity:  1,
    })
    onAdd?.(recommendation)
  }

  return (
    <div>
      <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2 px-5">{title}</p>
      <div className="flex gap-3 overflow-x-auto px-5 pb-1 snap-x scrollbar-hide">
        {recommendations.map((recommendation) => {
          // Total units of this product in the order, across every cart line —
          // not just the plain one — so an item added elsewhere with notes/extras
          // still shows as selected here.
          const matches = cartItems.filter((i) => i.productId === recommendation.productId)
          const quantity = matches.reduce((sum, i) => sum + i.quantity, 0)
          const plainItem = matches.find((i) => i.notes === undefined && i.modifiers.length === 0)
          return (
            <RecommendationCard
              key={recommendation.productId}
              recommendation={recommendation}
              quantity={quantity}
              onAdd={() => handleAdd(recommendation)}
              onRemove={() => {
                const target = plainItem ?? matches[0]
                if (target !== undefined) updateQuantity(target.key, target.quantity - 1)
              }}
            />
          )
        })}
      </div>
    </div>
  )
}

function RecommendationCard({
  recommendation, quantity, onAdd, onRemove,
}: { recommendation: Recommendation; quantity: number; onAdd: () => void; onRemove: () => void }) {
  return (
    <div className="flex-shrink-0 w-32 snap-start rounded-2xl border border-gray-100 bg-white overflow-hidden shadow-sm">
      {recommendation.imageUrl !== undefined ? (
        <img
          src={recommendation.imageUrl}
          alt={recommendation.name}
          className="h-20 w-full object-cover"
          loading="lazy"
        />
      ) : (
        <div className="h-20 w-full bg-gray-100" aria-hidden />
      )}
      <div className="p-2">
        <p className="text-[10px] font-medium text-rush-red truncate">{recommendation.reason}</p>
        <p className="text-xs font-semibold text-rush-dark line-clamp-2 mt-0.5 min-h-[2rem]">
          {recommendation.name}
        </p>
        <div className="flex items-center justify-between mt-1.5">
          <span className="text-xs font-bold text-rush-dark">{formatCurrency(recommendation.price)}</span>
          {quantity === 0 ? (
            <button
              type="button"
              onClick={onAdd}
              className="h-6 w-6 rounded-full bg-rush-red text-white flex items-center justify-center text-sm leading-none hover:bg-rush-red-hover transition-colors"
              aria-label={`Añadir ${recommendation.name}`}
            >
              +
            </button>
          ) : (
            <div className="flex items-center gap-1">
              <button
                type="button"
                onClick={onRemove}
                className="h-6 w-6 rounded-full border-2 border-rush-red text-rush-red flex items-center justify-center text-sm leading-none hover:bg-rush-red/10 transition-colors"
                aria-label={`Quitar una unidad de ${recommendation.name}`}
              >
                −
              </button>
              <span className="w-3 text-center text-xs font-bold tabular-nums">{quantity}</span>
              <button
                type="button"
                onClick={onAdd}
                className="h-6 w-6 rounded-full bg-rush-red text-white flex items-center justify-center text-sm leading-none hover:bg-rush-red-hover transition-colors"
                aria-label={`Añadir otra unidad de ${recommendation.name}`}
              >
                +
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
