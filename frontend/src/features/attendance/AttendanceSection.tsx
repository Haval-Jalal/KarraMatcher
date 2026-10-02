import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { useRoster } from '@/features/children'
import { useAutoFocus } from '@/hooks/useAutoFocus'
import { ApiError } from '@/lib/api'
import { hasKickedOff } from '@/lib/time'

import { answeredChildrenBeingDropped } from './attendanceApi'
import type { AttendanceReply, MyChildInvitation } from './attendanceApi'
import { CoachKallelse } from './CoachKallelse'
import {
  useKallelseSummary,
  useMyKallelse,
  useRemind,
  useRespond,
  useSetKallelse,
} from './useAttendance'

/**
 * Den riktade kallelsen på händelsesidan (§KM.7, `#199`).
 *
 * <h3>Två vyer</h3>
 *
 * En vårdnadshavare ser sina egna kallade barn och svarar Ja/Nej per barn. En admin för
 * truppen ser i stället en väljare — barn ur alla lag, så ett lag kan fyllas på — och en
 * sammanställning över vilka som svarat vad.
 *
 * <h3>Osynlig tills klubben slår på den</h3>
 *
 * Servern svarar `404` på vårdnadshavarens vy för ett lag där kallelsen är avslagen (§KM.7).
 * Då renderar den här sektionen ingenting för en vanlig medlem.
 */
export function AttendanceSection({
  eventId,
  truppId,
  teamName,
  teamSlug,
  kickoffUtc,
}: {
  eventId: string
  truppId: string
  teamName: string
  teamSlug: string
  kickoffUtc: string
}) {
  const { status, isSuperAdmin, adminOf, canManage } = useAuth()
  const isSignedIn = status === 'inloggad'
  const isAdmin = isSuperAdmin || adminOf.includes(truppId)
  // En färg-lag-tränare (inte admin) för just den här händelsens lag får också kalla (`#redesign`).
  // Servern är grinden (CoachOfTeam); det här styr bara vilken panel som visas. En trupp-vid
  // händelse saknar lag (teamSlug === ''), och tränar-panelen är lag-scopad — rendera den aldrig
  // då (annars `/teams//kallelse-roster`, `#485`).
  const isCoach = teamSlug !== '' && !isAdmin && canManage(teamSlug, truppId)

  const my = useMyKallelse(eventId, isSignedIn)

  if (!isSignedIn) {
    return null
  }

  const gateOff = my.error instanceof ApiError && my.error.status === 404
  const myChildren = gateOff ? [] : (my.data?.children ?? [])
  const guardianVisible = !gateOff && (my.data?.callOpen ?? false) && myChildren.length > 0

  // Medan den egna kallelsen hämtas vet vi ännu inte om föräldern har en. Visa en
  // laddnings-platshållare i stället för ingenting, annars kan en verklig kallelse se ut att
  // saknas på en långsam uppkoppling (#396). Admin/tränare har alltid sin panel och berörs inte;
  // en 404 (kallelsen avslagen för laget) är inte "pending" och ska fortsatt ge tomt.
  const guardianPending = !isAdmin && !isCoach && !gateOff && my.isPending

  // Ett nät-/5xx-fel (inte 404-grinden) på den egna kallelsen. Utan den här grenen blev både
  // guardianVisible och guardianPending false och hela "Kallelse"-sektionen försvann tyst — en
  // kallad förälder på dåligt nät såg ingenting (#533). Visa fel + försök igen i stället.
  const guardianError = !isAdmin && !isCoach && !gateOff && my.isError

  // Gäst, inga egna kallade barn — och varken admin eller tränare: ingenting.
  if (!isAdmin && !isCoach && !guardianVisible && !guardianPending && !guardianError) {
    return null
  }

  const closed = hasKickedOff(kickoffUtc)

  return (
    <section className="attendance" aria-labelledby="kallelse">
      <h2 id="kallelse" className="attendance__heading">
        Kallelse
      </h2>

      {guardianPending && (
        <p className="state" role="status">
          Hämtar kallelsen…
        </p>
      )}

      {guardianError && (
        <div className="state state--error" role="alert">
          <p>
            {my.error instanceof ApiError && my.error.offline
              ? 'Ingen anslutning. Kallelsen kan inte hämtas just nu.'
              : 'Kunde inte hämta kallelsen just nu.'}
          </p>
          <button
            type="button"
            className="button"
            disabled={my.isFetching}
            onClick={() => {
              void my.refetch()
            }}
          >
            {my.isFetching ? 'Försöker…' : 'Försök igen'}
          </button>
        </div>
      )}

      {guardianVisible && (
        <GuardianReplies eventId={eventId} childInvitations={myChildren} closed={closed} />
      )}

      {isAdmin && (
        <AdminKallelse
          truppId={truppId}
          eventId={eventId}
          teamName={teamName}
          hasTeam={teamSlug !== ''}
        />
      )}

      {isCoach && <CoachKallelse slug={teamSlug} eventId={eventId} teamName={teamName} />}
    </section>
  )
}

