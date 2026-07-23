import { useState, useRef, useEffect, useLayoutEffect, useCallback } from 'react'
import type { MenuProduct, SelectedOptions, VariantGroup, ExtraGroup } from '../types'
import type { CartModifier } from '@shared/types'
import { AllergenIcons } from './AllergenIcons'
import { BlurImage } from './BlurImage'
import { useCartStore } from '@shared/store/cartStore'
import { useFocusTrap } from '@shared/hooks/useFocusTrap'
import { formatCurrency } from '@shared/utils/format'
import { useRecommendations } from '@features/recommendations/hooks/useRecommendations'
import { RecommendationsRail } from '@features/recommendations/components/RecommendationsRail'

interface ProductDetailSheetProps {
  product: MenuProduct | null
  onClose: () => void
}

function calcTotal(product: MenuProduct, opts: SelectedOptions): number {
  let total = product.price * opts.quantity

  for (const group of product.variants ?? []) {
    const optId = opts.variants[group.id]
    if (optId !== undefined) {
      const opt = group.options.find((o) => o.id === optId)
      if (opt !== undefined) total += opt.priceModifier * opts.quantity
    }
  }

  for (const group of product.extras ?? []) {
    for (const extraId of opts.extras[group.id] ?? []) {
      const opt = group.options.find((o) => o.id === extraId)
      if (opt !== undefined) total += opt.price * opts.quantity
    }
  }

  return total
}

function buildModifiers(product: MenuProduct, opts: SelectedOptions): CartModifier[] {
  const mods: CartModifier[] = []
  for (const group of product.variants ?? []) {
    const optId = opts.variants[group.id]
    if (optId !== undefined) {
      const opt = group.options.find((o) => o.id === optId)
      if (opt !== undefined) mods.push({ name: group.name, value: opt.name, price: opt.priceModifier })
    }
  }
  for (const group of product.extras ?? []) {
    for (const extraId of opts.extras[group.id] ?? []) {
      const opt = group.options.find((o) => o.id === extraId)
      if (opt !== undefined) mods.push({ name: group.name, value: opt.name, price: opt.price })
    }
  }
  return mods
}

const INITIAL_OPTS: SelectedOptions = { variants: {}, extras: {}, quantity: 1 }

// Matches the Ver pedido button's transition duration (duration-300).
const EXIT_DURATION = 300

