import type { MenuCategory, MenuProduct, DietFilter } from './types'
import type { SearchGroup } from './hooks/useMenuSearch'
import { CategoryNav } from './components/CategoryNav'
import { FilterBar } from './components/FilterBar'
import { CategorySection } from './components/CategorySection'
import { SearchResults } from './components/SearchResults'
import { useCategoryScroll } from './hooks/useCategoryScroll'
import { useColumns } from '@shared/hooks/useColumns'

interface MenuSectionProps {
  categories:     MenuCategory[]
  activeFilters:  Set<DietFilter>
  onToggleFilter: (filter: DietFilter) => void
  searchQuery:    string
  searchResults:  SearchGroup[]
  isSearching:    boolean
  onProductClick: (product: MenuProduct) => void
}

export function MenuSection({
  categories,
  activeFilters,
  onToggleFilter,
  searchQuery,
  searchResults,
  isSearching,
  onProductClick,
}: MenuSectionProps) {
  const categoryIds        = categories.map((c) => c.id)
  const { activeId, navRef, scrollToCategory } = useCategoryScroll(categoryIds)
  const columns            = useColumns()
  const hasNoResults       = isSearching
    ? false
    : activeFilters.size > 0 && categories.every((c) => c.products.length === 0)

  return (
    <div id="menu-section">
      {/* Sticky category nav + filter bar */}
      <div className="sticky top-0 z-20 bg-white border-b shadow-sm">
        <CategoryNav
          categories={categories}
          activeId={activeId}
          navRef={navRef}
          onSelect={scrollToCategory}
        />
        <FilterBar activeFilters={activeFilters} onToggle={onToggleFilter} />
      </div>

      {/* Search results or product sections */}
      {isSearching ? (
        <SearchResults
          groups={searchResults}
          query={searchQuery}
          onProductClick={onProductClick}
        />
      ) : hasNoResults ? (
        <div className="flex flex-col items-center justify-center py-20 text-center px-4">
          <span className="text-5xl mb-4" aria-hidden>🧐</span>
          <p className="font-semibold text-gray-700">Sin resultados</p>
          <p className="text-sm text-gray-400 mt-1">
            Ningún producto cumple los filtros activos
          </p>
        </div>
      ) : (
        <div className="pb-32">
          {categories.map((cat) => (
            <CategorySection
              key={cat.id}
              category={cat}
              columns={columns}
              onProductClick={onProductClick}
            />
          ))}
        </div>
      )}
    </div>
  )
}
