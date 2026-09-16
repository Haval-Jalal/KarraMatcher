import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { getCurrentConsent, getMyConsent, grantConsent } from './consentApi'

/** Data och mutation för samtycke (§KM.6, `#195`). */

export const consentKeys = {
  current: ['consent', 'current'] as const,
  me: ['consent', 'me'] as const,
}

export function useCurrentConsent() {
  return useQuery({ queryKey: consentKeys.current, queryFn: () => getCurrentConsent() })
}

export function useMyConsent() {
  return useQuery({ queryKey: consentKeys.me, queryFn: () => getMyConsent() })
}

export function useGrantConsent() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (version: string) => grantConsent(version),
    onSuccess: () => client.invalidateQueries({ queryKey: consentKeys.me }),
  })
}
