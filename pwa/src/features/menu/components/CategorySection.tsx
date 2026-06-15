import type { ListChildComponentProps } from 'react-window'
import { FixedSizeList } from 'react-window'
import type { MenuCategory, MenuProduct } from '../types'
import { ProductCard } from './ProductCard'

const VIRTUAL_THRESHOLD = 50
const ROW_HEIGHT: Record<1 | 2, number> = { 1: 144, 2: 228 }

interface CategorySectionProps {
  category:       MenuCategory
  columns:        1 | 2
  onProductClick: (product: MenuProduct) => void
}

export function CategorySection({ category, columns, onProductClick }: CategorySectionProps) {
  if (category.products.length === 0) return null

  return (
    <section id={`cat-section-${category.id}`} className="scroll-mt-32">
      <h2 className="px-4 pt-5 pb-3 text-lg font-bold text-rush-dark">
        {category.emoji !== undefined && (
          <span className="mr-1.5" aria-hidden>{category.emoji}</span>
        )}
        {category.name}
      </h2>

      {category.products.length > VIRTUAL_THRESHOLD ? (
        <VirtualGrid
          products={category.products}
          columns={columns}
          onProductClick={onProductClick}
        />
      ) : (
        <div className={`px-4 grid gap-3 pb-4 ${columns === 2 ? 'grid-cols-2' : 'grid-cols-1'}`}>
          {category.products.map((p) => (
            <ProductCard key={p.id} product={p} onViewDetails={onProductClick} />
          ))}
        </div>
      )}
    </section>
  )
}

interface VirtualGridProps {
  products:       MenuProduct[]
  columns:        1 | 2
  onProductClick: (product: MenuProduct) => void
}

function VirtualGrid({ products, columns, onProductClick }: VirtualGridProps) {
  const rowCount    = Math.ceil(products.length / columns)
  const rowHeight   = ROW_HEIGHT[columns]

  const Row = ({ index, style }: ListChildComponentProps) => {
    const a = products[index * columns]
    const b = columns === 2 ? products[index * columns + 1] : undefined

    return (
      <div
        style={style}
        className={`px-4 grid gap-3 ${columns === 2 ? 'grid-cols-2' : 'grid-cols-1'}`}
      >
        {a !== undefined && <ProductCard product={a} onViewDetails={onProductClick} />}
        {b !== undefined && <ProductCard product={b} onViewDetails={onProductClick} />}
      </div>
    )
  }

  return (
    <FixedSizeList
      height={Math.min(rowCount * rowHeight, window.innerHeight * 0.7)}
      itemCount={rowCount}
      itemSize={rowHeight}
      width="100%"
      overscanCount={3}
    >
      {Row}
    </FixedSizeList>
  )
}