export function ProductDetailSheet({ product, onClose }: ProductDetailSheetProps) {
  const addItem      = useCartStore((s) => s.addItem)
  const cartItems     = useCartStore((s) => s.items)
  const restaurantId  = useCartStore((s) => s.restaurantId)
  const [opts, setOpts] = useState<SelectedOptions>(INITIAL_OPTS)

  // Kept mounted (with stale content) while the close animation plays, so the
  // sheet has something to render as it slides out instead of vanishing instantly.
  const [displayProduct, setDisplayProduct] = useState<MenuProduct | null>(null)
  const [open, setOpen] = useState(false)
  const sheetRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (product !== null) {
      setDisplayProduct(product)
      return
    }

    setOpen(false)
    const timer = setTimeout(() => setDisplayProduct(null), EXIT_DURATION)
    return () => clearTimeout(timer)
  }, [product])

  // Flip to the open (visible) position right after the sheet mounts in its
  // closed one. A forced synchronous reflow between the two guarantees the
  // browser has registered the closed styles before we change them — a plain
  // requestAnimationFrame (even doubled) can get coalesced with the mount and
  // silently skip the enter transition.
  useLayoutEffect(() => {
    if (displayProduct === null) return
    sheetRef.current?.getBoundingClientRect()
    const raf = requestAnimationFrame(() => setOpen(true))
    return () => cancelAnimationFrame(raf)
  }, [displayProduct])

  // The product being viewed counts as part of "tu selección" for the
  // recommendation signal (manual pairing rules / co-occurrence), even
  // before it's actually added to the cart.
  const cartProductIds = displayProduct !== null
    ? Array.from(new Set([...cartItems.map((i) => i.productId), displayProduct.id]))
    : []
  const { data: recommendations = [] } = useRecommendations(
    displayProduct !== null ? (restaurantId ?? undefined) : undefined,
    cartProductIds,
  )
  const [dragY, setDragY] = useState(0)
  const startY  = useRef(0)
  useFocusTrap(sheetRef, open)

  // Reset state on new product
  useEffect(() => {
    if (product !== null) {
      setOpts(INITIAL_OPTS)
      setDragY(0)
    }
  }, [product?.id])

  // ESC to close + lock body scroll
  useEffect(() => {
    if (product === null) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = ''
    }
  }, [product, onClose])

  const onTouchStart = useCallback((e: React.TouchEvent) => {
    startY.current = e.touches[0]?.clientY ?? 0
  }, [])

  const onTouchMove = useCallback((e: React.TouchEvent) => {
    const delta = (e.touches[0]?.clientY ?? 0) - startY.current
    if (delta > 0) setDragY(delta)
  }, [])

  const onTouchEnd = useCallback(() => {
    if (dragY > 100) onClose()
    setDragY(0)
  }, [dragY, onClose])

  const setVariant = (groupId: string, optId: string) =>
    setOpts((p) => ({ ...p, variants: { ...p.variants, [groupId]: optId } }))

  const toggleExtra = (groupId: string, optId: string) =>
    setOpts((p) => {
      const cur = p.extras[groupId] ?? []
      const next = cur.includes(optId) ? cur.filter((id) => id !== optId) : [...cur, optId]
      return { ...p, extras: { ...p.extras, [groupId]: next } }
    })

  const handleAdd = () => {
    if (product === null) return
    const unitPrice = calcTotal(product, { ...opts, quantity: 1 })
    const modifiers = buildModifiers(product, opts)
    const base      = { productId: product.id, name: product.name, price: unitPrice, quantity: opts.quantity, modifiers }
    if (opts.notes !== undefined) {
      addItem({ ...base, notes: opts.notes })
    } else {
      addItem(base)
    }
    onClose()
  }

  if (displayProduct === null) return null

  const total = calcTotal(displayProduct, opts)

  return (
    <>
      {/* Backdrop */}
      <div
        className={`fixed inset-0 z-40 bg-black/50 backdrop-blur-sm transition-opacity duration-300 ease-out ${
          open ? 'opacity-100' : 'opacity-0 pointer-events-none'
        }`}
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Sheet */}
      <div
        ref={sheetRef}
        role="dialog"
        aria-modal="true"
        aria-label={displayProduct.name}
        aria-hidden={!open}
        className={`fixed inset-x-0 z-50 flex flex-col rounded-t-3xl bg-white shadow-2xl overflow-hidden max-h-[92dvh] ${
          open ? '' : 'pointer-events-none'
        }`}
        style={{
          // `bottom` (not `transform`) on purpose — animating a GPU-composited
          // transform on an element that also has border-radius +
          // overflow-hidden can rasterize the two top corners with visibly
          // different anti-aliasing in Chromium. The live drag offset still
          // needs to track the finger instantly, which `bottom` does just as
          // well as `transform` since transitions are off while dragging.
          bottom:     open ? `${-dragY}px` : '-100%',
          opacity:    open ? 1 : 0,
          transition: dragY === 0 ? 'bottom 300ms ease-out, opacity 300ms ease-out' : 'none',
        }}
        onTouchStart={onTouchStart}
        onTouchMove={onTouchMove}
        onTouchEnd={onTouchEnd}
      >
        {/* Drag handle */}
        <div className="flex justify-center pt-3 pb-1 flex-shrink-0">
          <div className="h-1 w-10 rounded-full bg-gray-300" aria-hidden />
        </div>

        {/* Scrollable body */}
        <div className="overflow-y-auto flex-1 overscroll-contain scrollbar-hide">
          {/* Hero image */}
          {displayProduct.imageUrl !== undefined && (
            <BlurImage
              src={displayProduct.imageUrl}
              alt={`Foto de ${displayProduct.name}`}
              placeholder={displayProduct.imagePlaceholder}
              className="h-56 w-full"
              sizes="100vw"
            />
          )}

          <div className="px-5 py-4 space-y-6">
            {/* Name + description */}
            <div>
              <h2 className="text-xl font-bold text-rush-dark">{displayProduct.name}</h2>
              {displayProduct.description !== undefined && (
                <p className="mt-2 text-sm text-gray-600 leading-relaxed">{displayProduct.description}</p>
              )}
            </div>

            {/* Allergens */}
            {displayProduct.allergens !== undefined && displayProduct.allergens.length > 0 && (
              <div>
                <SectionLabel>Alérgenos</SectionLabel>
                <AllergenIcons allergens={displayProduct.allergens} showLabels size="md" />
              </div>
            )}

            {/* Nutrition */}
            {displayProduct.nutrition !== undefined && (
              <div>
                <SectionLabel>Información nutricional</SectionLabel>
                <div className="grid grid-cols-4 gap-2">
                  {displayProduct.nutrition.calories !== undefined && (
                    <NutriCell label="Kcal" value={displayProduct.nutrition.calories} />
                  )}
                  {displayProduct.nutrition.protein !== undefined && (
                    <NutriCell label="Proteína" value={`${displayProduct.nutrition.protein}g`} />
                  )}
                  {displayProduct.nutrition.carbs !== undefined && (
                    <NutriCell label="Carbos" value={`${displayProduct.nutrition.carbs}g`} />
                  )}
                  {displayProduct.nutrition.fat !== undefined && (
                    <NutriCell label="Grasa" value={`${displayProduct.nutrition.fat}g`} />
                  )}
                </div>
              </div>
            )}

            {/* Variants */}
            {displayProduct.variants?.map((group) => (
              <VariantSection
                key={group.id}
                group={group}
                selected={opts.variants[group.id]}
                onSelect={(optId) => setVariant(group.id, optId)}
              />
            ))}

            {/* Extras */}
            {displayProduct.extras?.map((group) => (
              <ExtraSection
                key={group.id}
                group={group}
                selected={opts.extras[group.id] ?? []}
                onToggle={(optId) => toggleExtra(group.id, optId)}
              />
            ))}

            {/* Notes */}
            <div>
              <SectionLabel>Notas</SectionLabel>
              <textarea
                id="sheet-notes"
                rows={2}
                value={opts.notes ?? ''}
                onChange={(e) => {
                  const val = e.target.value
                  setOpts((p) => {
                    if (val.length > 0) return { ...p, notes: val }
                    return { variants: p.variants, extras: p.extras, quantity: p.quantity }
                  })
                }}
                placeholder="Ej: sin cebolla, alergia a la mostaza…"
                className="w-full rounded-xl border-gray-200 text-sm resize-none focus:ring-rush-red"
              />
            </div>

            {/* Quantity */}
            <div className="flex items-center justify-between">
              <span className="font-medium text-gray-700">Cantidad</span>
              <div className="flex items-center gap-4">
                <button
                  onClick={() => setOpts((p) => ({ ...p, quantity: Math.max(1, p.quantity - 1) }))}
                  disabled={opts.quantity <= 1}
                  className="h-9 w-9 rounded-full border-2 border-gray-200 flex items-center justify-center text-xl hover:border-rush-red hover:text-rush-red disabled:opacity-40 disabled:hover:border-gray-200 disabled:hover:text-current transition-colors"
                  aria-label="Reducir cantidad"
                >
                  −
                </button>
                <span className="w-6 text-center font-bold text-lg tabular-nums">
                  {opts.quantity}
                </span>
                <button
                  onClick={() => setOpts((p) => ({ ...p, quantity: p.quantity + 1 }))}
                  className="h-9 w-9 rounded-full bg-rush-red text-white flex items-center justify-center text-xl hover:bg-rush-red-hover transition-colors"
                  aria-label="Aumentar cantidad"
                >
                  +
                </button>
              </div>
            </div>

          </div>

          {/* También te puede gustar */}
          {recommendations.length > 0 && (
            <div className="mt-2 mb-2">
              <RecommendationsRail title="También te puede gustar" recommendations={recommendations} />
            </div>
          )}

          <div className="h-2 px-5" aria-hidden />
        </div>

        {/* Sticky add button */}
        <div className="flex-shrink-0 border-t bg-white px-5 py-4">
          <button
            onClick={handleAdd}
            disabled={!displayProduct.isAvailable}
            className="w-full rounded-2xl bg-rush-red py-4 font-bold text-white text-base hover:bg-rush-red-hover disabled:opacity-50 transition-colors"
          >
            Añadir al pedido — {formatCurrency(total)}
          </button>
        </div>
      </div>
    </>
  )
}

