import type { Promotion } from '@shared/types'

const TYPE_CONFIG = {
  PERCENT_OFF:   { icon: '🏷️', color: 'bg-green-50 border-green-200 text-green-800' },
  BUY_X_GET_Y:   { icon: '🎁', color: 'bg-purple-50 border-purple-200 text-purple-800' },
  PRODUCT_PRICE: { icon: '⚡', color: 'bg-amber-50 border-amber-200 text-amber-800' },
} as const

function timeUntil(iso: string): string {
  const diff = new Date(iso).getTime() - Date.now()
  if (diff <= 0) return ''
  const h = Math.floor(diff / 3_600_000)
  const m = Math.floor((diff % 3_600_000) / 60_000)
  if (h > 24) return `${Math.floor(h / 24)}d`
  if (h > 0) return `${h}h ${m}m`
  return `${m} min`
}

interface PromoCardProps {
  promo: Promotion
}

export function PromoCard({ promo }: PromoCardProps) {
  const cfg      = TYPE_CONFIG[promo.type]
  const timeLeft = promo.endsAt !== undefined ? timeUntil(promo.endsAt) : ''

  return (
    <div className={`flex items-center gap-3 rounded-2xl border px-4 py-3 ${cfg.color}`}>
      <span className="text-2xl flex-shrink-0">{cfg.icon}</span>
      <div className="flex-1 min-w-0">
        <p className="font-semibold text-sm leading-tight">{promo.title}</p>
        <p className="text-xs opacity-80 leading-snug mt-0.5">{promo.description}</p>
      </div>
      {timeLeft.length > 0 && (
        <div className="flex-shrink-0 text-right">
          <p className="text-xs opacity-60">Termina en</p>
          <p className="text-sm font-bold">{timeLeft}</p>
        </div>
      )}
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
