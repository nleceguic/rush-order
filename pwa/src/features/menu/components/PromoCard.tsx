import type { Promotion } from '@shared/types'

interface PromoCardProps {
  promo: Promotion
}

export function PromoCard({ promo }: PromoCardProps) {
  return (
    <div className="flex items-center gap-3 rounded-2xl border px-4 py-3 bg-green-50 border-green-200 text-green-800">
      <span className="text-2xl flex-shrink-0">🏷️</span>
      <div className="flex-1 min-w-0">
        <p className="font-semibold text-sm leading-tight">{promo.name}</p>
        <p className="text-xs opacity-80 leading-snug mt-0.5">{promo.description}</p>
      </div>
    </div>
  )
}

interface PromoCarouselProps {
  promotions: Promotion[]
}

export function PromoCarousel({ promotions }: PromoCarouselProps) {
  if (promotions.length === 0) return null

  return (
    <div className="px-4 py-3 space-y-2">
      {promotions.map((p) => (
        <PromoCard key={p.id} promo={p} />
      ))}
    </div>
  )
}
