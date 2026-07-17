import { useState, useRef, useCallback, useEffect } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useQRMenu } from './hooks/useQRMenu'
import { useMenu } from './hooks/useMenu'
import { useMenuFilters } from './hooks/useMenuFilters'
import { useMenuSearch } from './hooks/useMenuSearch'
import { usePromotions } from './hooks/usePromotions'
import { RestaurantHero } from './components/RestaurantHero'
import { RestaurantSkeleton } from './components/RestaurantSkeleton'
import { ProductDetailSheet } from './components/ProductDetailSheet'
import { SearchBar } from './components/SearchBar'
import { PromoCarousel } from './components/PromoCard'
import { MenuSection } from './MenuSection'
import { CartFlow } from '@features/cart/CartFlow'
import { FloatingCartButton } from '@features/cart/components/FloatingCartButton'
import { PastOrdersPanel } from '@features/cart/components/PastOrdersPanel'
import { useQRSession } from '@features/cart/hooks/useQRSession'
import { AuthSheet } from '@features/auth/AuthSheet'
import { useCartStore } from '@shared/store/cartStore'
import { useAuth } from '@shared/hooks/useAuth'
import { toast } from '@shared/components/Toast'
import type { MenuProduct } from './types'

export default function LandingPage() {
  const { qrToken = '' }         = useParams<{ qrToken: string }>()
  const [locale, setLocale]      = useState('es')
  const [selected, setSelected]  = useState<MenuProduct | null>(null)
  const [cartOpen, setCartOpen]  = useState(false)
  const [authOpen, setAuthOpen]  = useState(false)
  const menuRef                  = useRef<HTMLDivElement>(null)

  const { isAuthenticated, user } = useAuth()
  const completedOrders = useCartStore((s) => s.completedOrders)

  const { data: qrData,   isLoading: qrLoading  } = useQRMenu(qrToken)
  const { data: menuData, isLoading: menuLoading } = useMenu(qrData?.restaurantId, locale)
  const { data: promotions } = usePromotions(qrData?.restaurantId)

  const isLoading     = qrLoading || (qrData !== undefined && menuLoading)
  const allCategories = menuData?.categories ?? []

  const { activeFilters, toggleFilter, filteredCategories } = useMenuFilters(allCategories)
  const { query, setQuery, results, isSearching }           = useMenuSearch(filteredCategories)

  const availableProductIds = menuData !== undefined
    ? new Set(allCategories.flatMap((c) => c.products.filter((p) => p.isAvailable).map((p) => p.id)))
    : undefined

  // Prefetch menu API and JS chunk as soon as restaurant ID is known
  useEffect(() => {
    if (qrData?.restaurantId === undefined) return

    const link = document.createElement('link')
    link.rel  = 'prefetch'
    link.href = `/api/v1/menu/public/${qrData.restaurantId}`
    link.as   = 'fetch'
    link.crossOrigin = 'anonymous'
    document.head.appendChild(link)

    // Eagerly load the MenuPage JS chunk
    void import('@features/menu/MenuPage')

    return () => { document.head.removeChild(link) }
  }, [qrData?.restaurantId])

  const handleQRWarning = useCallback(() => {
    toast.warning('Tu sesión expirará en 5 minutos por inactividad.')
  }, [])

  const handleQRExpired = useCallback(() => {
    toast.error('Sesión expirada. Vuelve a escanear el código QR.')
  }, [])

  useQRSession({ onWarning: handleQRWarning, onExpired: handleQRExpired })

  const handleNewRound = useCallback(() => {
    setCartOpen(false)
  }, [])

  const scrollToMenu = () =>
    menuRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })

  if (isLoading) return <RestaurantSkeleton />

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Sticky header */}
      <header className="sticky top-0 z-30 bg-white border-b shadow-sm px-4 py-2 flex items-center justify-between gap-2 min-h-[52px]">
        {qrData !== undefined && (
          <div className="flex items-center gap-2 min-w-0">
            {qrData.logoUrl !== undefined && (
              <img
                src={qrData.logoUrl}
                alt=""
                className="h-7 w-7 rounded-full object-cover flex-shrink-0"
                loading="eager"
              />
            )}
            <span className="font-semibold text-sm text-rush-dark truncate">
              {qrData.restaurantName}
            </span>
          </div>
        )}
        <SearchBar value={query} onChange={setQuery} />
        {/* User icon → AuthSheet or profile */}
        {isAuthenticated ? (
          <Link to="/profile" aria-label="Perfil" className="flex-shrink-0">
            <div className="h-8 w-8 rounded-full bg-rush-red flex items-center justify-center text-white text-sm font-bold">
              {user?.fullName.charAt(0).toUpperCase() ?? '?'}
            </div>
          </Link>
        ) : (
          <button
            type="button"
            onClick={() => setAuthOpen(true)}
            aria-label="Iniciar sesión"
            className="flex-shrink-0 h-8 w-8 rounded-full bg-gray-100 flex items-center justify-center hover:bg-gray-200"
          >
            <svg className="h-4 w-4 text-gray-600" viewBox="0 0 24 24" fill="none" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
            </svg>
          </button>
        )}
      </header>

      {/* Hero */}
      {qrData !== undefined && (
        <RestaurantHero
          data={qrData}
          locale={locale}
          onLocaleChange={setLocale}
          onViewMenu={scrollToMenu}
        />
      )}

      {/* Promotions banner */}
      {(promotions ?? []).length > 0 && (
        <PromoCarousel promotions={promotions ?? []} />
      )}

      {/* Menu section */}
      <div ref={menuRef}>
        <MenuSection
          categories={filteredCategories}
          activeFilters={activeFilters}
          onToggleFilter={toggleFilter}
          searchQuery={query}
          searchResults={results}
          isSearching={isSearching}
          onProductClick={setSelected}
        />
      </div>

      {/* Past orders panel (multi-round) */}
      {completedOrders.length > 0 && (
        <div className="pb-4 pt-2">
          <PastOrdersPanel orders={completedOrders} />
        </div>
      )}

      {/* Product detail sheet */}
      <ProductDetailSheet product={selected} onClose={() => setSelected(null)} />

      {/* Floating cart button */}
      <FloatingCartButton
        hidden={selected !== null || cartOpen}
        onClick={() => setCartOpen(true)}
      />

      {/* Full cart + checkout flow */}
      <CartFlow
        open={cartOpen}
        onClose={() => setCartOpen(false)}
        onNewRound={handleNewRound}
        vatRate={qrData?.vatRate}
        onlinePayEnabled={qrData?.onlinePaymentEnabled ?? false}
        availableProductIds={availableProductIds}
        categories={allCategories}
        upsellingEnabled={qrData?.upsellingEnabled ?? true}
      />

      {/* Auth sheet — triggered from header icon */}
      <AuthSheet open={authOpen} onClose={() => setAuthOpen(false)} />
    </div>
  )
}
