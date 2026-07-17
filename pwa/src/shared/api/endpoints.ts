export const ENDPOINTS = {
  auth: {
    login:        '/auth/login',
    register:     '/auth/customer/register',
    loginGoogle:  '/auth/customer/google',
    refresh:      '/auth/refresh',
    logout:       '/auth/logout',
    me:           '/auth/customer/me',
  },
  menu: {
    public:     (restaurantId: string) => `/v1/menu/public/${restaurantId}`,
    categories: '/v1/categories',
    products:   '/v1/menu/products',
  },
  orders: {
    base:   '/orders',
    status: (id: string) => `/orders/${id}/status`,
    pay:    '/payments',
  },
  payments: {
    intent:      '/payments/intent',
    intentById:  (id: string) => `/payments/intent/${id}`,
    subscribe:   '/notifications/subscribe',
  },
  tracking: {
    order:  (id: string) => `/orders/${id}/tracking`,
    rating: (id: string) => `/orders/${id}/rating`,
  },
  loyalty: {
    profile: '/loyalty/profile',
    history: '/loyalty/history',
  },
  promotions: {
    active: (restaurantId: string) => `/restaurants/${restaurantId}/promotions`,
  },
  recommendations: {
    list: '/v1/recommendations',
  },
  experiments: {
    assignment: (key: string) => `/v1/experiments/${key}/assignment`,
    events:     '/v1/experiments/events',
  },
  profile: {
    me:                  '/profile/me',
    avatar:              '/profile/me/avatar',
    history:             '/profile/orders',
    historyById:         (id: string) => `/profile/orders/${id}`,
    paymentMethods:      '/profile/payment-methods',
    deletePaymentMethod: (id: string) => `/profile/payment-methods/${id}`,
    favorites:           '/profile/favorites',
    toggleFavorite:      (restaurantId: string) => `/profile/favorites/${restaurantId}`,
  },
  hubs: {
    orders:        '/hubs/orders',
    orderTracking: '/hubs/order-tracking',
    payments:      '/hubs/payments',
  },
} as const
