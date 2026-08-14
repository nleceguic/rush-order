import { defineConfig, type Plugin } from 'vite'
import { configDefaults } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'
import { visualizer } from 'rollup-plugin-visualizer'
import { resolve } from 'path'

export default defineConfig(({ mode }) => ({
  plugins: [
    react(),

    VitePWA({
      registerType:   'autoUpdate',
      manifestFilename: 'manifest.json',
      includeAssets: ['favicon.ico', 'icons/*.png', 'offline.html'],
      manifest: {
        name:             'Rush Order',
        short_name:       'Rush Order',
        description:      'Pide desde tu mesa',
        theme_color:      '#E63946',
        background_color: '#ffffff',
        display:          'standalone',
        orientation:      'portrait-primary',
        start_url:        '/',
        lang:             'es',
        categories:       ['food', 'lifestyle'],
        icons: [
          { src: '/icons/icon-72x72.png',   sizes: '72x72',   type: 'image/png' },
          { src: '/icons/icon-96x96.png',   sizes: '96x96',   type: 'image/png' },
          { src: '/icons/icon-128x128.png', sizes: '128x128', type: 'image/png' },
          { src: '/icons/icon-144x144.png', sizes: '144x144', type: 'image/png' },
          { src: '/icons/icon-152x152.png', sizes: '152x152', type: 'image/png' },
          { src: '/icons/icon-192x192.png', sizes: '192x192', type: 'image/png', purpose: 'any maskable' },
          { src: '/icons/icon-384x384.png', sizes: '384x384', type: 'image/png' },
          { src: '/icons/icon-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'any maskable' },
        ],
        shortcuts: [
          {
            name:       'Ver menú',
            url:        '/',
            icons: [{ src: '/icons/icon-96x96.png', sizes: '96x96' }],
          },
        ],
      },
      workbox: {
        // Pre-cache app shell
        globPatterns: ['**/*.{js,css,html,ico,png,svg,woff2}'],
        globIgnores:  ['**/node_modules/**', '**/stats.html'],

        // Offline fallback for navigation requests
        navigateFallback:          '/offline.html',
        navigateFallbackDenylist:  [/^\/api\//, /^\/hubs\//],

        runtimeCaching: [
          // Menu API — NetworkFirst: show live data, fallback to cached
          {
            urlPattern: /\/api\/v1\/menu\/public\/.*/i,
            handler:    'NetworkFirst',
            options: {
              cacheName:            'menu-api-cache',
              networkTimeoutSeconds: 3,
              expiration:           { maxEntries: 50, maxAgeSeconds: 60 * 60 * 24 },
              cacheableResponse:    { statuses: [0, 200] },
            },
          },
          // Product images — StaleWhileRevalidate: instant from cache, update in background
          {
            urlPattern: /\.(png|jpg|jpeg|webp|avif|svg)(\?.*)?$/i,
            handler:    'StaleWhileRevalidate',
            options: {
              cacheName:         'product-images-cache',
              expiration:        { maxEntries: 150, maxAgeSeconds: 60 * 60 * 24 * 7 },
              cacheableResponse: { statuses: [0, 200] },
            },
          },
          // Self-hosted fonts — CacheFirst (immutable)
          {
            urlPattern: /\.(woff|woff2|ttf|otf)(\?.*)?$/i,
            handler:    'CacheFirst',
            options: {
              cacheName:         'font-cache',
              expiration:        { maxEntries: 20, maxAgeSeconds: 60 * 60 * 24 * 365 },
              cacheableResponse: { statuses: [0, 200] },
            },
          },
          // Google Fonts CSS (if still used)
          {
            urlPattern: /^https:\/\/fonts\.googleapis\.com\/.*/i,
            handler:    'StaleWhileRevalidate',
            options: {
              cacheName:         'google-fonts-css',
              expiration:        { maxEntries: 5, maxAgeSeconds: 60 * 60 * 24 * 7 },
              cacheableResponse: { statuses: [0, 200] },
            },
          },
          // Google Font files
          {
            urlPattern: /^https:\/\/fonts\.gstatic\.com\/.*/i,
            handler:    'CacheFirst',
            options: {
              cacheName:         'google-fonts-files',
              expiration:        { maxEntries: 20, maxAgeSeconds: 60 * 60 * 24 * 365 },
              cacheableResponse: { statuses: [0, 200] },
            },
          },
        ],
      },
    }),

    // Bundle visualizer — generates dist/stats.html on every build
    visualizer({
      filename:   'dist/stats.html',
      gzipSize:   true,
      brotliSize: true,
      open:       mode === 'analyze',
    }) as Plugin,
  ],

  resolve: {
    alias: {
      '@app':      resolve(__dirname, 'src/app'),
      '@features': resolve(__dirname, 'src/features'),
      '@shared':   resolve(__dirname, 'src/shared'),
    },
  },

  build: {
    target:    'esnext',
    minify:    'esbuild',
    sourcemap: mode !== 'production',
    rollupOptions: {
      output: {
        // Fine-grained vendor splitting — keeps hot path chunks small
        manualChunks(id) {
          if (!id.includes('node_modules')) return undefined

          if (id.includes('react-dom'))       return 'vendor-react'
          if (id.includes('react-router'))    return 'vendor-router'
          if (id.includes('react') && !id.includes('@stripe')) return 'vendor-react'
          if (id.includes('@tanstack'))       return 'vendor-query'
          if (id.includes('@stripe'))         return 'vendor-stripe'
          if (id.includes('@microsoft/signalr')) return 'vendor-signalr'
          if (id.includes('i18next') || id.includes('react-i18next')) return 'vendor-i18n'
          if (id.includes('canvas-confetti')) return 'vendor-misc'
          if (id.includes('zustand'))         return 'vendor-misc'
          if (id.includes('axios'))           return 'vendor-misc'

          return 'vendor-misc'
        },
        // Deterministic chunk names for long-term caching
        chunkFileNames:  'assets/[name]-[hash].js',
        entryFileNames:  'assets/[name]-[hash].js',
        assetFileNames:  'assets/[name]-[hash][extname]',
      },
    },
  },

  server: {
    port: 5173,
    proxy: {
      '/api': {
        target:      'http://localhost:5000',
        changeOrigin: true,
      },
      '/hubs': {
        target:      'http://localhost:5000',
        changeOrigin: true,
        ws:           true,
      },
    },
  },

  test: {
    globals:     true,
    environment: 'jsdom',
    setupFiles:  './src/app/test-setup.ts',
    // Playwright's E2E suite lives under tests/e2e — it has its own runner
    // and would otherwise be picked up by Vitest's default *.spec.ts glob.
    exclude:     [...configDefaults.exclude, 'tests/e2e/**'],
  },
}))
