import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { createLag, getLag } from './lagApi'

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
