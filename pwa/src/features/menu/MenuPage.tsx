import { useRef, useState } from 'react'
import { useMenu } from './hooks/useMenu'
import { useQRMenu } from './hooks/useQRMenu'
import { CategoryList } from './components/CategoryList'
import { ProductCard } from './components/ProductCard'
import { ProductDetailSheet } from './components/ProductDetailSheet'
import { Spinner } from '@shared/components/Spinner'
import { useCartStore } from '@shared/store/cartStore'
import { useEdgeBounce } from '@shared/hooks/useEdgeBounce'
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
  const scrollRef = useRef<HTMLDivElement>(null)
  useEdgeBounce(scrollRef, 'y')

  const categories   = data?.categories ?? []
  const firstCatId   = categories[0]?.id ?? null
  const activeId     = selectedCategory ?? firstCatId

  const products = categories.find((c) => c.id === activeId)?.products ?? []

  const availableProductIds = data !== undefined
    ? new Set(categories.flatMap((c) => c.products.filter((p) => p.isAvailable).map((p) => p.id)))
    : undefined

  return (
    <div className="h-dvh bg-gray-50 flex flex-col overflow-hidden">
      {/* Header — fixed, never scrolls */}
      <header className="shrink-0 z-30 bg-gray-50 px-4 pt-8 pb-3 flex flex-col">
        <h1 className="text-xl font-bold text-rush-red">
          {qrData?.restaurantName}
        </h1>
        <p className="text-xs font-medium text-gray-500 tracking-wide">
          {qrData?.name ?? ''}
        </p>
      </header>

      {/* Category tabs — fixed, scroll horizontally within themselves only */}
      {categories.length > 0 && (
        <div className="shrink-0 bg-gray-50 px-4 pt-3">
          <CategoryList
            categories={categories}
            selected={activeId}
            onSelect={setSelectedCategory}
          />
        </div>
      )}

      {/* Products — the only vertically scrollable region */}
      <main
        ref={scrollRef}
        className={`flex-1 overflow-y-auto px-4 pt-1 ${itemCount > 0 ? 'pb-24' : 'pb-4'}`}
      >
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
          <div className="grid grid-cols-1 gap-3">
            {products.map((product) => (
              <ProductCard key={product.id} product={product} onViewDetails={setSelectedProduct} />
            ))}
            {products.length === 0 && (
              <p className="text-center text-gray-400 py-10 text-sm">
                No hay productos en esta categoría.
              </p>
            )}
          </div>
        )}
      </main>

      {/* Floating CTA — overlays the product cards instead of reserving its own row */}
      <div
        className={`fixed inset-x-4 bottom-4 z-40 transition-all duration-300 ease-out ${
          itemCount > 0 ? 'translate-y-0 opacity-100' : 'translate-y-24 opacity-0 pointer-events-none'
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
