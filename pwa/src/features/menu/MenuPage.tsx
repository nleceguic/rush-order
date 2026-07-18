import { useRef, useState } from 'react'
import { useMenu } from './hooks/useMenu'
import { useQRMenu } from './hooks/useQRMenu'
import { CategoryList } from './components/CategoryList'
import { ProductCard } from './components/ProductCard'
import { ProductDetailSheet } from './components/ProductDetailSheet'
import { Spinner } from '@shared/components/Spinner'
import { useCartStore } from '@shared/store/cartStore'
import { useWindowEdgeBounceY } from '@shared/hooks/useEdgeBounce'
import { formatCurrency } from '@shared/utils/format'
import { CartFlow } from '@features/cart/CartFlow'
import type { MenuProduct } from './types'

const RESTAURANT_ID = import.meta.env.VITE_RESTAURANT_ID ?? 'demo'

export default function MenuPage() {
  // RESTAURANT_ID is really a table QR token (see pwa/.env) — useQRMenu resolves
  // it into the table/restaurant settings CartFlow needs (VAT, online payment,
  // upselling) and also sets the cart's tableId/restaurantId, required to
  // submit an order at all.
  const { data: qrData } = useQRMenu(RESTAURANT_ID)
  const { data, isLoading, isError } = useMenu(RESTAURANT_ID)
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null)
  const [selectedProduct, setSelectedProduct] = useState<MenuProduct | null>(null)
  const [cartOpen, setCartOpen] = useState(false)
  const itemCount = useCartStore((s) => s.items.reduce((n, i) => n + i.quantity, 0))
  const subtotal  = useCartStore((s) => s.total())
  const productsRef = useRef<HTMLDivElement>(null)
  useWindowEdgeBounceY(productsRef)

  const categories   = data?.categories ?? []
  const firstCatId   = categories[0]?.id ?? null
  const activeId     = selectedCategory ?? firstCatId

  const products = categories.find((c) => c.id === activeId)?.products ?? []

  const availableProductIds = data !== undefined
    ? new Set(categories.flatMap((c) => c.products.filter((p) => p.isAvailable).map((p) => p.id)))
    : undefined

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      {/* Header */}
      <header className="sticky top-0 z-30 bg-white border-b px-4 py-3 flex items-center shadow-sm">
        <h1 className="text-xl font-bold text-rush-red">Rush Order</h1>
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
            <div ref={productsRef} className="grid grid-cols-1 gap-3">
              {products.map((product) => (
                <ProductCard key={product.id} product={product} onViewDetails={setSelectedProduct} />
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
      <div
        className={`sticky bottom-0 p-4 transition-all duration-300 ease-out ${
          itemCount > 0 ? 'translate-y-0 opacity-100' : 'translate-y-full opacity-0 pointer-events-none'
        }`}
        aria-hidden={itemCount === 0}
      >
        <button
          onClick={() => setCartOpen(true)}
          tabIndex={itemCount > 0 ? 0 : -1}
          className="flex w-full items-center justify-between gap-3 rounded-xl bg-rush-red px-5 py-3.5 text-white shadow-lg hover:bg-rush-red-hover transition-colors"
        >
          <span className="flex items-baseline gap-1.5">
            <span className="font-semibold">Ver pedido</span>
            <span className="text-sm text-white/75">
              · {itemCount} {itemCount === 1 ? 'ítem' : 'ítems'}
            </span>
          </span>
          <span className="rounded-full bg-white/20 px-3 py-1 text-sm font-bold tabular-nums">
            {formatCurrency(subtotal)}
          </span>
        </button>
      </div>

      <ProductDetailSheet product={selectedProduct} onClose={() => setSelectedProduct(null)} />

      <CartFlow
        open={cartOpen}
        onClose={() => setCartOpen(false)}
        onNewRound={() => setCartOpen(false)}
        vatRate={qrData?.vatRate}
        onlinePayEnabled={qrData?.onlinePaymentEnabled ?? false}
        availableProductIds={availableProductIds}
        categories={categories}
        upsellingEnabled={qrData?.upsellingEnabled ?? true}
      />
    </div>
  )
}
