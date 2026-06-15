export interface Category {
  id: string
  name: string
  description?: string
  displayOrder: number
  isActive: boolean
}

export interface Product {
  id: string
  categoryId: string
  name: string
  description?: string
  price: number
  imageUrl?: string
  isAvailable: boolean
  allergens?: string[]
}

export interface CartModifier {
  name:  string
  value: string
  price: number
}

export interface CartItem {
  key:       string
  productId: string
  name:      string
  price:     number
  quantity:  number
  notes?:    string
  modifiers: CartModifier[]
}

export interface CompletedOrder {
  id:                string
  number:            string
  round:             number
  items:             CartItem[]
  total:             number
  createdAt:         string
  estimatedMinutes?: number
}

export interface Order {
  id: string
  tableId: string
  items: OrderItem[]
  status: OrderStatus
  totalAmount: number
  createdAt: string
}

export interface OrderItem {
  productId: string
  productName: string
  quantity: number
  unitPrice: number
  notes?: string
}

export type OrderStatus = 'New' | 'Preparing' | 'Ready' | 'Served' | 'Paid' | 'Cancelled'

export type LoyaltyLevel = 'Bronze' | 'Silver' | 'Gold' | 'Platinum'

export type PromotionType = 'PERCENT_OFF' | 'BUY_X_GET_Y' | 'PRODUCT_PRICE'

export interface Promotion {
  id:          string
  title:       string
  description: string
  type:        PromotionType
  value:       number
  productId?:  string
  endsAt?:     string
}

export interface SavedPaymentMethod {
  id:        string
  brand:     string
  last4:     string
  expMonth:  number
  expYear:   number
  isDefault: boolean
}

export interface ApiError {
  message: string
  code?: string
  status: number
}
