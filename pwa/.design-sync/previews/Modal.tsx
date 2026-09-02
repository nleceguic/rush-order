import { Modal, Button } from 'rush-order-pwa'

export function ConfirmDialog() {
  return (
    <>
      {/* Modal renders `fixed inset-0`; the card harness makes this preview's
          own wrapper the fixed containing block (see cfg.overrides.Modal),
          which has no other content to size its height against. The real
          app always has a tall page underneath the modal - this spacer
          stands in for that ambient height so the dialog centers correctly
          instead of collapsing around a ~0-height containing block. */}
      <div style={{ height: 480 }} aria-hidden="true" />
      <Modal open onClose={() => {}} title="Cancelar pedido">
        <p style={{ fontSize: 14, color: '#4B5563', marginBottom: 16 }}>
          ¿Seguro que quieres cancelar el pedido #0847? Esta acción no se puede deshacer.
        </p>
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
          <Button variant="ghost">Volver</Button>
          <Button variant="danger">Cancelar pedido</Button>
        </div>
      </Modal>
    </>
  )
}
