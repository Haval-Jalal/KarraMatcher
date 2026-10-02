import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { useRoster } from '@/features/children'
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
  email: z.string().trim().email('Adressen ser inte giltig ut.').max(320, 'Adressen är för lång.'),
  // Valfritt lag-förslag. Tom sträng = inget lag (`#408`).
  teamId: z.string(),
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
  const roster = useRoster(truppId)
  const create = useCreateInvitation(truppId)
  const revoke = useRevokeInvitation(truppId)
  const [failure, setFailure] = useState<string | null>(null)
  const [created, setCreated] = useState<InvitationCreated | null>(null)

  const teams = roster.data?.teams ?? []

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', teamId: '' },
  })

  return (
    <div className="admin-subsection">
      <h2>Inbjudningar</h2>

      {invitations.isLoading && (
        <p className="state" role="status">
          Hämtar…
        </p>
      )}
      {invitations.isError && (
        <p className="state state--error" role="alert">
          {invitations.error instanceof ApiError && invitations.error.offline
            ? 'Ingen anslutning. Inbjudningarna kunde inte hämtas.'
            : 'Kunde inte hämta inbjudningarna just nu.'}
        </p>
      )}
      {invitations.data && (
        <ul className="admin-list">
          {invitations.data.length === 0 && <li className="state">Inga väntande inbjudningar.</li>}
          {invitations.data.map((invitation) => (
            <li key={invitation.id} className="admin-list__row">
              <span>
                <strong>{invitation.email}</strong>{' '}
                {invitation.teamName !== null && (
                  <span className="admin-muted">· föreslaget lag: {invitation.teamName} </span>
                )}
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
              const result = await create.mutateAsync({
                email: values.email.trim(),
                teamId: values.teamId === '' ? null : values.teamId,
              })
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
            aria-describedby={errors.email ? `inbjudan-epost-fel-${truppId}` : undefined}
            {...register('email')}
          />
          {errors.email && (
            <p id={`inbjudan-epost-fel-${truppId}`} className="form__error">
              {errors.email.message}
            </p>
          )}
        </div>

        {teams.length > 0 && (
          <div className="form__field">
            <label htmlFor={`inbjudan-lag-${truppId}`}>Föreslå färg-lag (valfritt)</label>
            <select id={`inbjudan-lag-${truppId}`} {...register('teamId')}>
              <option value="">Inget lag</option>
              {teams.map((team) => (
                <option key={team.id} value={team.id}>
                  {team.name}
                </option>
              ))}
            </select>
          </div>
        )}

        {/* Lag-förslaget är valfritt — failar lag-hämtningen går inbjudan ändå, bara utan väljaren.
            Säg det i stället för att låta väljaren tyst saknas (#543). */}
        {roster.isError && (
          <p className="admin-muted" role="status">
            {roster.error instanceof ApiError && roster.error.offline
              ? 'Ingen anslutning, så färg-lagen kunde inte hämtas. Du kan ändå skicka inbjudan utan lag-förslag.'
              : 'Färg-lagen kunde inte hämtas, så du kan inte föreslå ett lag just nu. Inbjudan går ändå att skicka.'}
          </p>
        )}

        {failure !== null && (
          <p className="state state--error" role="alert">
            {failure}
          </p>
        )}

        <div className="actions">
          <button type="submit" className="button button--action" disabled={isSubmitting}>
            {isSubmitting ? 'Skickar…' : 'Skicka inbjudan'}
          </button>
        </div>
      </form>
    </div>
  )
}