function GuardianReplies({
  eventId,
  childInvitations,
  closed,
}: {
  eventId: string
  childInvitations: MyChildInvitation[]
  closed: boolean
}) {
  const respond = useRespond(eventId)
  const [failed, setFailed] = useState(false)

  function answer(childId: string, reply: AttendanceReply): void {
    setFailed(false)
    respond.mutate({ childId, reply }, { onError: () => setFailed(true) })
  }

  return (
    <div className="attendance__guardian">
      <p className="attendance__note">
        {closed
          ? 'Händelsen har börjat — svaren går inte längre att ändra.'
          : 'Svara för varje barn. Du kan ändra ända fram till start.'}
      </p>

      <ul className="attendance__children">
        {childInvitations.map((child) => (
          <li key={child.childId} className="attendance__child">
            <span className="attendance__child-name">{child.displayName}</span>
            <span
              className="attendance__reply"
              role="group"
              aria-label={`Svar för ${child.displayName}`}
            >
              <button
                type="button"
                className="button button--small"
                aria-pressed={child.reply === 'Coming'}
                disabled={closed || respond.isPending}
                onClick={() => {
                  answer(child.childId, 'Coming')
                }}
              >
                Ja
              </button>
              <button
                type="button"
                className="button button--small"
                aria-pressed={child.reply === 'NotComing'}
                disabled={closed || respond.isPending}
                onClick={() => {
                  answer(child.childId, 'NotComing')
                }}
              >
                Nej
              </button>
            </span>
          </li>
        ))}
      </ul>

      {failed && (
        <p className="state state--error" role="alert">
          Svaret gick inte att spara just nu. Försök igen om en stund.
        </p>
      )}
    </div>
  )
}

