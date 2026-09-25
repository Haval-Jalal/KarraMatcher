import { useState } from 'react'

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

type View =
  | { kind: 'truppen' }
  | { kind: 'lag' }
  | { kind: 'team'; teamId: string }
  | { kind: 'child'; childId: string; backTo: 'truppen' | { teamId: string } }

/**
 * Barn & lag som en borra-in-vy (§KM.1, `#283`).
 *
 * <h3>Truppen och Lagen, inte en oändlig kolumn</h3>
 *
 * Tidigare låg hela rostern utfälld med alla kontroller synliga — det blev en oändlig scroll.
 * Nu finns två ingångar: <b>Truppen</b> (alla barn i en lista) och <b>Lag</b> (de fyra
 * färgerna). Man drar sig in i ett barn eller ett lag i stället för att scrolla förbi allt.
 * Ett barn visas alltid som "Liam J" — aldrig hela efternamnet.
 */
export function BarnOchLag({ truppId }: { truppId: string }) {
  const roster = useRoster(truppId)
  const [view, setView] = useState<View>({ kind: 'truppen' })

  if (roster.isLoading) {
    return <p className="state">Hämtar…</p>
  }

  if (roster.isError || !roster.data) {
    return (
      <p className="state state--error" role="alert">
        Kunde inte hämta barnen.
      </p>
    )
  }

  const { teams, children } = roster.data
  const rootIsLag =
    view.kind === 'lag' ||
    view.kind === 'team' ||
    (view.kind === 'child' && view.backTo !== 'truppen')

  return (
    <div className="barn-lag">
      <div className="subtabs" role="group" aria-label="Barn och lag">
        <button
          type="button"
          className={rootIsLag ? 'subtabs__tab' : 'subtabs__tab subtabs__tab--active'}
          aria-pressed={!rootIsLag}
          onClick={() => setView({ kind: 'truppen' })}
        >
          Truppen
        </button>
        <button
          type="button"
          className={rootIsLag ? 'subtabs__tab subtabs__tab--active' : 'subtabs__tab'}
          aria-pressed={rootIsLag}
          onClick={() => setView({ kind: 'lag' })}
        >
          Lag
        </button>
      </div>

      {view.kind === 'truppen' && (
        <TruppenList
          truppId={truppId}
          teams={teams}
          children={children}
          onOpenChild={(childId) => setView({ kind: 'child', childId, backTo: 'truppen' })}
        />
      )}

      {view.kind === 'lag' && (
        <LagList
          teams={teams}
          children={children}
          onOpenTeam={(teamId) => setView({ kind: 'team', teamId })}
        />
      )}

      {view.kind === 'team' && (
        <TeamChildren
          team={teams.find((team) => team.id === view.teamId) ?? null}
          children={children.filter((child) => child.teamId === view.teamId)}
          onBack={() => setView({ kind: 'lag' })}
          onOpenChild={(childId) =>
            setView({ kind: 'child', childId, backTo: { teamId: view.teamId } })
          }
        />
      )}

      {view.kind === 'child' && (
        <ChildDetail
          truppId={truppId}
          child={children.find((child) => child.id === view.childId) ?? null}
          teams={teams}
          onBack={() =>
            setView(
              view.backTo === 'truppen'
                ? { kind: 'truppen' }
                : { kind: 'team', teamId: view.backTo.teamId },
            )
          }
          onRemoved={() =>
            setView(
              view.backTo === 'truppen'
                ? { kind: 'truppen' }
                : { kind: 'team', teamId: view.backTo.teamId },
            )
          }
        />
      )}
    </div>
  )
}

/** En färg-prick + lagnamn (eller "Inget lag"). Färgen bär aldrig betydelsen ensam (§KM.1). */
function TeamTag({ team }: { team: RosterTeam | null }) {
  if (team === null) {
    return <span className="admin-muted">Inget lag</span>
  }

  return (
    <span className="team-tag-inline">
      <span className="admin-color" style={{ backgroundColor: team.colorHex }} aria-hidden="true" />
      {team.name}
    </span>
  )
}

