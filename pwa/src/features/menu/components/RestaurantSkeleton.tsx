function Pulse({ className = '' }: { className?: string }) {
  return <div className={`animate-pulse rounded bg-gray-200 ${className}`} />
}

export function RestaurantSkeleton() {
  return (
    <div className="min-h-screen bg-gray-50">
      {/* Cover */}
      <div className="h-52 w-full animate-pulse bg-gray-300" />

      {/* Logo */}
      <div className="-mt-10 flex justify-center">
        <div className="h-20 w-20 rounded-full animate-pulse bg-gray-300 border-4 border-white" />
      </div>

      {/* Name + message */}
      <div className="flex flex-col items-center gap-2 px-4 py-4">
        <Pulse className="h-7 w-48" />
        <Pulse className="h-4 w-64" />
        <Pulse className="h-4 w-40" />
      </div>

      {/* CTA button */}
      <div className="flex justify-center pb-6">
        <Pulse className="h-12 w-36 rounded-full" />
      </div>

      {/* Category nav */}
      <div className="flex gap-2 overflow-hidden px-4 py-2 border-b">
        {Array.from({ length: 5 }).map((_, i) => (
          <Pulse key={i} className="h-8 w-24 flex-shrink-0 rounded-full" />
        ))}
      </div>

      {/* Product cards */}
      <div className="px-4 mt-4 space-y-3">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="flex gap-3 rounded-xl border border-gray-100 bg-white p-3">
            <Pulse className="h-24 w-24 rounded-lg flex-shrink-0" />
            <div className="flex-1 space-y-2 pt-1">
              <Pulse className="h-4 w-3/4" />
              <Pulse className="h-3 w-full" />
              <Pulse className="h-3 w-2/3" />
              <Pulse className="h-5 w-14" />
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
