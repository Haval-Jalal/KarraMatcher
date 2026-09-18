import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { ApiError } from '@/lib/api'

import type { Child, RosterTeam } from './childrenApi'
import {
  useCreateChild,
  useDeleteChild,
  useLinkGuardian,
  useRoster,
  useUnlinkGuardian,
  useUpdateChild,
} from './useChildren'

function messageOf(error: unknown): string {
  if (error instanceof ApiError) {
    return error.offline ? 'Ingen anslutning. Försök igen.' : error.message
  }

  return 'Något gick fel. Försök igen om en stund.'
}

const createSchema = z.object({
  firstName: z.string().trim().min(1, 'Fyll i förnamnet.').max(50),
  lastInitial: z
    .string()
    .trim()
    .min(1, 'Fyll i efternamnets initial.')
    .max(2, 'Bara initialen, inte hela efternamnet.'),
  teamId: z.string(),
})

type CreateValues = z.infer<typeof createSchema>

/**
 * Barnhantering för en trupp (§KM.1, `#196`): skapa barn, sortera i färg-lag, koppla
 * vårdnadshavare. Återanvänds i trupp-adminvyn och superadmin-konsolen.
 *
 * Visar "Liam J" — aldrig hela efternamnet. Att koppla en vårdnadshavare kräver samtycke;
 * servern nekar annars med ett begripligt fel.
 */
export function ChildrenPanel({ truppId }: { truppId: string }) {
  const roster = useRoster(truppId)
  const create = useCreateChild(truppId)
  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<CreateValues>({
    resolver: zodResolver(createSchema),
    defaultValues: { firstName: '', lastInitial: '', teamId: '' },
  })

  const teams = roster.data?.teams ?? []

  const groups: { team: RosterTeam | null; children: Child[] }[] = [
    ...teams.map((team) => ({
      team,
      children: (roster.data?.children ?? []).filter((child) => child.teamId === team.id),
    })),
    {
      team: null,
      children: (roster.data?.children ?? []).filter((child) => child.teamId === null),
    },
  ]

  return (
    <div className="admin-subsection">
      <h3>Barn</h3>

      {roster.isLoading && <p className="state">Hämtar…</p>}
      {roster.isError && (
        <p className="state state--error" role="alert">
          Kunde inte hämta barnen.
        </p>
      )}

      {roster.data && (
        <>
          {groups.map((group) => (
            <div key={group.team?.id ?? 'otilldelade'} className="roster-group">
              <h4>
                {group.team !== null && (
                  <span
                    className="admin-color"
                    style={{ backgroundColor: group.team.colorHex }}
                    aria-hidden="true"
                  />
                )}
                {group.team?.name ?? 'Otilldelade'}
              </h4>
              <ul className="admin-list">
                {group.children.length === 0 && <li className="state">Inga barn här.</li>}
                {group.children.map((child) => (
                  <ChildRow key={child.id} truppId={truppId} child={child} teams={teams} />
                ))}
              </ul>
            </div>
          ))}

          <form
            className="form"
            noValidate
            onSubmit={(event) => {
              void handleSubmit(async (values) => {
                try {
                  await create.mutateAsync({
                    firstName: values.firstName.trim(),
                    lastInitial: values.lastInitial.trim(),
                    teamId: values.teamId === '' ? null : values.teamId,
                  })
                  setFailure(null)
                  reset()
                } catch (error) {
                  setFailure(messageOf(error))
                }
              })(event)
            }}
          >
            <div className="form__field">
              <label htmlFor={`barn-fornamn-${truppId}`}>Förnamn</label>
              <input id={`barn-fornamn-${truppId}`} type="text" {...register('firstName')} />
              {errors.firstName && <p className="form__error">{errors.firstName.message}</p>}
            </div>

            <div className="form__field">
              <label htmlFor={`barn-initial-${truppId}`}>Efternamnets initial</label>
              <input
                id={`barn-initial-${truppId}`}
                type="text"
                maxLength={2}
                {...register('lastInitial')}
              />
              {errors.lastInitial && <p className="form__error">{errors.lastInitial.message}</p>}
            </div>

            <div className="form__field">
              <label htmlFor={`barn-lag-${truppId}`}>Lag (valfritt)</label>
              <select id={`barn-lag-${truppId}`} {...register('teamId')}>
                <option value="">Otilldelad</option>
                {teams.map((team) => (
                  <option key={team.id} value={team.id}>
                    {team.name}
                  </option>
                ))}
              </select>
            </div>

            {failure !== null && (
              <p className="state state--error" role="alert">
                {failure}
              </p>
            )}

            <div className="actions">
              <button type="submit" className="button" disabled={isSubmitting}>
                {isSubmitting ? 'Lägger till…' : 'Lägg till barn'}
              </button>
            </div>
          </form>
        </>
      )}
    </div>
  )
}