function TruppenList({
  truppId,
  teams,
  children,
  onOpenChild,
}: {
  truppId: string
  teams: RosterTeam[]
  children: Child[]
  onOpenChild: (childId: string) => void
}) {
  const teamById = new Map(teams.map((team) => [team.id, team]))

  return (
    <div>
      <ul className="drill-list">
        {children.length === 0 && <li className="state">Inga barn i truppen än.</li>}
        {children.map((child) => (
          <li key={child.id}>
            <button
              type="button"
              className="drill-list__item"
              onClick={() => onOpenChild(child.id)}
            >
              <span className="drill-list__name">{child.displayName}</span>
              <TeamTag team={child.teamId !== null ? (teamById.get(child.teamId) ?? null) : null} />
              <span className="drill-list__chevron" aria-hidden="true">
                ›
              </span>
            </button>
          </li>
        ))}
      </ul>

      <AddChild truppId={truppId} teams={teams} />
    </div>
  )
}

function LagList({
  teams,
  children,
  onOpenTeam,
}: {
  teams: RosterTeam[]
  children: Child[]
  onOpenTeam: (teamId: string) => void
}) {
  const countFor = (teamId: string): number => children.filter((c) => c.teamId === teamId).length
  const unassigned = children.filter((c) => c.teamId === null).length

  return (
    <ul className="drill-list">
      {teams.map((team) => (
        <li key={team.id}>
          <button type="button" className="drill-list__item" onClick={() => onOpenTeam(team.id)}>
            <TeamTag team={team} />
            <span className="admin-muted">{countFor(team.id)} barn</span>
            <span className="drill-list__chevron" aria-hidden="true">
              ›
            </span>
          </button>
        </li>
      ))}
      {unassigned > 0 && (
        <li className="state">{unassigned} barn utan lag — koppla dem via Truppen.</li>
      )}
    </ul>
  )
}

