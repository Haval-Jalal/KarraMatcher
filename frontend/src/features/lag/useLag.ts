import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { createLag, getLag, setLagAttendance, updateLag } from './lagApi'

const lagKey = (truppId: string) => ['admin', 'lag', truppId] as const

export function useLag(truppId: string) {
  return useQuery({ queryKey: lagKey(truppId), queryFn: () => getLag(truppId) })
}

export function useCreateLag(truppId: string) {
  const client = useQueryClient()

  return useMutation({
    mutationFn: (input: { name: string; colorHex: string; slug: string }) =>
      createLag(truppId, input.name, input.colorHex, input.slug),
    onSuccess: () => client.invalidateQueries({ queryKey: lagKey(truppId) }),
  })
}

export function useUpdateLag(truppId: string) {
  const client = useQueryClient()

  return useMutation({
    mutationFn: (input: { id: string; name: string; colorHex: string }) =>
      updateLag(truppId, input.id, input.name, input.colorHex),
    onSuccess: () => client.invalidateQueries({ queryKey: lagKey(truppId) }),
  })
}

export function useSetLagAttendance(truppId: string) {
  const client = useQueryClient()

  return useMutation({
    mutationFn: (input: { id: string; enabled: boolean }) =>
      setLagAttendance(truppId, input.id, input.enabled),
    onSuccess: () => client.invalidateQueries({ queryKey: lagKey(truppId) }),
  })
}
