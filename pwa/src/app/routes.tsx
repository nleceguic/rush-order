import { createBrowserRouter } from 'react-router-dom'
import { lazy, Suspense, type ReactNode } from 'react'
import { Spinner } from '@shared/components/Spinner'

const LandingPage   = lazy(() => import('@features/menu/LandingPage'))
const MenuPage      = lazy(() => import('@features/menu/MenuPage'))
const PaymentPage       = lazy(() => import('@features/payment/PaymentPage'))
const PaymentResultPage = lazy(() => import('@features/payment/PaymentResultPage'))
const TrackingPage      = lazy(() => import('@features/tracking/TrackingPage'))
const LoyaltyPage       = lazy(() => import('@features/loyalty/LoyaltyPage'))
const ProfilePage       = lazy(() => import('@features/profile/ProfilePage'))
const OrderHistoryPage  = lazy(() => import('@features/profile/OrderHistoryPage'))

function PageFallback() {
  return (
    <div className="flex min-h-screen items-center justify-center">
      <Spinner size="lg" />
    </div>
  )
}

function wrap(element: ReactNode) {
  return <Suspense fallback={<PageFallback />}>{element}</Suspense>
}

export const router = createBrowserRouter([
  // QR landing — primary customer entry point
  { path: '/menu/:qrToken',   element: wrap(<LandingPage />) },

  // Fallback direct menu (dev / standalone)
  { path: '/',                element: wrap(<MenuPage />) },
  // static /payment/result MUST come before the dynamic /payment/:orderId
  { path: '/payment/result',   element: wrap(<PaymentResultPage />) },
  { path: '/payment/:orderId', element: wrap(<PaymentPage />) },
  { path: '/tracking/:orderId', element: wrap(<TrackingPage />) },
  { path: '/loyalty',         element: wrap(<LoyaltyPage />) },
  { path: '/profile',         element: wrap(<ProfilePage />) },
  { path: '/profile/orders',  element: wrap(<OrderHistoryPage />) },
])