function TeamChildren({
  team,
  children,
  onBack,
  onOpenChild,
}: {
  team: RosterTeam | null
  children: Child[]
  onBack: () => void
  onOpenChild: (childId: string) => void
}) {
  return (
    <div>
      <button type="button" className="drill-back" onClick={onBack}>
        ‹ Alla lag
      </button>

      <h3 className="drill-title">
        <TeamTag team={team} />
      </h3>

      <ul className="drill-list">
        {children.length === 0 && <li className="state">Inga barn i det här laget än.</li>}
        {children.map((child) => (
          <li key={child.id}>
            <button
              type="button"
              className="drill-list__item"
              onClick={() => onOpenChild(child.id)}
            >
              <span className="drill-list__name">{child.displayName}</span>
              <span className="drill-list__chevron" aria-hidden="true">
                ›
              </span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  )
}

function ChildDetail({
  truppId,
  child,
  teams,
  onBack,
  onRemoved,
}: {
  truppId: string
  child: Child | null
  teams: RosterTeam[]
  onBack: () => void
  onRemoved: () => void
}) {
  const update = useUpdateChild(truppId)
  const remove = useDeleteChild(truppId)
  const link = useLinkGuardian(truppId)
  const unlink = useUnlinkGuardian(truppId)

  const [guardianEmail, setGuardianEmail] = useState('')
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  const run = (action: Promise<unknown>) => {
    setFailure(null)
    void action.catch((error: unknown) => setFailure(messageOf(error)))
  }

  if (child === null) {
    // Barnet försvann under oss (t.ex. togs bort i en annan flik) — tillbaka till listan.
    return (
      <div>
        <button type="button" className="drill-back" onClick={onBack}>
          ‹ Tillbaka
        </button>
        <p className="state">Barnet finns inte längre.</p>
      </div>
    )
  }

  return (
    <div className="child-detail">
      <button type="button" className="drill-back" onClick={onBack}>
        ‹ Tillbaka
      </button>

      <h3 className="drill-title">{child.displayName}</h3>

      <section className="child-detail__block">
        <h4>Lag</h4>
        <label className="visually-hidden" htmlFor={`byt-lag-${child.id}`}>
          Byt lag för {child.displayName}
        </label>
        <select
          id={`byt-lag-${child.id}`}
          className="child-detail__select"
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
          <option value="">Inget lag</option>
          {teams.map((team) => (
            <option key={team.id} value={team.id}>
              {team.name}
            </option>
          ))}
        </select>
      </section>

      <section className="child-detail__block">
        <h4>Vårdnadshavare</h4>
        {child.guardians.length === 0 ? (
          <p className="admin-muted">Ingen vårdnadshavare kopplad än.</p>
        ) : (
          <ul className="admin-list">
            {child.guardians.map((guardian) => (
              <li key={guardian.accountId} className="admin-list__row">
                <span>
                  {guardian.displayName ?? guardian.email} <code>{guardian.email}</code>
                </span>
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
          <label htmlFor={`koppla-vh-${child.id}`}>Koppla en vårdnadshavare (adress)</label>
          <input
            id={`koppla-vh-${child.id}`}
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
      </section>

      <section className="child-detail__block">
        {confirmDelete ? (
          <div className="actions">
            <button
              type="button"
              className="button button--small"
              disabled={remove.isPending}
              onClick={() => run(remove.mutateAsync(child.id).then(onRemoved))}
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
          </div>
        ) : (
          <button
            type="button"
            className="button button--small button--danger"
            onClick={() => setConfirmDelete(true)}
          >
            Ta bort barn
          </button>
        )}
      </section>

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}
    </div>
  )
}

function AddChild({ truppId, teams }: { truppId: string; teams: RosterTeam[] }) {
  const create = useCreateChild(truppId)
  const [open, setOpen] = useState(false)
  const [firstName, setFirstName] = useState('')
  const [lastInitial, setLastInitial] = useState('')
  const [teamId, setTeamId] = useState('')
  const [failure, setFailure] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  const canSave = firstName.trim() !== '' && lastInitial.trim() !== ''

  if (!open) {
    return (
      <button type="button" className="button" onClick={() => setOpen(true)}>
        Lägg till barn
      </button>
    )
  }

  function save(): void {
    setFailure(null)
    setSuccess(null)
    const name = `${firstName.trim()} ${lastInitial.trim()}`.trim()
    void create
      .mutateAsync({
        firstName: firstName.trim(),
        lastInitial: lastInitial.trim(),
        teamId: teamId === '' ? null : teamId,
      })
      .then(() => {
        setSuccess(`${name} lades till.`)
        setFirstName('')
        setLastInitial('')
      })
      .catch((error: unknown) => setFailure(messageOf(error)))
  }

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        if (canSave) {
          save()
        }
      }}
    >
      <div className="form__field">
        <label htmlFor="nytt-barn-fornamn">Förnamn</label>
        <input
          id="nytt-barn-fornamn"
          type="text"
          value={firstName}
          onChange={(event) => {
            setFirstName(event.target.value)
            setSuccess(null)
          }}
        />
      </div>

      <div className="form__field">
        <label htmlFor="nytt-barn-initial">Efternamnets initial</label>
        <input
          id="nytt-barn-initial"
          type="text"
          maxLength={2}
          value={lastInitial}
          onChange={(event) => setLastInitial(event.target.value)}
        />
      </div>

      <div className="form__field">
        <label htmlFor="nytt-barn-lag">Lag (valfritt)</label>
        <select
          id="nytt-barn-lag"
          value={teamId}
          onChange={(event) => setTeamId(event.target.value)}
        >
          <option value="">Inget lag</option>
          {teams.map((team) => (
            <option key={team.id} value={team.id}>
              {team.name}
            </option>
          ))}
        </select>
      </div>

      {success !== null && (
        <p className="form__success" role="status">
          {success}
        </p>
      )}

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}

      <div className="actions">
        <button type="submit" className="button" disabled={!canSave || create.isPending}>
          {create.isPending ? 'Lägger till…' : 'Lägg till barn'}
        </button>
        <button type="button" className="button" onClick={() => setOpen(false)}>
          Stäng
        </button>
      </div>
    </form>
  )
}
