export type ProductTag =
  | 'new' | 'popular' | 'vegetarian' | 'vegan' | 'spicy' | 'glutenFree' | 'dairyFree'

export type AllergenKey =
  | 'gluten' | 'crustaceans' | 'eggs' | 'fish' | 'peanuts' | 'soybeans'
  | 'dairy'  | 'nuts'        | 'celery' | 'mustard' | 'sesame' | 'sulphites'
  | 'lupin'  | 'molluscs'

export type DietFilter = 'vegetarian' | 'vegan' | 'glutenFree' | 'dairyFree' | 'spicy'

export interface VariantOption {
  id:            string
  name:          string
  priceModifier: number
}

export interface VariantGroup {
  id:       string
  name:     string
  required: boolean
  options:  VariantOption[]
}

export interface ExtraOption {
  id:    string
  name:  string
  price: number
}

export interface ExtraGroup {
  id:             string
  name:           string
  maxSelections?: number
  options:        ExtraOption[]
}

export interface NutritionInfo {
  calories?: number
  protein?:  number
  carbs?:    number
  fat?:      number
}

export interface MenuProduct {
  id:               string
  name:             string
  shortDescription?: string
  description?:     string
  price:            number
  imageUrl?:        string
  imagePlaceholder?: string
  isAvailable:      boolean
  tags?:            ProductTag[]
  allergens?:       AllergenKey[]
  nutrition?:       NutritionInfo
  variants?:        VariantGroup[]
  extras?:          ExtraGroup[]
}

export interface MenuCategory {
  id:           string
  name:         string
  emoji?:       string
  displayOrder: number
  products:     MenuProduct[]
}

export interface QRData {
  restaurantId:          string
  tableId:               string
  restaurantName:        string
  coverImageUrl?:        string
  logoUrl?:              string
  welcomeMessage?:       string
  availableLocales:      string[]
  onlinePaymentEnabled?: boolean
  vatRate?:              number
  upsellingEnabled?:     boolean
}

export interface SelectedOptions {
  variants: Record<string, string>
  extras:   Record<string, string[]>
  notes?:   string
  quantity: number
}
