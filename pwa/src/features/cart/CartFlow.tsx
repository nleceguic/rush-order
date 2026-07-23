import { useEffect, useCallback, useState, useRef, useLayoutEffect } from 'react'
import { useCheckout, type CheckoutStep } from './hooks/useCheckout'
import { CartStep } from './steps/CartStep'
import { GuestCountStep } from './steps/GuestCountStep'
import { ReviewStep } from './steps/ReviewStep'
import { PaymentStep } from './steps/PaymentStep'
import { ConfirmationStep } from './steps/ConfirmationStep'
import type { MenuCategory } from '@features/menu/types'

interface CartFlowProps {
  open:                 boolean
  onClose:              () => void
  onNewRound:           () => void
  vatRate?:             number | undefined
  onlinePayEnabled?:    boolean
  availableProductIds?: Set<string> | undefined
  categories?:          MenuCategory[] | undefined
  upsellingEnabled?:    boolean
}

const CLOSE_ANIMATION_MS = 300

const STEP_ORDER: readonly CheckoutStep[] = ['cart', 'guestCount', 'review', 'payment', 'confirmation']

export function CartFlow({
  open, onClose, onNewRound, vatRate, onlinePayEnabled = false, availableProductIds, categories,
  upsellingEnabled = true,
}: CartFlowProps) {
  const {
    step, guestCount, generalNotes, redeemedPoints, isLoading, error, confirmedOrder,
    goTo, setGuestCount, setGeneralNotes, setRedeemedPoints, confirmOrder, reset,
  } = useCheckout()

  // Which way to slide the incoming step in from — forward (checkout
  // progressing) enters from the right, backward (user hit "atrás") enters
  // from the left. Computed synchronously in `navigate` from the closure's
  // `step` at click time and stored as real state, rather than inferred
  // during render from a ref mutated in an effect — that pattern reads as
  // impure under StrictMode's double-render check (the ref can already
  // reflect the *new* step by the second render pass, flipping the result).
  const [stepDirection, setStepDirection] = useState<'forward' | 'backward'>('forward')
  const navigate = useCallback((target: CheckoutStep) => {
    setStepDirection(STEP_ORDER.indexOf(target) >= STEP_ORDER.indexOf(step) ? 'forward' : 'backward')
    goTo(target)
  }, [step, goTo])

  // `open` going false should still show the slide-down exit before actually
  // unmounting — otherwise the sheet would just vanish instantly. `visible`
  // keeps it mounted for one more animation frame while `closing` swaps
  // which keyframe plays.
  const [visible, setVisible] = useState(open)
  const [closing, setClosing] = useState(false)

  // Different steps have very different content (a summary list vs a guest
  // count picker), so the sheet's height needs to change too — but CSS can't
  // transition to/from `height: auto`, so we measure the pixel heights
  // ourselves and animate between those (classic FLIP: freeze at the old
  // height, then animate to the newly-measured one on the next frame).
  const stepContentRef = useRef<HTMLDivElement>(null)
  const lastHeightRef  = useRef<number | null>(null)

  useLayoutEffect(() => {
    const el = stepContentRef.current
    if (el === null) return

    // Clear any override left mid-transition from a previous, still-settling
    // step change so this measures the new content's true natural height.
    el.style.transition = 'none'
    el.style.height = ''
    const end = el.getBoundingClientRect().height

    if (lastHeightRef.current === null) {
      lastHeightRef.current = end
      return
    }

    const start = lastHeightRef.current
    lastHeightRef.current = end
    if (start === end) return

    el.style.height = `${start}px`
    el.getBoundingClientRect() // force a reflow so the browser registers the start height

    // Double rAF: a single one can fire before the browser has actually
    // *painted* the frozen start height, in which case there's nothing
    // rendered to transition from and it just snaps straight to `end`.
    let raf2 = 0
    const raf1 = requestAnimationFrame(() => {
      raf2 = requestAnimationFrame(() => {
        el.style.transition = 'height 300ms ease'
        el.style.height = `${end}px`
      })
    })

    const timer = window.setTimeout(() => {
      el.style.transition = ''
      el.style.height = ''
    }, 340)

    return () => {
      cancelAnimationFrame(raf1)
      cancelAnimationFrame(raf2)
      window.clearTimeout(timer)
    }
    // `visible` too — on the render where the sheet first mounts, `visible`
    // is still false and the ref isn't attached yet, so this effect exits
    // early without ever recording a baseline height. Since `step` doesn't
    // change again until the user navigates, it would never get another
    // chance to — leaving the very first real step change with no "start"
    // height to animate from.
  }, [step, visible])

  useEffect(() => {
    if (open) {
      setVisible(true)
      setClosing(false)
      return
    }
    if (!visible) return
    setClosing(true)
    const timer = window.setTimeout(() => {
      setVisible(false)
      setClosing(false)
    }, CLOSE_ANIMATION_MS)
    return () => window.clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  // Lock body scroll for as long as the sheet is visible, including the
  // closing animation window — otherwise the page behind it scrolls while
  // it's still sliding down.
  useEffect(() => {
    document.body.style.overflow = visible ? 'hidden' : ''
    return () => { document.body.style.overflow = '' }
  }, [visible])

  // ESC to close (blocked during payment processing)
  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !(step === 'payment' && isLoading)) onClose()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [open, step, isLoading, onClose])

  const handleNewRound = useCallback(() => {
    reset()
    onNewRound()
  }, [reset, onNewRound])

  const canDismiss = !(step === 'payment' && isLoading)

  if (!visible) return null

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 z-40 bg-black/50 backdrop-blur-sm"
        onClick={canDismiss ? onClose : undefined}
        aria-hidden="true"
      />

      {/* Sheet */}
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Carrito"
        className="fixed inset-x-0 bottom-0 z-50 flex flex-col rounded-t-3xl bg-white shadow-2xl overflow-hidden"
        style={{
          // No fixed height — the sheet sizes to however much content the
          // current step actually has (a 1-item cart vs a 20-item cart),
          // capped at 92dvh where it starts scrolling internally instead of
          // growing further.
          maxHeight: '92dvh',
          animation: closing
            ? `slide-down ${CLOSE_ANIMATION_MS}ms ease-in forwards`
            : 'slide-up 0.3s ease-out',
        }}
      >
        {/* Drag handle */}
        <div className="flex justify-center pt-3 pb-1 flex-shrink-0">
          <div className="h-1 w-10 rounded-full bg-gray-200" aria-hidden />
        </div>

        {/* Step content fills remaining height */}
        <div ref={stepContentRef} className="flex-1 min-h-0 overflow-hidden">
          <div
            key={step}
            className="h-full"
            style={{
              animation: `${stepDirection === 'forward' ? 'step-slide-in-right' : 'step-slide-in-left'} 0.25s ease-out both`,
            }}
          >
            {step === 'cart' && (
              <CartStep
                generalNotes={generalNotes}
                onNotesChange={setGeneralNotes}
                onCheckout={() => navigate('guestCount')}
                onClose={onClose}
                vatRate={vatRate}
                availableProductIds={availableProductIds}
                redeemedPoints={redeemedPoints}
                onRedemptionChange={setRedeemedPoints}
              />
            )}

            {step === 'guestCount' && (
              <GuestCountStep
                value={guestCount}
                onChange={setGuestCount}
                onNext={() => navigate('review')}
                onBack={() => navigate('cart')}
              />
            )}

            {step === 'review' && (
              <ReviewStep
                guestCount={guestCount}
                generalNotes={generalNotes}
                onConfirm={() => navigate('payment')}
                onBack={() => navigate('guestCount')}
                vatRate={vatRate}
                categories={categories}
                upsellingEnabled={upsellingEnabled}
              />
            )}

            {step === 'payment' && (
              <PaymentStep
                isLoading={isLoading}
                error={error}
                onlinePayEnabled={onlinePayEnabled}
                onSelect={confirmOrder}
                onBack={() => navigate('review')}
              />
            )}

            {step === 'confirmation' && confirmedOrder !== null && (
              <ConfirmationStep
                order={confirmedOrder}
                onNewRound={handleNewRound}
              />
            )}
          </div>
        </div>
      </div>
    </>
  )
}
