import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'

import { formatMatchDate } from '@/lib/time'

import { listPasskeys, removePasskey } from './passkeysApi'
import { passkeysSupported, registerPasskey } from './webauthn'

/**
 * Passkey-sektionen på Mitt konto — lägg till Face ID/fingeravtryck, se och ta bort sina egna.
 *
 * <para>
 * En genväg <em>ovanpå</em> e-postkoden, aldrig i stället för den: koden är kvar som reserv och
 * vägen in på varje ny enhet. Stöder webbläsaren inte passkeys visas bara ett lugnt besked.
 * </para>
 */

const passkeysQueryKey = ['passkeys'] as const

export function PasskeysSection() {
  const supported = passkeysSupported()
  const queryClient = useQueryClient()
  const [failure, setFailure] = useState<string | null>(null)

  const list = useQuery({
    queryKey: passkeysQueryKey,
    queryFn: ({ signal }) => listPasskeys(signal),
    enabled: supported,
  })

  const add = useMutation({
    mutationFn: () => registerPasskey('Den här enheten'),
    onSuccess: () => {
      setFailure(null)
      void queryClient.invalidateQueries({ queryKey: passkeysQueryKey })
    },
    onError: () => {
      // En avbruten Face ID eller en enhet utan stöd — inget att skrämmas av.
      setFailure(
        'Det gick inte att lägga till en passkey. Försök igen, eller använd en kod via mejl.',
      )
    },
  })

  const remove = useMutation({
    mutationFn: (id: string) => removePasskey(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: passkeysQueryKey }),
  })

  if (!supported) {
    return (
      <section aria-labelledby="passkeys">
        <h2 className="match-list__title" id="passkeys">
          Passkeys
        </h2>
        <p className="state">
          Den här webbläsaren stöder inte passkeys. Du loggar in med en kod via mejl.
        </p>
      </section>
    )
  }

  return (
    <section aria-labelledby="passkeys">
      <h2 className="match-list__title" id="passkeys">
        Passkeys
      </h2>

      <p className="state">
        Logga in snabbare nästa gång — med Face ID eller fingeravtryck, utan att vänta på en kod.
        E-postkoden fungerar fortfarande.
      </p>

      {list.data && list.data.length > 0 && (
        <ul className="passkey-list">
          {list.data.map((passkey) => (
            <li key={passkey.id} className="passkey-row">
              <span>
                {passkey.deviceLabel ?? 'Passkey'} · tillagd {formatMatchDate(passkey.createdUtc)}
              </span>
              <button
                type="button"
                className="button button--small"
                disabled={remove.isPending}
                onClick={() => {
                  remove.mutate(passkey.id)
                }}
              >
                Ta bort
              </button>
            </li>
          ))}
        </ul>
      )}

      {list.data && list.data.length === 0 && <p className="state">Inga passkeys tillagda än.</p>}

      <button
        type="button"
        className="button button--action"
        disabled={add.isPending}
        onClick={() => {
          add.mutate()
        }}
      >
        {add.isPending ? 'Lägger till…' : 'Lägg till passkey'}
      </button>

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}
    </section>
  )
}
