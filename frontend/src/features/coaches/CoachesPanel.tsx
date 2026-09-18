import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { ApiError } from '@/lib/api'

import type { CoachTeam } from './coachesApi'
import { useCoaches, useGrantCoach, useRevokeCoach } from './useCoaches'

function messageOf(error: unknown): string {
  if (error instanceof ApiError) {
    return error.offline ? 'Ingen anslutning. Försök igen.' : error.message
  }

  return 'Något gick fel. Försök igen om en stund.'
}

const grantSchema = z.object({
  email: z.string().trim().email('Adressen ser inte giltig ut.'),
})

type GrantValues = z.infer<typeof grantSchema>

/**
 * Tränartillsättning per lag (§KM.3, `#197`): admin tillsätter en tränare på ett befintligt
 * konto via adress och kan avsätta. Återanvänds i trupp-adminvyn och superadmin-konsolen.
 */
export function CoachesPanel({ truppId }: { truppId: string }) {
  const coaches = useCoaches(truppId)
  const teams = coaches.data?.teams ?? []

  return (
    <div className="admin-subsection">
      <h3>Tränare</h3>

      {coaches.isLoading && <p className="state">Hämtar…</p>}
      {coaches.isError && (
        <p className="state state--error" role="alert">
          Kunde inte hämta tränarna.
        </p>
      )}

      {coaches.data && teams.length === 0 && <p className="state">Skapa ett lag först.</p>}

      {teams.map((team) => (
        <CoachTeamCard key={team.teamId} truppId={truppId} team={team} />
      ))}
    </div>
  )
}

function CoachTeamCard({ truppId, team }: { truppId: string; team: CoachTeam }) {
  const grant = useGrantCoach(truppId)
  const revoke = useRevokeCoach(truppId)
  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<GrantValues>({ resolver: zodResolver(grantSchema), defaultValues: { email: '' } })

  return (
    <div className="roster-group">
      <h4>
        <span
          className="admin-color"
          style={{ backgroundColor: team.colorHex }}
          aria-hidden="true"
        />
        {team.teamName}
      </h4>

      <ul className="admin-list">
        {team.coaches.length === 0 && <li className="state">Ingen tränare än.</li>}
        {team.coaches.map((coach) => (
          <li key={coach.accountId} className="admin-list__row">
            <span>
              <strong>{coach.displayName ?? coach.email}</strong> <code>{coach.email}</code>
            </span>
            <button
              type="button"
              className="button button--small"
              disabled={revoke.isPending}
              onClick={() => {
                setFailure(null)
                void revoke
                  .mutateAsync({ teamId: team.teamId, accountId: coach.accountId })
                  .catch((error: unknown) => setFailure(messageOf(error)))
              }}
            >
              Ta bort
            </button>
          </li>
        ))}
      </ul>

      <form
        className="form"
        noValidate
        onSubmit={(event) => {
          void handleSubmit(async (values) => {
            try {
              await grant.mutateAsync({ teamId: team.teamId, email: values.email.trim() })
              setFailure(null)
              reset()
            } catch (error) {
              setFailure(messageOf(error))
            }
          })(event)
        }}
      >
        <div className="form__field">
          <label htmlFor={`tranare-epost-${team.teamId}`}>
            Tillsätt tränare i {team.teamName} (adress till ett befintligt konto)
          </label>
          <input
            id={`tranare-epost-${team.teamId}`}
            type="email"
            autoComplete="off"
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
          <button type="submit" className="button button--small" disabled={isSubmitting}>
            {isSubmitting ? 'Tillsätter…' : 'Tillsätt tränare'}
          </button>
        </div>
      </form>
    </div>
  )
}
