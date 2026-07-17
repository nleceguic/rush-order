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
  const addItem = useCartStore((s) => s.addItem)

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
        {recommendations.map((recommendation) => (
          <RecommendationCard
            key={recommendation.productId}
            recommendation={recommendation}
            onAdd={() => handleAdd(recommendation)}
          />
        ))}
      </div>
    </div>
  )
}

function RecommendationCard({
  recommendation, onAdd,
}: { recommendation: Recommendation; onAdd: () => void }) {
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
          <button
            type="button"
            onClick={onAdd}
            className="h-6 w-6 rounded-full bg-rush-red text-white flex items-center justify-center text-sm leading-none hover:bg-rush-red-hover transition-colors"
            aria-label={`Añadir ${recommendation.name}`}
          >
            +
          </button>
        </div>
      </div>
    </div>
  )
}
