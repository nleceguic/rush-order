import { useEffect } from 'react'
import { ToastContainer, toast } from 'rush-order-pwa'

// ToastContainer reads from a module-level zustand store with no props of
// its own - push real toasts through the same `toast` helper the app uses,
// then render the real container.
export function Stack() {
  useEffect(() => {
    toast.success('Pago confirmado')
    toast.info('Tu pedido está en preparación')
    toast.warning('La mesa 12 lleva 20 min de espera')
  }, [])

  return (
    <div style={{ position: 'relative', height: 220 }}>
      <ToastContainer />
    </div>
  )
}
