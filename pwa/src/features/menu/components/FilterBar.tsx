import type { DietFilter } from '../types'

const FILTERS: Array<{ key: DietFilter; label: string; emoji: string }> = [
  { key: 'vegetarian', label: 'Vegetariano', emoji: '🌿' },
  { key: 'vegan',      label: 'Vegano',      emoji: '🥦' },
  { key: 'glutenFree', label: 'Sin Gluten',  emoji: '🌾' },
  { key: 'dairyFree',  label: 'Sin Lactosa', emoji: '🥛' },
  { key: 'spicy',      label: 'Picante',     emoji: '🌶️' },
]

interface FilterBarProps {
  activeFilters: Set<DietFilter>
  onToggle:      (filter: DietFilter) => void
}

export function FilterBar({ activeFilters, onToggle }: FilterBarProps) {
  return (
    <div
      className="flex gap-2 overflow-x-auto scrollbar-hide px-4 py-2"
      role="group"
      aria-label="Filtros de dieta"
    >
      {FILTERS.map(({ key, label, emoji }) => {
        const active = activeFilters.has(key)
        return (
          <button
            key={key}
            onClick={() => onToggle(key)}
            aria-pressed={active}
            className={[
              'flex-shrink-0 inline-flex items-center gap-1 rounded-full px-3 py-1.5 text-xs font-medium border transition-colors',
              active
                ? 'bg-rush-red text-white border-rush-red'
                : 'bg-white text-gray-600 border-gray-200 hover:border-gray-300 hover:text-gray-800',
            ].join(' ')}
          >
            <span aria-hidden>{emoji}</span>
            {label}
          </button>
        )
      })}
    </div>
  )
}