function SectionLabel({ children }: { children: React.ReactNode }) {
  return (
    <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">{children}</p>
  )
}

function NutriCell({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="text-center bg-gray-50 rounded-xl py-2.5">
      <p className="text-sm font-bold text-rush-dark">{value}</p>
      <p className="text-[10px] text-gray-400 mt-0.5">{label}</p>
    </div>
  )
}

function VariantSection({
  group, selected, onSelect,
}: { group: VariantGroup; selected: string | undefined; onSelect: (id: string) => void }) {
  return (
    <div>
      <div className="flex items-center gap-2 mb-3">
        <SectionLabel>{group.name}</SectionLabel>
        {group.required && (
          <span className="text-[10px] bg-rush-red/10 text-rush-red px-1.5 py-0.5 rounded-full font-medium -mt-2">
            Obligatorio
          </span>
        )}
      </div>
      <div className="space-y-2">
        {group.options.map((opt) => (
          <label key={opt.id} className="flex items-center justify-between cursor-pointer gap-3">
            <div className="flex items-center gap-2">
              <input
                type="radio"
                name={`variant-${group.id}`}
                value={opt.id}
                checked={selected === opt.id}
                onChange={() => onSelect(opt.id)}
                className="accent-rush-red"
              />
              <span className="text-sm">{opt.name}</span>
            </div>
            {opt.priceModifier !== 0 && (
              <span className="text-sm text-gray-500 flex-shrink-0">
                {opt.priceModifier > 0 ? '+' : ''}{formatCurrency(opt.priceModifier)}
              </span>
            )}
          </label>
        ))}
      </div>
    </div>
  )
}

