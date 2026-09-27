import { useState } from 'react'

import { ApiError } from '@/lib/api'

import type { CupSummary } from './cupApi'
import {
  useAssignCupChild,
  useCreateCupTeam,
  useDeleteCupTeam,
  useUnassignCupChild,
} from './useCup'

function messageOf(error: unknown): string {
  if (error instanceof ApiError) {
    return error.offline ? 'Ingen anslutning. Försök igen.' : error.message
  }

  return 'Något gick fel. Försök igen om en stund.'
}

/**
 * Cup-lags-bygget på cupens händelsesida (`#335`, slice 4 av #330).
 *
 * <para>
 * En admin (prövas server-side) bygger tillfälliga cup-lag av de barn som anmält sig — andra
 * barn varje cup (§KM.1). Varje anmält barn får en lagväljare; att välja ett lag placerar barnet
 * (och flyttar det från ett tidigare lag). En vårdnadshavare ser lagen i läsläge, så hen vet
 * vilket lag barnet hamnat i. Barn visas som "Liam J" (§KM.1).
 * </para>
 */
export function CupTeamsBuilder({
  eventId,
  truppId,
  summary,
  isAdmin,
}: {
  eventId: string
  truppId: string
  summary: CupSummary
  isAdmin: boolean
}) {
  const create = useCreateCupTeam(truppId, eventId)
  const remove = useDeleteCupTeam(truppId, eventId)
  const assign = useAssignCupChild(truppId, eventId)
  const unassign = useUnassignCupChild(truppId, eventId)

  const [name, setName] = useState('')
  const [failure, setFailure] = useState<string | null>(null)

  const run = (action: Promise<unknown>) => {
    setFailure(null)
    void action.catch((error: unknown) => setFailure(messageOf(error)))
  }

  // Barnets nuvarande cup-lag, härlett ur lagens medlemslistor.
  const teamOf = new Map<string, string>()
  for (const team of summary.teams) {
    for (const member of team.members) {
      teamOf.set(member.childId, team.id)
    }
  }

  // En vårdnadshavare utan lag att visa får ingenting — sektionen dyker upp först när admin byggt.
  if (!isAdmin && summary.teams.length === 0) {
    return null
  }

  function onPick(childId: string, nextTeamId: string): void {
    const current = teamOf.get(childId)

    if (nextTeamId === '') {
      if (current !== undefined) {
        run(unassign.mutateAsync({ cupTeamId: current, childId }))
      }

      return
    }

    run(assign.mutateAsync({ cupTeamId: nextTeamId, childId }))
  }

  return (
    <div className="cup-teams">
      <h3 className="cup__list-title">Cup-lag</h3>

      {isAdmin && (
        <form
          className="cup__open"
          onSubmit={(event) => {
            event.preventDefault()
            const trimmed = name.trim()
            if (trimmed === '') {
              setFailure('Ge cup-laget ett namn.')
              return
            }
            run(create.mutateAsync(trimmed).then(() => setName('')))
          }}
        >
          <label htmlFor="cup-team-name">Nytt cup-lag</label>
          <input
            id="cup-team-name"
            type="text"
            autoComplete="off"
            maxLength={60}
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="t.ex. Lag 1"
          />
          <button type="submit" className="button" disabled={create.isPending}>
            Skapa cup-lag
          </button>
        </form>
      )}

      {summary.teams.length > 0 && (
        <ul className="cup-teams__list">
          {summary.teams.map((team) => (
            <li key={team.id} className="cup-teams__team">
              <div className="cup-teams__team-head">
                <strong>{team.name}</strong>
                {isAdmin && (
                  <button
                    type="button"
                    className="button button--small"
                    disabled={remove.isPending}
                    onClick={() => run(remove.mutateAsync(team.id))}
                  >
                    Ta bort laget
                  </button>
                )}
              </div>
              {team.members.length > 0 ? (
                <ul>
                  {team.members.map((member) => (
                    <li key={member.childId}>{member.displayName}</li>
                  ))}
                </ul>
              ) : (
                <p className="admin-muted">Inga barn i laget än.</p>
              )}
            </li>
          ))}
        </ul>
      )}

      {/* Admin placerar de anmälda barnen — en lagväljare per barn. */}
      {isAdmin && summary.teams.length > 0 && summary.signedUp.length > 0 && (
        <div className="cup-teams__assign">
          <h4 className="cup__list-title">Placera anmälda</h4>
          <ul className="cup__mine">
            {summary.signedUp.map((child) => (
              <li key={child.childId} className="cup__mine-row">
                <label htmlFor={`cup-team-pick-${child.childId}`}>{child.displayName}</label>
                <select
                  id={`cup-team-pick-${child.childId}`}
                  value={teamOf.get(child.childId) ?? ''}
                  onChange={(event) => onPick(child.childId, event.target.value)}
                >
                  <option value="">Inget lag</option>
                  {summary.teams.map((team) => (
                    <option key={team.id} value={team.id}>
                      {team.name}
                    </option>
                  ))}
                </select>
              </li>
            ))}
          </ul>
        </div>
      )}

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}
    </div>
  )
}
