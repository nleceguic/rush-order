import { useRef } from 'react'
import type { MenuCategory } from '../types'
import { useEdgeBounce } from '@shared/hooks/useEdgeBounce'

interface CategoryListProps {
  categories: MenuCategory[]
  selected:   string | null
  onSelect:   (id: string) => void
}

export function CategoryList({ categories, selected, onSelect }: CategoryListProps) {
  const navRef = useRef<HTMLElement>(null)
  useEdgeBounce(navRef, 'x')

  return (
    <nav ref={navRef} className="flex gap-2 overflow-x-auto pb-2 scrollbar-hide" aria-label="Categorías">
      {categories.map((cat) => (
        <button
          key={cat.id}
          onClick={() => onSelect(cat.id)}
          className={[
            'flex-shrink-0 rounded-full px-4 py-2 text-sm font-medium transition-colors whitespace-nowrap',
            selected === cat.id
              ? 'bg-rush-red text-white'
              : 'text-gray-700 hover:text-gray-900',
          ].join(' ')}
          aria-pressed={selected === cat.id}
        >
          {cat.name}
        </button>
      ))}
    </nav>
  )
}
