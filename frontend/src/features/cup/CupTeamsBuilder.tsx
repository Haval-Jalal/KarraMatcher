import { useEffect, useRef, useState } from 'react'

import { ApiError } from '@/lib/api'

import type { CupSummary } from './cupApi'
import {
  useAssignCupChild,
  useCreateCupTeam,
  useDeleteCupTeam,
  useRenameCupTeam,
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
  const rename = useRenameCupTeam(truppId, eventId)
  const remove = useDeleteCupTeam(truppId, eventId)
  const assign = useAssignCupChild(truppId, eventId)
  const unassign = useUnassignCupChild(truppId, eventId)

  const [name, setName] = useState('')
  // Vilket cup-lag som byter namn just nu, och den redigerade texten (#408).
  const [renaming, setRenaming] = useState<{ id: string; value: string } | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
  // Fokusera namnfältet när "Byt namn" fälls in (WCAG 2.4.3, `#484`). Bara ett fält renderas
  // åt gången (det cup-lag som byter namn), så en enda ref räcker. Nyckla på id:t så fokus sätts
  // när ett lag öppnas för namnbyte, inte vid varje tangenttryck.
  const renameRef = useRef<HTMLInputElement>(null)
  const renamingId = renaming?.id ?? null
  useEffect(() => {
    if (renamingId !== null) {
      renameRef.current?.focus()
    }
  }, [renamingId])

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
          <button type="submit" className="button button--action" disabled={create.isPending}>
            Skapa cup-lag
          </button>
        </form>
      )}

      {summary.teams.length > 0 && (
        <ul className="cup-teams__list">
          {summary.teams.map((team) => (
            <li key={team.id} className="cup-teams__team">
              {isAdmin && renaming?.id === team.id ? (
                <form
                  className="cup-teams__rename"
                  onSubmit={(event) => {
                    event.preventDefault()
                    const trimmed = renaming.value.trim()
                    if (trimmed === '') {
                      setFailure('Ge cup-laget ett namn.')
                      return
                    }
                    run(
                      rename
                        .mutateAsync({ cupTeamId: team.id, name: trimmed })
                        .then(() => setRenaming(null)),
                    )
                  }}
                >
                  <label htmlFor={`cup-team-rename-${team.id}`} className="visually-hidden">
                    Nytt namn på {team.name}
                  </label>
                  <input
                    ref={renameRef}
                    id={`cup-team-rename-${team.id}`}
                    type="text"
                    autoComplete="off"
                    maxLength={60}
                    value={renaming.value}
                    onChange={(event) => setRenaming({ id: team.id, value: event.target.value })}
                  />
                  <button
                    type="submit"
                    className="button button--small"
                    disabled={rename.isPending}
                  >
                    Spara
                  </button>
                  <button
                    type="button"
                    className="button button--small"
                    onClick={() => setRenaming(null)}
                  >
                    Avbryt
                  </button>
                </form>
              ) : (
                <div className="cup-teams__team-head">
                  <strong>{team.name}</strong>
                  {isAdmin && (
                    <span className="cup-teams__team-actions">
                      <button
                        type="button"
                        className="button button--small"
                        onClick={() => {
                          setFailure(null)
                          setRenaming({ id: team.id, value: team.name })
                        }}
                      >
                        Byt namn
                      </button>
                      <button
                        type="button"
                        className="button button--small"
                        disabled={remove.isPending}
                        onClick={() => run(remove.mutateAsync(team.id))}
                      >
                        Ta bort laget
                      </button>
                    </span>
                  )}
                </div>
              )}
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
