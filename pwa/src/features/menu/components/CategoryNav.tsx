import type { RefObject } from 'react'
import type { MenuCategory } from '../types'
import { useEdgeBounce } from '@shared/hooks/useEdgeBounce'

interface CategoryNavProps {
  categories: MenuCategory[]
  activeId:   string | null
  navRef:     RefObject<HTMLElement>
  onSelect:   (id: string) => void
}

export function CategoryNav({ categories, activeId, navRef, onSelect }: CategoryNavProps) {
  useEdgeBounce(navRef, 'x')

  return (
    <nav
      ref={navRef}
      aria-label="Categorías del menú"
      className="flex overflow-x-auto scrollbar-hide px-2"
      style={{ scrollSnapType: 'x mandatory' }}
    >
      {categories.map((cat) => {
        const active = cat.id === activeId
        return (
          <button
            key={cat.id}
            data-cat={cat.id}
            onClick={() => onSelect(cat.id)}
            aria-current={active ? 'true' : undefined}
            style={{ scrollSnapAlign: 'start' }}
            className={[
              'flex-shrink-0 inline-flex items-center gap-1.5 px-3 py-3 text-sm font-medium whitespace-nowrap',
              'border-b-2 transition-colors focus:outline-none',
              active
                ? 'border-rush-red text-rush-red'
                : 'border-transparent text-gray-500 hover:text-gray-800',
            ].join(' ')}
          >
            {cat.emoji !== undefined && <span aria-hidden>{cat.emoji}</span>}
            {cat.name}
          </button>
        )
      })}
    </nav>
  )
}
