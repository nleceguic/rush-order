import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { registerSW } from 'virtual:pwa-register'
import { configureApiClient } from '@shared/api/axios'
import { useAuthStore } from '@shared/store/authStore'
import App from './App'
import '@fontsource/poppins/400.css'
import '@fontsource/poppins/500.css'
import '@fontsource/poppins/600.css'
import '@fontsource/poppins/700.css'
import '../shared/styles/globals.css'
import './i18n'

configureApiClient({
  getToken:        () => useAuthStore.getState().accessToken,
  getRefreshToken: () => useAuthStore.getState().refreshToken,
  setTokens:       (access, refresh) => useAuthStore.getState().setTokens(access, refresh),
  onAuthFailed:    () => {
    useAuthStore.getState().logout()
    window.location.href = '/'
  },
})

registerSW({ immediate: true })

const rootEl = document.getElementById('root')
if (rootEl === null) throw new Error('Root element not found')

createRoot(rootEl).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