function ExtraSection({
  group, selected, onToggle,
}: { group: ExtraGroup; selected: string[]; onToggle: (id: string) => void }) {
  return (
    <div>
      <div className="flex items-center gap-2 mb-3">
        <SectionLabel>{group.name}</SectionLabel>
        {group.maxSelections !== undefined && (
          <span className="text-[10px] text-gray-400 -mt-2">Máx. {group.maxSelections}</span>
        )}
      </div>
      <div className="space-y-2">
        {group.options.map((opt) => {
          const checked = selected.includes(opt.id)
          const atMax   = group.maxSelections !== undefined
            && selected.length >= group.maxSelections
            && !checked
          return (
            <label
              key={opt.id}
              className={`flex items-center justify-between gap-3 cursor-pointer ${atMax ? 'opacity-40 cursor-not-allowed' : ''}`}
            >
              <div className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={checked}
                  disabled={atMax}
                  onChange={() => { if (!atMax) onToggle(opt.id) }}
                  className="accent-rush-red rounded"
                />
                <span className="text-sm">{opt.name}</span>
              </div>
              {opt.price > 0 && (
                <span className="text-sm text-gray-500 flex-shrink-0">+{formatCurrency(opt.price)}</span>
              )}
            </label>
          )
        })}
      </div>
    </div>
  )
}
