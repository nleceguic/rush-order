import type { AllergenKey } from '../types'

const ALLERGEN_MAP: Record<AllergenKey, { emoji: string; label: string }> = {
  gluten:      { emoji: '🌾', label: 'Gluten' },
  crustaceans: { emoji: '🦐', label: 'Crustáceos' },
  eggs:        { emoji: '🥚', label: 'Huevos' },
  fish:        { emoji: '🐟', label: 'Pescado' },
  peanuts:     { emoji: '🥜', label: 'Cacahuetes' },
  soybeans:    { emoji: '🫘', label: 'Soja' },
  dairy:       { emoji: '🥛', label: 'Lácteos' },
  nuts:        { emoji: '🌰', label: 'Frutos secos' },
  celery:      { emoji: '🌿', label: 'Apio' },
  mustard:     { emoji: '🌻', label: 'Mostaza' },
  sesame:      { emoji: '⚪', label: 'Sésamo' },
  sulphites:   { emoji: '🍷', label: 'Sulfitos' },
  lupin:       { emoji: '🌼', label: 'Altramuces' },
  molluscs:    { emoji: '🐚', label: 'Moluscos' },
}

interface AllergenIconsProps {
  allergens:   AllergenKey[]
  showLabels?: boolean
  size?:       'sm' | 'md'
}

export function AllergenIcons({ allergens, showLabels = false, size = 'sm' }: AllergenIconsProps) {
  if (allergens.length === 0) return null

  return (
    <div className="flex flex-wrap gap-1">
      {allergens.map((key) => {
        const info = ALLERGEN_MAP[key]
        return (
          <span
            key={key}
            title={info.label}
            className={[
              'inline-flex items-center gap-1 rounded-full bg-orange-50 border border-orange-200 font-medium',
              size === 'sm' ? 'px-1.5 py-0.5 text-[10px]' : 'px-2 py-1 text-xs',
            ].join(' ')}
          >
            <span role="img" aria-label={info.label}>{info.emoji}</span>
            {showLabels && <span className="text-orange-700">{info.label}</span>}
          </span>
        )
      })}
    </div>
  )
}
