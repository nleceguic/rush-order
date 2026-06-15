import axios, {
  type AxiosError,
  type InternalAxiosRequestConfig,
} from 'axios'

interface RetryConfig extends InternalAxiosRequestConfig {
  _retry?: boolean
  _retryCount?: number
}

const BASE_URL = import.meta.env.VITE_API_URL ?? '/api'

export const apiClient = axios.create({
  baseURL: BASE_URL,
  timeout: 10_000,
  headers: { 'Content-Type': 'application/json' },
})

// These are injected once from main.tsx to avoid circular deps with stores
let _getToken:        () => string | null = () => null
let _getRefreshToken: () => string | null = () => null
let _setTokens:       (access: string, refresh: string) => void = () => undefined
let _onAuthFailed:    () => void = () => undefined

export function configureApiClient(cfg: {
  getToken:        () => string | null
  getRefreshToken: () => string | null
  setTokens:       (access: string, refresh: string) => void
  onAuthFailed:    () => void
}) {
  _getToken        = cfg.getToken
  _getRefreshToken = cfg.getRefreshToken
  _setTokens       = cfg.setTokens
  _onAuthFailed    = cfg.onAuthFailed
}

// ── Request: inject Authorization ────────────────────────────────────────────
apiClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = _getToken()
  if (token !== null) {
    config.headers.set('Authorization', `Bearer ${token}`)
  }
  return config
})

let _refreshPromise: Promise<string> | null = null

// ── Response: 401 refresh + 2-retry on network error ─────────────────────────
apiClient.interceptors.response.use(
  (res) => res,
  async (error: AxiosError) => {
    const config = error.config as RetryConfig | undefined
    if (config === undefined) return Promise.reject(error)

    // 401 → try token refresh exactly once
    if (error.response?.status === 401 && config._retry !== true) {
      config._retry = true
      try {
        if (_refreshPromise === null) {
          _refreshPromise = doRefresh()
        }
        const newToken = await _refreshPromise
        _refreshPromise = null
        config.headers.set('Authorization', `Bearer ${newToken}`)
        return await apiClient(config)
      } catch {
        _refreshPromise = null
        _onAuthFailed()
        return Promise.reject(error)
      }
    }

    // Network errors → retry up to 2 times with back-off
    const retries = config._retryCount ?? 0
    if (error.response === undefined && retries < 2) {
      config._retryCount = retries + 1
      await sleep(800 * (retries + 1))
      return apiClient(config)
    }

    return Promise.reject(error)
  },
)

async function doRefresh(): Promise<string> {
  const refreshToken = _getRefreshToken()
  if (refreshToken === null) throw new Error('No refresh token')

  const { data } = await axios.post<{ accessToken: string; refreshToken: string }>(
    `${BASE_URL}/auth/refresh`,
    { refreshToken },
  )
  _setTokens(data.accessToken, data.refreshToken)
  return data.accessToken
}

const sleep = (ms: number) => new Promise<void>((r) => setTimeout(r, ms))
