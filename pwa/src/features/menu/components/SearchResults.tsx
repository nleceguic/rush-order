import type { SearchGroup } from '../hooks/useMenuSearch'
import type { MenuProduct } from '../types'
import { highlightMatch } from './SearchBar'

interface SearchResultsProps {
  groups:         SearchGroup[]
  query:          string
  onProductClick: (product: MenuProduct) => void
}

export function SearchResults({ groups, query, onProductClick }: SearchResultsProps) {
  if (groups.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-20 px-4 text-center">
        <span className="text-5xl mb-4" aria-hidden>🔍</span>
        <p className="font-semibold text-gray-700">Sin resultados</p>
        <p className="text-sm text-gray-400 mt-1">
          No encontramos nada para{' '}
          <span className="font-medium text-gray-600">«{query}»</span>
        </p>
      </div>
    )
  }

  return (
    <div className="px-4 py-4 space-y-6">
      {groups.map((group) => (
        <section key={group.categoryId}>
          <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">
            {group.categoryName}
          </h3>
          <div className="space-y-2">
            {group.products.map((product) => (
              <button
                key={product.id}
                className="w-full text-left flex gap-3 rounded-xl border border-gray-100 bg-white p-3 hover:shadow-sm transition-shadow"
                onClick={() => onProductClick(product)}
              >
                {product.imageUrl !== undefined && (
                  <img
                    src={product.imageUrl}
                    alt={product.name}
                    className="h-14 w-14 rounded-lg object-cover flex-shrink-0"
                    loading="lazy"
                  />
                )}
                <div className="flex-1 min-w-0">
                  <p className="font-medium text-sm text-rush-dark leading-snug">
                    {highlightMatch(product.name, query)}
                  </p>
                  {product.shortDescription !== undefined && (
                    <p className="text-xs text-gray-500 mt-0.5 line-clamp-1">
                      {highlightMatch(product.shortDescription, query)}
                    </p>
                  )}
                  <p className="text-sm font-bold text-green-600 mt-1">{product.price.toFixed(2)} €</p>
                </div>
              </button>
            ))}
          </div>
        </section>
      ))}
    </div>
  )
}
