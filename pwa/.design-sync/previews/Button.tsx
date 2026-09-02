import { Button } from 'rush-order-pwa'

const row = { display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' } as const

export function Variants() {
  return (
    <div style={row}>
      <Button variant="primary">Confirmar pedido</Button>
      <Button variant="secondary">Ver detalle</Button>
      <Button variant="ghost">Cancelar</Button>
      <Button variant="danger">Eliminar</Button>
    </div>
  )
}

export function Sizes() {
  return (
    <div style={row}>
      <Button size="sm">Pequeño</Button>
      <Button size="md">Mediano</Button>
      <Button size="lg">Grande</Button>
    </div>
  )
}

export function States() {
  return (
    <div style={row}>
      <Button loading>Procesando pago</Button>
      <Button disabled>No disponible</Button>
    </div>
  )
}
