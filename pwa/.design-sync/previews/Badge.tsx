import { Badge } from 'rush-order-pwa'

export function Variants() {
  return (
    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
      <Badge variant="default">Nuevo</Badge>
      <Badge variant="success">Confirmado</Badge>
      <Badge variant="warning">Pendiente</Badge>
      <Badge variant="danger">Cancelado</Badge>
      <Badge variant="info">En camino</Badge>
    </div>
  )
}