function AdminKallelse({
  truppId,
  eventId,
  teamName,
  hasTeam,
}: {
  truppId: string
  eventId: string
  teamName: string
  /**
   * Har händelsen ett färg-lag? En trupp-övergripande händelse (utan lag, `#332`) får
   * `teamName` = truppens namn — då matchar "Hela laget {namn}" inga barn (barnens teamName är
   * färg-lag, aldrig truppnamnet), så knappen döljs och bara "Hela truppen" visas (`#477`).
   */
  hasTeam: boolean
}) {
  const roster = useRoster(truppId)
  const summary = useKallelseSummary(truppId, eventId, true)
  const send = useSetKallelse(truppId, eventId)
  const remind = useRemind(truppId, eventId)

  // null tills adminen rört urvalet: då speglar vyn de barn som redan är kallade (ur
  // sammanställningen). Ett urval härleds alltså utan en seedande effekt — first-render och
  // en sen laddad sammanställning ger båda rätt förkryssning.
  const [selected, setSelected] = useState<Set<string> | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
  // Kvitto på att kallelsen faktiskt skickades — annars vet adminen inte om något hände, och en
  // skärmläsaranvändare får ingen signal (#394). role="status" annonserar det.
  const [sentMsg, setSentMsg] = useState<string | null>(null)
  const [remindMsg, setRemindMsg] = useState<string | null>(null)
  // Namnen på svarade barn som urvalet skulle kasta — sätts vid första Skicka och kräver en
  // bekräftelse innan svaren raderas (`#472`). Nollas så fort urvalet ändras igen.
  const [dropWarning, setDropWarning] = useState<string[] | null>(null)
  // Varningen ersätter "Skicka kallelse"-knappen; flytta fokus dit (WCAG 2.4.3, #598).
  const dropWarningRef = useAutoFocus<HTMLParagraphElement>(dropWarning !== null)

  const rosterChildren = roster.data?.children ?? []
  const rosterTeams = roster.data?.teams ?? []
  const current = selected ?? new Set(summary.data?.children.map((child) => child.childId) ?? [])

  function toggle(childId: string): void {
    setSentMsg(null)
    setDropWarning(null)
    const next = new Set(current)
    if (next.has(childId)) {
      next.delete(childId)
    } else {
      next.add(childId)
    }
    setSelected(next)
  }

  function selectTeam(): void {
    setSentMsg(null)
    setDropWarning(null)
    setSelected(
      new Set(rosterChildren.filter((child) => child.teamName === teamName).map((c) => c.id)),
    )
  }

  function selectAll(): void {
    setSentMsg(null)
    setDropWarning(null)
    setSelected(new Set(rosterChildren.map((child) => child.id)))
  }

  function doSend(): void {
    setDropWarning(null)
    send.mutate([...current], {
      onSuccess: () => setSentMsg('Kallelsen är skickad.'),
      onError: (error) =>
        setFailure(
          error instanceof ApiError ? error.message : 'Kallelsen gick inte att skicka just nu.',
        ),
    })
  }

  function submit(): void {
    setFailure(null)
    setSentMsg(null)

    // Varna en gång innan svarade barn kastas ur kallelsen (full synk, `#472`). Andra klicket
    // (Skicka ändå) går via doSend direkt.
    const dropped = answeredChildrenBeingDropped(summary.data?.children ?? [], current)
    if (dropped.length > 0) {
      setDropWarning(dropped)
      return
    }

    doSend()
  }

  function nudge(): void {
    setRemindMsg(null)
    remind.mutate(undefined, {
      onSuccess: (result) =>
        setRemindMsg(
          result.reminded === 1
            ? 'Påminde 1 barns vårdnadshavare.'
            : `Påminde ${String(result.reminded)} barns vårdnadshavare.`,
        ),
      onError: () => setRemindMsg('Påminnelsen gick inte att skicka just nu.'),
    })
  }

  const groups = [
    ...rosterTeams.map((team) => ({
      key: team.id,
      name: team.name,
      colorHex: team.colorHex,
      children: rosterChildren.filter((child) => child.teamId === team.id),
    })),
    {
      key: 'otilldelade',
      name: 'Otilldelade',
      colorHex: null as string | null,
      children: rosterChildren.filter((child) => child.teamId === null),
    },
  ]

  return (
    <div className="attendance__admin">
      <h3 className="attendance__subheading">Skicka kallelse</h3>

      {roster.isLoading && (
        <p className="state" role="status">
          Hämtar truppen…
        </p>
      )}
      {roster.isError && (
        <p className="state state--error" role="alert">
          {roster.error instanceof ApiError && roster.error.offline
            ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
            : 'Kunde inte hämta truppens barn.'}
        </p>
      )}

      {/*
        Väljaren och skicka-knappen väntar på BÅDE rostern och sammanställningen. Skickandet är en
        full synk: en tom uppsättning tar bort varje befintlig kallelse och dess svar. Förr kunde
        rostern vinna kapplöpningen, väljaren renderas med sammanställningen ännu ohämtad (current
        tom) och ett tryck nollställa allt (#392). Nu speglar current alltid de redan kallade barnen
        innan knappen finns.
      */}
      {roster.data && summary.isPending && (
        <p className="state" role="status">
          Hämtar svar…
        </p>
      )}
      {roster.data && summary.isError && (
        <p className="state state--error" role="alert">
          Kunde inte hämta vilka som redan är kallade. Ladda om och försök igen — kallelsen skickas
          inte förrän den listan hämtats, så att inga svar råkar nollställas.
        </p>
      )}

      {roster.data && summary.isSuccess && (
        <>
          <div className="actions">
            {/* En trupp-övergripande händelse har inget lag att välja — bara "Hela truppen" (`#477`). */}
            {hasTeam && (
              <button type="button" className="button button--small" onClick={selectTeam}>
                Hela laget {teamName}
              </button>
            )}
            <button type="button" className="button button--small" onClick={selectAll}>
              Hela truppen
            </button>
          </div>

          {groups.map((group) => (
            <div key={group.key} className="roster-group">
              <h4>
                {group.colorHex !== null && (
                  <span
                    className="admin-color"
                    style={{ backgroundColor: group.colorHex }}
                    aria-hidden="true"
                  />
                )}
                {group.name}
              </h4>
              <ul className="admin-list">
                {group.children.length === 0 && <li className="state">Inga barn här.</li>}
                {group.children.map((child) => (
                  <li key={child.id} className="admin-list__row">
                    <label>
                      <input
                        type="checkbox"
                        checked={current.has(child.id)}
                        onChange={() => {
                          toggle(child.id)
                        }}
                      />{' '}
                      {child.displayName}
                    </label>
                  </li>
                ))}
              </ul>
            </div>
          ))}

          {failure !== null && (
            <p className="state state--error" role="alert">
              {failure}
            </p>
          )}

          {dropWarning !== null && (
            <div className="state state--error" role="alert">
              <p tabIndex={-1} ref={dropWarningRef}>
                {dropWarning.length === 1
                  ? '1 barn som redan svarat tas bort ur kallelsen och förlorar sitt svar:'
                  : `${String(dropWarning.length)} barn som redan svarat tas bort ur kallelsen och förlorar sina svar:`}{' '}
                {dropWarning.join(', ')}.
              </p>
              <div className="actions">
                <button type="button" className="button" disabled={send.isPending} onClick={doSend}>
                  {send.isPending ? 'Skickar…' : 'Skicka ändå'}
                </button>
                <button
                  type="button"
                  className="button button--small"
                  onClick={() => setDropWarning(null)}
                >
                  Avbryt
                </button>
              </div>
            </div>
          )}

          {dropWarning === null && (
            <div className="actions">
              <button type="button" className="button" disabled={send.isPending} onClick={submit}>
                {send.isPending ? 'Skickar…' : 'Skicka kallelse'}
              </button>
            </div>
          )}

          {sentMsg !== null && (
            <p className="state" role="status">
              {sentMsg}
            </p>
          )}
        </>
      )}

      {summary.data && summary.data.callOpen && (
        <div className="attendance__summary">
          <h3 className="attendance__subheading">Svar hittills</h3>
          <p className="attendance__totals">
            <strong>{summary.data.coming}</strong> kommer, {summary.data.notComing} kan inte,{' '}
            {summary.data.notAnswered} har inte svarat
          </p>

          {summary.data.children.length === 0 ? (
            <p className="attendance__note">Inga barn är kallade än.</p>
          ) : (
            <ul className="admin-list">
              {summary.data.children.map((child) => (
                <li key={child.childId} className="admin-list__row">
                  <span>
                    {child.colorHex !== null && (
                      <span
                        className="admin-color"
                        style={{ backgroundColor: child.colorHex }}
                        aria-hidden="true"
                      />
                    )}
                    {child.displayName}
                  </span>
                  <span>{replyLabel(child.reply)}</span>
                </li>
              ))}
            </ul>
          )}

          {summary.data.notAnswered > 0 && (
            <div className="actions">
              <button
                type="button"
                className="button button--small"
                disabled={remind.isPending}
                onClick={nudge}
              >
                {remind.isPending ? 'Påminner…' : 'Påminn dem som inte svarat'}
              </button>
            </div>
          )}

          {remindMsg !== null && (
            <p className="attendance__note" role="status">
              {remindMsg}
            </p>
          )}
        </div>
      )}
    </div>
  )
}

function replyLabel(reply: AttendanceReply | null): string {
  switch (reply) {
    case 'Coming':
      return 'Ja'
    case 'NotComing':
      return 'Nej'
    default:
      return '–'
  }
}
