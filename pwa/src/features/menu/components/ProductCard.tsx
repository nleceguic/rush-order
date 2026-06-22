import type { MenuProduct, ProductTag } from '../types'
import { BlurImage } from './BlurImage'
import { AllergenIcons } from './AllergenIcons'
import { useCartStore } from '@shared/store/cartStore'
import { formatCurrency } from '@shared/utils/format'

const TAG_CONFIG: Record<ProductTag, { label: string; cls: string }> = {
  new:        { label: 'Nuevo',        cls: 'bg-blue-100 text-blue-700' },
  popular:    { label: 'Popular',      cls: 'bg-amber-100 text-amber-700' },
  vegetarian: { label: 'Vegetariano',  cls: 'bg-green-100 text-green-700' },
  vegan:      { label: 'Vegano',       cls: 'bg-emerald-100 text-emerald-700' },
  spicy:      { label: 'Picante 🌶️',  cls: 'bg-red-100 text-red-700' },
  glutenFree: { label: 'Sin Gluten',   cls: 'bg-yellow-100 text-yellow-700' },
  dairyFree:  { label: 'Sin Lactosa',  cls: 'bg-sky-100 text-sky-700' },
}

interface ProductCardProps {
  product:       MenuProduct
  onViewDetails: (product: MenuProduct) => void
}

export function ProductCard({ product, onViewDetails }: ProductCardProps) {
  const addItem = useCartStore((s) => s.addItem)

  const handleQuickAdd = (e: React.MouseEvent) => {
    e.stopPropagation()
    if (!product.isAvailable) return
    addItem({ productId: product.id, name: product.name, price: product.price })
  }

  return (
    <article
      role="button"
      tabIndex={0}
      onClick={() => { if (product.isAvailable) onViewDetails(product) }}
      onKeyDown={(e) => { if (e.key === 'Enter') onViewDetails(product) }}
      className="relative flex gap-3 rounded-xl border border-gray-100 bg-white p-3 shadow-sm cursor-pointer hover:shadow-md transition-shadow focus:outline-none focus:ring-2 focus:ring-rush-red"
    >
      {/* Image */}
      {product.imageUrl !== undefined && (
        <div className="relative flex-shrink-0">
          <BlurImage
            src={product.imageUrl}
            alt={`Foto de ${product.name}`}
            placeholder={product.imagePlaceholder}
            className="h-24 w-24 rounded-lg"
            sizes="96px"
          />
          {!product.isAvailable && (
            <div className="absolute inset-0 rounded-lg bg-black/50 flex items-center justify-center">
              <span className="text-white text-[10px] font-semibold text-center px-1">No disponible</span>
            </div>
          )}
        </div>
      )}

      {/* Content */}
      <div className="flex flex-1 flex-col min-w-0">
        {/* Tags */}
        {product.tags !== undefined && product.tags.length > 0 && (
          <div className="flex flex-wrap gap-1 mb-1">
            {product.tags.slice(0, 2).map((tag) => {
              const cfg = TAG_CONFIG[tag]
              return (
                <span key={tag} className={`text-[10px] font-medium px-1.5 py-0.5 rounded-full ${cfg.cls}`}>
                  {cfg.label}
                </span>
              )
            })}
          </div>
        )}

        {/* Name */}
        <h3 className="font-semibold text-rush-dark text-sm line-clamp-2 leading-snug">{product.name}</h3>

        {/* Short description */}
        {product.shortDescription !== undefined && (
          <p className="mt-0.5 text-xs text-gray-500 line-clamp-2">{product.shortDescription}</p>
        )}

        {/* Allergens */}
        {product.allergens !== undefined && product.allergens.length > 0 && (
          <div className="mt-1.5">
            <AllergenIcons allergens={product.allergens} size="sm" />
          </div>
        )}

        {/* Price + Add */}
        <div className="flex items-center justify-between mt-auto pt-2">
          <span className="font-bold text-green-600 text-sm">{formatCurrency(product.price)}</span>
          <button
            onClick={handleQuickAdd}
            disabled={!product.isAvailable}
            aria-label={`Añadir ${product.name} al pedido`}
            className="h-7 w-7 rounded-full bg-rush-red text-white flex items-center justify-center hover:bg-rush-red-hover disabled:opacity-40 transition-colors flex-shrink-0"
          >
            <svg className="h-4 w-4" viewBox="0 0 20 20" fill="currentColor" aria-hidden>
              <path
                fillRule="evenodd"
                d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z"
                clipRule="evenodd"
              />
            </svg>
          </button>
        </div>
      </div>

      {/* Sold-out overlay (no image variant) */}
      {!product.isAvailable && product.imageUrl === undefined && (
        <div className="absolute inset-0 rounded-xl bg-gray-100/80 flex items-center justify-center pointer-events-none">
          <span className="text-gray-500 text-sm font-medium">No disponible</span>
        </div>
      )}
    </article>
  )
}
