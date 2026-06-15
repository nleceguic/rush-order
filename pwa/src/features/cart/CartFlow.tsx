import { useEffect, useCallback } from 'react'
import { useCheckout } from './hooks/useCheckout'
import { CartStep } from './steps/CartStep'
import { GuestCountStep } from './steps/GuestCountStep'
import { ReviewStep } from './steps/ReviewStep'
import { PaymentStep } from './steps/PaymentStep'
import { ConfirmationStep } from './steps/ConfirmationStep'

interface CartFlowProps {
  open:                 boolean
  onClose:              () => void
  onNewRound:           () => void
  vatRate?:             number
  onlinePayEnabled?:    boolean
  availableProductIds?: Set<string>
}

export function CartFlow({
  open, onClose, onNewRound, vatRate, onlinePayEnabled = false, availableProductIds,
}: CartFlowProps) {
  const {
    step, guestCount, generalNotes, redeemedPoints, isLoading, error, confirmedOrder,
    goTo, setGuestCount, setGeneralNotes, setRedeemedPoints, confirmOrder, reset,
  } = useCheckout()

  // Lock body scroll when open
  useEffect(() => {
    document.body.style.overflow = open ? 'hidden' : ''
    return () => { document.body.style.overflow = '' }
  }, [open])

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

  if (!open) return null

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
        style={{ maxHeight: '92dvh', height: '92dvh' }}
      >
        {/* Drag handle */}
        <div className="flex justify-center pt-3 pb-1 flex-shrink-0">
          <div className="h-1 w-10 rounded-full bg-gray-200" aria-hidden />
        </div>

        {/* Step content fills remaining height */}
        <div className="flex-1 min-h-0 overflow-hidden">
          {step === 'cart' && (
            <CartStep
              generalNotes={generalNotes}
              onNotesChange={setGeneralNotes}
              onCheckout={() => goTo('guestCount')}
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
              onNext={() => goTo('review')}
              onBack={() => goTo('cart')}
            />
          )}

          {step === 'review' && (
            <ReviewStep
              guestCount={guestCount}
              generalNotes={generalNotes}
              onConfirm={() => goTo('payment')}
              onBack={() => goTo('guestCount')}
              vatRate={vatRate}
            />
          )}

          {step === 'payment' && (
            <PaymentStep
              isLoading={isLoading}
              error={error}
              onlinePayEnabled={onlinePayEnabled}
              onSelect={confirmOrder}
              onBack={() => goTo('review')}
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
    </>
  )
}
