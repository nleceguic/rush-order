import { useState } from 'react'

interface BlurImageProps {
  src:          string
  alt:          string
  placeholder?: string | undefined
  className?:   string
  sizes?:       string
}

export function BlurImage({ src, alt, placeholder, className = '', sizes }: BlurImageProps) {
  const [loaded, setLoaded] = useState(false)
  const resolvedSizes = sizes ?? '(min-width: 640px) 50vw, 100vw'

  return (
    <div className={`relative overflow-hidden bg-gray-100 ${className}`}>
      {placeholder !== undefined && !loaded && (
        <img
          src={placeholder}
          aria-hidden="true"
          alt=""
          className="absolute inset-0 w-full h-full object-cover scale-110 blur-xl"
        />
      )}
      <picture>
        {/* WebP — served when the CDN/server supports ?format=webp */}
        <source
          type="image/webp"
          sizes={resolvedSizes}
          srcSet={`${src}?w=400&format=webp 400w, ${src}?w=800&format=webp 800w`}
        />
        {/* Fallback JPEG/PNG */}
        <img
          src={src}
          alt={alt}
          loading="lazy"
          decoding="async"
          sizes={resolvedSizes}
          srcSet={`${src}?w=400 400w, ${src}?w=800 800w`}
          onLoad={() => setLoaded(true)}
          className={`w-full h-full object-cover transition-opacity duration-500 ${
            loaded ? 'opacity-100' : 'opacity-0'
          }`}
        />
      </picture>
    </div>
  )
}
