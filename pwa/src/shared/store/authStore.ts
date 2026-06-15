import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { LoyaltyLevel } from '@shared/types'

export interface AuthUser {
  id:                   string
  email:                string
  fullName:             string
  loyaltyPoints:        number
  loyaltyLevel:         LoyaltyLevel
  phone?:               string
  avatarUrl?:           string
  dietaryPrefs:         string[]
  favoriteRestaurantIds: string[]
  notifyOrderReady:     boolean
  notifyPromotions:     boolean
}

interface AuthState {
  accessToken:  string | null
  refreshToken: string | null
  user:         AuthUser | null
  isGuest:      boolean
  setTokens:    (access: string, refresh: string) => void
  setUser:      (user: AuthUser) => void
  updateUser:   (partial: Partial<AuthUser>) => void
  setIsGuest:   (v: boolean) => void
  logout:       () => void
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      accessToken:  null,
      refreshToken: null,
      user:         null,
      isGuest:      false,
      setTokens:    (access, refresh) => set({ accessToken: access, refreshToken: refresh }),
      setUser:      (user) => set({ user, isGuest: false }),
      updateUser:   (partial) => set((s) => ({
        user: s.user !== null ? { ...s.user, ...partial } : s.user,
      })),
      setIsGuest:   (v) => set({ isGuest: v }),
      logout:       () => set({ accessToken: null, refreshToken: null, user: null, isGuest: false }),
    }),
    {
      name: 'rush-order-auth',
      partialize: (state) => ({
        refreshToken: state.refreshToken,
        user:         state.user,
        isGuest:      state.isGuest,
      }),
    },
  ),
)
