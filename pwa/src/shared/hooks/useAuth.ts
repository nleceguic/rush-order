import { useAuthStore, type AuthUser } from '@shared/store/authStore'
import { apiClient } from '@shared/api/axios'
import { ENDPOINTS } from '@shared/api/endpoints'
import { getDeviceFingerprint } from '@shared/utils/deviceFingerprint'

interface AuthResponse {
  accessToken:  string
  refreshToken: string
  user:         AuthUser
}

export function useAuth() {
  const {
    user, accessToken, isGuest,
    setTokens, setUser, updateUser, setIsGuest, logout,
  } = useAuthStore()

  const login = async (email: string, password: string): Promise<void> => {
    const fp = getDeviceFingerprint()
    const { data } = await apiClient.post<AuthResponse>(ENDPOINTS.auth.login, {
      email, password, deviceFingerprint: fp,
    })
    setTokens(data.accessToken, data.refreshToken)
    setUser(data.user)
  }

  const register = async (fullName: string, email: string, password: string): Promise<void> => {
    const fp = getDeviceFingerprint()
    const { data } = await apiClient.post<AuthResponse>(ENDPOINTS.auth.register, {
      fullName, email, password, deviceFingerprint: fp,
    })
    setTokens(data.accessToken, data.refreshToken)
    setUser(data.user)
  }

  const loginWithGoogle = (): void => {
    const apiBase = String(import.meta.env.VITE_API_URL ?? 'http://localhost:5000/api')
    const base    = apiBase.replace(/\/api$/, '')
    window.location.href = `${base}/auth/customer/google?return=${encodeURIComponent(window.location.href)}`
  }

  const continueAsGuest = (): void => {
    getDeviceFingerprint() // ensure fingerprint exists
    setIsGuest(true)
  }

  const updateProfile = async (partial: Partial<Pick<AuthUser, 'fullName' | 'phone' | 'dietaryPrefs' | 'notifyOrderReady' | 'notifyPromotions'>>): Promise<void> => {
    await apiClient.patch(ENDPOINTS.profile.me, partial)
    updateUser(partial)
  }

  return {
    user,
    isAuthenticated: accessToken !== null,
    isGuest,
    login,
    register,
    loginWithGoogle,
    continueAsGuest,
    updateProfile,
    logout,
  }
}
