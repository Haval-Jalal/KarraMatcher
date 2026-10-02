import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'

// Djupimport (inte via feature-barreln): invitations/index re-exporterar AdminPage som i sin tur
// importerar BarnOchLag härifrån — en barrel-import hade blivit en cirkel mellan featurarna.
import { useCreateInvitation } from '@/features/invitations/useInvitations'
import { useCreateLag, useUpdateLag } from '@/features/lag'
import { ApiError } from '@/lib/api'
import { slugify } from '@/lib/slugify'

import type { Child, RosterTeam } from './childrenApi'
import {
  childrenKeys,
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

  // Borra in/ut byter hela panelinnehållet via setView, men utan detta faller fokus till <body>
  // när den klickade knappen avmonteras — en tangentbords-/skärmläsaranvändare släpps till toppen
  // utan besked (WCAG 2.4.3, `#482`). Flytta fokus till den nya vyns naturliga start: flik-knappen
  // för trupp/lag-listan, annars borra-vyns rubrik. Inte vid första renderingen (RootLayout har
  // redan satt fokus då).
  const firstRender = useRef(true)
  const panelRef = useRef<HTMLDivElement>(null)
  const truppenTabRef = useRef<HTMLButtonElement>(null)
  const lagTabRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    if (firstRender.current) {
      firstRender.current = false
      return
    }

    if (view.kind === 'truppen') {
      truppenTabRef.current?.focus()
    } else if (view.kind === 'lag') {
      lagTabRef.current?.focus()
    } else {
      panelRef.current?.querySelector<HTMLElement>('[data-view-focus]')?.focus()
    }
  }, [view])

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
    <div className="barn-lag" ref={panelRef}>
      <div className="subtabs" role="group" aria-label="Barn och lag">
        <button
          ref={truppenTabRef}
          type="button"
          className={rootIsLag ? 'subtabs__tab' : 'subtabs__tab subtabs__tab--active'}
          aria-pressed={!rootIsLag}
          onClick={() => setView({ kind: 'truppen' })}
        >
          Truppen
        </button>
        <button
          ref={lagTabRef}
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
          truppId={truppId}
          teams={teams}
          children={children}
          onOpenTeam={(teamId) => setView({ kind: 'team', teamId })}
        />
      )}

      {view.kind === 'team' && (
        <TeamChildren
          truppId={truppId}
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
  truppId,
  teams,
  children,
  onOpenTeam,
}: {
  truppId: string
  teams: RosterTeam[]
  children: Child[]
  onOpenTeam: (teamId: string) => void
}) {
  const countFor = (teamId: string): number => children.filter((c) => c.teamId === teamId).length
  const unassigned = children.filter((c) => c.teamId === null).length

  return (
    <div>
      <ul className="drill-list">
        {teams.length === 0 && (
          <li className="state">Inga färg-lag än. Skapa truppens färger nedan.</li>
        )}
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

      <AddLag truppId={truppId} />
    </div>
  )
}

function AddLag({ truppId }: { truppId: string }) {
  const create = useCreateLag(truppId)
  const client = useQueryClient()
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [colorHex, setColorHex] = useState('#1e3f8a')
  const [failure, setFailure] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  // Flytta fokus till första fältet när formuläret fälls in (WCAG 2.4.3, `#484`).
  const nameRef = useRef<HTMLInputElement>(null)
  useEffect(() => {
    if (open) {
      nameRef.current?.focus()
    }
  }, [open])

  if (!open) {
    return (
      <button type="button" className="button" onClick={() => setOpen(true)}>
        Skapa färg-lag
      </button>
    )
  }

  function save(): void {
    setFailure(null)
    setSuccess(null)
    const added = name.trim()
    void create
      .mutateAsync({ name: added, colorHex, slug: slugify(added) })
      .then(() => {
        // Roster-frågan bär lagen som Lag-vyn visar — uppdatera den så det nya laget syns.
        void client.invalidateQueries({ queryKey: childrenKeys.roster(truppId) })
        setSuccess(`${added} skapades.`)
        setName('')
      })
      .catch((error: unknown) => setFailure(messageOf(error)))
  }

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        if (name.trim() !== '') {
          save()
        }
      }}
    >
      <div className="form__field">
        <label htmlFor="nytt-lag-namn">Färgens namn</label>
        <input
          ref={nameRef}
          id="nytt-lag-namn"
          type="text"
          value={name}
          onChange={(event) => {
            setName(event.target.value)
            setSuccess(null)
          }}
        />
      </div>

      <div className="form__field">
        <label htmlFor="nytt-lag-farg">Färg</label>
        <input
          id="nytt-lag-farg"
          type="color"
          value={colorHex}
          onChange={(event) => setColorHex(event.target.value)}
        />
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
        <button
          type="submit"
          className="button button--action"
          disabled={name.trim() === '' || create.isPending}
        >
          {create.isPending ? 'Skapar…' : 'Skapa färg-lag'}
        </button>
        <button type="button" className="button" onClick={() => setOpen(false)}>
          Stäng
        </button>
      </div>
    </form>
  )
}

