import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { ApiError } from '@/lib/api'
import { formatFullDate } from '@/lib/time'

import type { InvitationCreated } from './invitationsApi'
import { useCreateInvitation, useInvitations, useRevokeInvitation } from './useInvitations'

function messageOf(error: unknown): string {
  if (error instanceof ApiError) {
    return error.offline ? 'Ingen anslutning. Försök igen.' : error.message
  }

  return 'Något gick fel. Försök igen om en stund.'
}

const schema = z.object({
  email: z.string().trim().email('Adressen ser inte giltig ut.'),
})

type FormValues = z.infer<typeof schema>

/**
 * Skapa, lista och återkalla inbjudningar för en trupp (§KM.3, `#193`).
 *
 * Återanvänds i både superadmin-konsolen och trupp-adminens vy — samma trupp-id, samma
 * server-grind (<c>AdminOfTrupp</c>).
 */
export function InvitationsPanel({ truppId }: { truppId: string }) {
  const invitations = useInvitations(truppId)
  const create = useCreateInvitation(truppId)
  const revoke = useRevokeInvitation(truppId)
  const [failure, setFailure] = useState<string | null>(null)
  const [created, setCreated] = useState<InvitationCreated | null>(null)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { email: '' } })

  return (
    <div className="admin-subsection">
      <h3>Inbjudningar</h3>

      {invitations.isLoading && <p className="state">Hämtar…</p>}
      {invitations.data && (
        <ul className="admin-list">
          {invitations.data.length === 0 && <li className="state">Inga väntande inbjudningar.</li>}
          {invitations.data.map((invitation) => (
            <li key={invitation.id} className="admin-list__row">
              <span>
                <strong>{invitation.email}</strong>{' '}
                <span className="admin-muted">
                  gäller till {formatFullDate(invitation.expiresUtc)}
                </span>
              </span>
              <button
                type="button"
                className="button button--small"
                disabled={revoke.isPending}
                onClick={() => {
                  setFailure(null)
                  void revoke
                    .mutateAsync(invitation.id)
                    .catch((error: unknown) => setFailure(messageOf(error)))
                }}
              >
                Återkalla
              </button>
            </li>
          ))}
        </ul>
      )}

      {created !== null && (
        <p className="state state--ok" role="status">
          Inbjudan skickad till {created.invitation.email}. Länk att dela vid behov:{' '}
          <code>{created.acceptUrl}</code>
        </p>
      )}

      <form
        className="form"
        noValidate
        onSubmit={(event) => {
          void handleSubmit(async (values) => {
            try {
              const result = await create.mutateAsync(values.email.trim())
              setCreated(result)
              setFailure(null)
              reset()
            } catch (error) {
              setFailure(messageOf(error))
            }
          })(event)
        }}
      >
        <div className="form__field">
          <label htmlFor={`inbjudan-epost-${truppId}`}>Bjud in en vårdnadshavare (adress)</label>
          <input
            id={`inbjudan-epost-${truppId}`}
            type="email"
            autoComplete="off"
            aria-invalid={errors.email ? true : undefined}
            {...register('email')}
          />
          {errors.email && <p className="form__error">{errors.email.message}</p>}
        </div>

        {failure !== null && (
          <p className="state state--error" role="alert">
            {failure}
          </p>
        )}

        <div className="actions">
          <button type="submit" className="button" disabled={isSubmitting}>
            {isSubmitting ? 'Skickar…' : 'Skicka inbjudan'}
          </button>
        </div>
      </form>
    </div>
  )
}
