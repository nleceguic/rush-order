import {
  useQuery,
  useMutation,
  type UseQueryOptions,
  type UseMutationOptions,
} from '@tanstack/react-query'
import { apiClient } from '@shared/api/axios'
import type { AxiosError } from 'axios'
import type { ApiError } from '@shared/types'

export function useGet<TData>(
  key:      unknown[],
  url:      string,
  options?: Omit<UseQueryOptions<TData, AxiosError<ApiError>>, 'queryKey' | 'queryFn'>,
) {
  return useQuery<TData, AxiosError<ApiError>>({
    queryKey: key,
    queryFn:  async () => {
      const { data } = await apiClient.get<TData>(url)
      return data
    },
    ...options,
  })
}

export function usePost<TData, TVariables>(
  url:      string,
  options?: UseMutationOptions<TData, AxiosError<ApiError>, TVariables>,
) {
  return useMutation<TData, AxiosError<ApiError>, TVariables>({
    mutationFn: async (variables) => {
      const { data } = await apiClient.post<TData>(url, variables)
      return data
    },
    ...options,
  })
}

export function usePut<TData, TVariables>(
  url:      string,
  options?: UseMutationOptions<TData, AxiosError<ApiError>, TVariables>,
) {
  return useMutation<TData, AxiosError<ApiError>, TVariables>({
    mutationFn: async (variables) => {
      const { data } = await apiClient.put<TData>(url, variables)
      return data
    },
    ...options,
  })
}

export function useDelete<TData>(
  url:      string,
  options?: UseMutationOptions<TData, AxiosError<ApiError>, void>,
) {
  return useMutation<TData, AxiosError<ApiError>, void>({
    mutationFn: async () => {
      const { data } = await apiClient.delete<TData>(url)
      return data
    },
    ...options,
  })
}