function TeamChildren({
  truppId,
  team,
  children,
  onBack,
  onOpenChild,
}: {
  truppId: string
  team: RosterTeam | null
  children: Child[]
  onBack: () => void
  onOpenChild: (childId: string) => void
}) {
  const [editing, setEditing] = useState(false)

  return (
    <div>
      <button type="button" className="drill-back" onClick={onBack}>
        ‹ Alla lag
      </button>

      <div className="drill-title-row">
        <h3 className="drill-title" tabIndex={-1} data-view-focus>
          <TeamTag team={team} />
        </h3>
        {team !== null && (
          <button
            type="button"
            className="icon-button"
            aria-label={`Ändra ${team.name}`}
            aria-expanded={editing}
            onClick={() => setEditing((open) => !open)}
          >
            <span aria-hidden="true">⚙️</span>
          </button>
        )}
      </div>

      {editing && team !== null && (
        <EditLag truppId={truppId} team={team} onDone={() => setEditing(false)} />
      )}

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

/**
 * Kugghjulets redigering av ett lag (`#redesign`): namn och färg. Slugen ändras aldrig — den
 * lever i delade länkar. Servern verifierar att laget hör till truppen (IDOR-vakt, §KM.3).
 */
function EditLag({
  truppId,
  team,
  onDone,
}: {
  truppId: string
  team: RosterTeam
  onDone: () => void
}) {
  const update = useUpdateLag(truppId)
  const client = useQueryClient()
  const [name, setName] = useState(team.name)
  const [colorHex, setColorHex] = useState(team.colorHex)
  const [failure, setFailure] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  // Fälls in via kugghjulet — fokusera namnfältet direkt (WCAG 2.4.3, `#484`).
  const nameRef = useRef<HTMLInputElement>(null)
  useEffect(() => {
    nameRef.current?.focus()
  }, [])

  function save(): void {
    setFailure(null)
    setSuccess(null)
    const trimmed = name.trim()
    void update
      .mutateAsync({ id: team.id, name: trimmed, colorHex })
      .then(() => {
        // Rostern bär lagen som Lag-vyn visar — uppdatera den så namn/färg slår igenom direkt.
        void client.invalidateQueries({ queryKey: childrenKeys.roster(truppId) })
        setSuccess('Laget uppdaterades.')
      })
      .catch((error: unknown) => setFailure(messageOf(error)))
  }

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        if (name.trim() !== '') {
          save()
        }
      }}
    >
      <div className="form__field">
        <label htmlFor={`lag-namn-${team.id}`}>Lagets namn</label>
        <input
          ref={nameRef}
          id={`lag-namn-${team.id}`}
          type="text"
          value={name}
          onChange={(event) => {
            setName(event.target.value)
            setSuccess(null)
          }}
        />
      </div>

      <div className="form__field">
        <label htmlFor={`lag-farg-${team.id}`}>Färg</label>
        <input
          id={`lag-farg-${team.id}`}
          type="color"
          value={colorHex}
          onChange={(event) => {
            setColorHex(event.target.value)
            setSuccess(null)
          }}
        />
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
        <button
          type="submit"
          className="button button--action"
          disabled={name.trim() === '' || update.isPending}
        >
          {update.isPending ? 'Sparar…' : 'Spara'}
        </button>
        <button type="button" className="button" onClick={onDone}>
          Stäng
        </button>
      </div>
    </form>
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
  const invite = useCreateInvitation(truppId)

  const [guardianEmail, setGuardianEmail] = useState('')
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  // Adressen vi kan erbjuda en inbjudan för, satt när en koppling faller på att föräldern inte
  // finns eller inte gått med i truppen än (`#redesign`, Fas 4).
  const [inviteOffer, setInviteOffer] = useState<string | null>(null)

  const run = (action: Promise<unknown>) => {
    setFailure(null)
    void action.catch((error: unknown) => setFailure(messageOf(error)))
  }

  const clearNotices = () => {
    setFailure(null)
    setSuccess(null)
    setInviteOffer(null)
  }

  function handleLink(): void {
    if (child === null) {
      return
    }

    clearNotices()
    const email = guardianEmail.trim()
    void link
      .mutateAsync({ childId: child.id, email })
      .then(() => {
        setGuardianEmail('')
        setSuccess('Vårdnadshavaren kopplades.')
      })
      .catch((error: unknown) => {
        // 409 pga saknat samtycke (§KM.6) är inte en återvändsgränd: säg vad som behöver hända.
        // Föräldern nudgas numera på Hem att samtycka (#597), så peka adminen dit.
        if (error instanceof ApiError && error.status === 409 && /samtycke/i.test(error.message)) {
          setFailure(
            'Vårdnadshavaren har inte godkänt hanteringen av barnets uppgifter än (§KM.6). Be hen ' +
              'logga in och godkänna på Mitt konto — appen påminner dem också. Koppla sedan igen.',
          )

          return
        }

        setFailure(messageOf(error))
        // 400 = kontot finns inte, eller föräldern har inte gått med i truppen än. Då hjälper en
        // inbjudan; 409 (samtycke saknas / redan kopplad) gör den inte, så erbjud den inte då.
        if (error instanceof ApiError && error.status === 400) {
          setInviteOffer(email)
        }
      })
  }

  function handleInvite(email: string): void {
    setFailure(null)
    setSuccess(null)
    void invite
      // Barnet finns redan här med sitt lag; inbjudan behöver inget lag-förslag (#408).
      .mutateAsync({ email, teamId: null })
      .then(() => {
        setInviteOffer(null)
        setSuccess(
          `Inbjudan skapad — ${email} får ett mejl. Koppla vårdnadshavaren när hen gått med och godkänt samtycket.`,
        )
      })
      .catch((error: unknown) => setFailure(messageOf(error)))
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

      <h3 className="drill-title" tabIndex={-1} data-view-focus>
        {child.displayName}
      </h3>

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
          onChange={(event) => {
            // Kvittera lagbytet (`#487`) — select:en är bunden till serverns child.teamId och kan
            // snäppa tillbaka tills refetchen landar; ett kort besked säger att det gick fram.
            clearNotices()
            void update
              .mutateAsync({
                id: child.id,
                data: {
                  firstName: child.firstName,
                  lastInitial: child.lastInitial,
                  teamId: event.target.value === '' ? null : event.target.value,
                },
              })
              .then(() => setSuccess('Lagbytet sparades.'))
              .catch((error: unknown) => setFailure(messageOf(error)))
          }}
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
            maxLength={320}
            value={guardianEmail}
            onChange={(event) => {
              setGuardianEmail(event.target.value)
              clearNotices()
            }}
          />
        </div>
        <div className="actions">
          <button
            type="button"
            className="button button--small"
            disabled={link.isPending || guardianEmail.trim() === ''}
            onClick={handleLink}
          >
            Koppla vårdnadshavare
          </button>
        </div>

        {inviteOffer !== null && (
          <div className="child-detail__invite">
            <p className="admin-muted">
              {inviteOffer} är inte medlem i truppen än. Bjud in vårdnadshavaren, så kan du koppla
              hen efter att hen gått med och godkänt samtycket.
            </p>
            <div className="actions">
              <button
                type="button"
                className="button button--small button--action"
                disabled={invite.isPending}
                onClick={() => handleInvite(inviteOffer)}
              >
                {invite.isPending ? 'Bjuder in…' : `Bjud in ${inviteOffer} till truppen`}
              </button>
            </div>
          </div>
        )}
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
  // Flytta fokus till förnamnsfältet när formuläret fälls in (WCAG 2.4.3, `#484`).
  const firstNameRef = useRef<HTMLInputElement>(null)
  useEffect(() => {
    if (open) {
      firstNameRef.current?.focus()
    }
  }, [open])

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
        // Nollställ lag-valet så nästa barn inte tyst ärver föregående lag (`#487`).
        setTeamId('')
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
          ref={firstNameRef}
          id="nytt-barn-fornamn"
          type="text"
          maxLength={50}
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
        <button
          type="submit"
          className="button button--action"
          disabled={!canSave || create.isPending}
        >
          {create.isPending ? 'Lägger till…' : 'Lägg till barn'}
        </button>
        <button type="button" className="button" onClick={() => setOpen(false)}>
          Stäng
        </button>
      </div>
    </form>
  )
}
