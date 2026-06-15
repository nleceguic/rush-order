interface Props {
  visible: boolean
}

export function ReadyBanner({ visible }: Props) {
  if (!visible) return null

  return (
    <div
      role="alert"
      className="rounded-2xl bg-rush-red p-5 text-white text-center space-y-1 shadow-lg"
      style={{ animation: 'scale-in 0.35s ease' }}
    >
      <div className="text-4xl">🔔</div>
      <h2 className="text-lg font-black">¡Tu pedido está listo!</h2>
      <p className="text-sm opacity-90 leading-snug">
        El camarero se dirige a tu mesa.
      </p>
    </div>
  )
}
