import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMenu } from './hooks/useMenu'
import { CategoryList } from './components/CategoryList'
import { ProductCard } from './components/ProductCard'
import { Spinner } from '@shared/components/Spinner'
import { useCartStore } from '@shared/store/cartStore'

const RESTAURANT_ID = import.meta.env.VITE_RESTAURANT_ID ?? 'demo'

export default function MenuPage() {
  const navigate = useNavigate()
  const { data, isLoading, isError } = useMenu(RESTAURANT_ID)
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null)
  const itemCount = useCartStore((s) => s.items.reduce((n, i) => n + i.quantity, 0))

  const categories   = data?.categories ?? []
  const firstCatId   = categories[0]?.id ?? null
  const activeId     = selectedCategory ?? firstCatId

  const products = (data?.products ?? []).filter(
    (p) => activeId === null || p.categoryId === activeId,
  )

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      {/* Header */}
      <header className="sticky top-0 z-30 bg-white border-b px-4 py-3 flex items-center justify-between shadow-sm">
        <h1 className="text-xl font-bold text-rush-red">Rush Order</h1>
        <button
          onClick={() => navigate('/cart')}
          className="relative p-2"
          aria-label={`Ver carrito, ${itemCount} ítems`}
        >
          <svg className="h-6 w-6 text-rush-dark" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z"
            />
          </svg>
          {itemCount > 0 && (
            <span className="absolute -top-1 -right-1 flex h-5 w-5 items-center justify-center rounded-full bg-rush-red text-[10px] font-bold text-white">
              {itemCount}
            </span>
          )}
        </button>
      </header>

      {/* Content */}
      <main className="flex-1 px-4 py-4 space-y-4">
        {isLoading && (
          <div className="flex justify-center py-16">
            <Spinner size="lg" />
          </div>
        )}

        {isError && (
          <div className="rounded-xl bg-red-50 border border-red-200 p-4 text-center text-red-700 text-sm">
            No se pudo cargar el menú. Comprueba tu conexión e intenta de nuevo.
          </div>
        )}

        {!isLoading && !isError && (
          <>
            {categories.length > 0 && (
              <CategoryList
                categories={categories}
                selected={activeId}
                onSelect={setSelectedCategory}
              />
            )}
            <div className="grid grid-cols-1 gap-3">
              {products.map((product) => (
                <ProductCard key={product.id} product={product} />
              ))}
              {products.length === 0 && (
                <p className="text-center text-gray-400 py-10 text-sm">
                  No hay productos en esta categoría.
                </p>
              )}
            </div>
          </>
        )}
      </main>

      {/* Sticky CTA */}
      {itemCount > 0 && (
        <div className="sticky bottom-0 p-4 bg-white border-t shadow-up">
          <button
            onClick={() => navigate('/cart')}
            className="w-full rounded-xl bg-rush-red py-3 text-center font-semibold text-white hover:bg-rush-red-hover transition-colors"
          >
            Ver pedido · {itemCount} {itemCount === 1 ? 'ítem' : 'ítems'}
          </button>
        </div>
      )}
    </div>
  )
}
