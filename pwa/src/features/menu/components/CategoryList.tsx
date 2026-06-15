import type { Category } from '@shared/types'

interface CategoryListProps {
  categories: Category[]
  selected:   string | null
  onSelect:   (id: string) => void
}

export function CategoryList({ categories, selected, onSelect }: CategoryListProps) {
  return (
    <nav className="flex gap-2 overflow-x-auto pb-2 scrollbar-hide" aria-label="Categorías">
      {categories.map((cat) => (
        <button
          key={cat.id}
          onClick={() => onSelect(cat.id)}
          className={[
            'flex-shrink-0 rounded-full px-4 py-2 text-sm font-medium transition-colors whitespace-nowrap',
            selected === cat.id
              ? 'bg-rush-red text-white'
              : 'bg-gray-100 text-gray-700 hover:bg-gray-200',
          ].join(' ')}
          aria-pressed={selected === cat.id}
        >
          {cat.name}
        </button>
      ))}
    </nav>
  )
}