function ChildRow({
  truppId,
  child,
  teams,
}: {
  truppId: string
  child: Child
  teams: RosterTeam[]
}) {
  const update = useUpdateChild(truppId)
  const remove = useDeleteChild(truppId)
  const link = useLinkGuardian(truppId)
  const unlink = useUnlinkGuardian(truppId)

  const [editing, setEditing] = useState(false)
  const [firstName, setFirstName] = useState(child.firstName)
  const [lastInitial, setLastInitial] = useState(child.lastInitial)
  const [guardianEmail, setGuardianEmail] = useState('')
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  const run = (action: Promise<unknown>) => {
    setFailure(null)
    void action.catch((error: unknown) => setFailure(messageOf(error)))
  }

  return (
    <li className="admin-list__row roster-child">
      <div className="roster-child__main">
        {editing ? (
          <span className="roster-child__edit">
            <input
              aria-label="Förnamn"
              type="text"
              value={firstName}
              onChange={(event) => setFirstName(event.target.value)}
            />
            <input
              aria-label="Efternamnets initial"
              type="text"
              maxLength={2}
              value={lastInitial}
              onChange={(event) => setLastInitial(event.target.value)}
            />
            <button
              type="button"
              className="button button--small"
              disabled={update.isPending || firstName.trim() === '' || lastInitial.trim() === ''}
              onClick={() =>
                run(
                  update
                    .mutateAsync({
                      id: child.id,
                      data: {
                        firstName: firstName.trim(),
                        lastInitial: lastInitial.trim(),
                        teamId: child.teamId,
                      },
                    })
                    .then(() => setEditing(false)),
                )
              }
            >
              Spara
            </button>
            <button
              type="button"
              className="button button--small"
              onClick={() => {
                setFirstName(child.firstName)
                setLastInitial(child.lastInitial)
                setEditing(false)
              }}
            >
              Avbryt
            </button>
          </span>
        ) : (
          <strong>{child.displayName}</strong>
        )}

        <span className="roster-child__controls">
          <label className="visually-hidden" htmlFor={`flytta-${child.id}`}>
            Flytta {child.displayName} till lag
          </label>
          <select
            id={`flytta-${child.id}`}
            value={child.teamId ?? ''}
            disabled={update.isPending}
            onChange={(event) =>
              run(
                update.mutateAsync({
                  id: child.id,
                  data: {
                    firstName: child.firstName,
                    lastInitial: child.lastInitial,
                    teamId: event.target.value === '' ? null : event.target.value,
                  },
                }),
              )
            }
          >
            <option value="">Otilldelad</option>
            {teams.map((team) => (
              <option key={team.id} value={team.id}>
                {team.name}
              </option>
            ))}
          </select>

          {!editing && (
            <button type="button" className="button button--small" onClick={() => setEditing(true)}>
              Ändra namn
            </button>
          )}

          {confirmDelete ? (
            <>
              <button
                type="button"
                className="button button--small"
                disabled={remove.isPending}
                onClick={() => run(remove.mutateAsync(child.id))}
              >
                Bekräfta borttagning
              </button>
              <button
                type="button"
                className="button button--small"
                onClick={() => setConfirmDelete(false)}
              >
                Avbryt
              </button>
            </>
          ) : (
            <button
              type="button"
              className="button button--small"
              onClick={() => setConfirmDelete(true)}
            >
              Ta bort
            </button>
          )}
        </span>
      </div>

      <div className="roster-child__guardians">
        {child.guardians.length === 0 ? (
          <span className="admin-muted">Ingen vårdnadshavare kopplad</span>
        ) : (
          <ul className="admin-list">
            {child.guardians.map((guardian) => (
              <li key={guardian.accountId}>
                {guardian.displayName ?? guardian.email} <code>{guardian.email}</code>{' '}
                <button
                  type="button"
                  className="button button--small"
                  disabled={unlink.isPending}
                  onClick={() =>
                    run(unlink.mutateAsync({ childId: child.id, accountId: guardian.accountId }))
                  }
                >
                  Koppla bort
                </button>
              </li>
            ))}
          </ul>
        )}

        <div className="form__field">
          <label htmlFor={`vh-${child.id}`}>Koppla vårdnadshavare (adress)</label>
          <input
            id={`vh-${child.id}`}
            type="email"
            value={guardianEmail}
            onChange={(event) => setGuardianEmail(event.target.value)}
          />
        </div>
        <div className="actions">
          <button
            type="button"
            className="button button--small"
            disabled={link.isPending || guardianEmail.trim() === ''}
            onClick={() =>
              run(
                link
                  .mutateAsync({ childId: child.id, email: guardianEmail.trim() })
                  .then(() => setGuardianEmail('')),
              )
            }
          >
            Koppla vårdnadshavare
          </button>
        </div>
      </div>

      {failure !== null && (
        <p className="form__error" role="alert">
          {failure}
        </p>
      )}
    </li>
  )
}
