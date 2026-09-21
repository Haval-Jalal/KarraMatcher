import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { useRoster } from '@/features/children'
import { ApiError } from '@/lib/api'
import { hasKickedOff } from '@/lib/time'

import type { AttendanceReply, MyChildInvitation } from './attendanceApi'
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
  kickoffUtc,
}: {
  eventId: string
  truppId: string
  teamName: string
  kickoffUtc: string
}) {
  const { status, isSuperAdmin, adminOf } = useAuth()
  const isSignedIn = status === 'inloggad'
  const isAdmin = isSuperAdmin || adminOf.includes(truppId)

  const my = useMyKallelse(eventId, isSignedIn)

  if (!isSignedIn) {
    return null
  }

  const gateOff = my.error instanceof ApiError && my.error.status === 404
  const myChildren = gateOff ? [] : (my.data?.children ?? [])
  const guardianVisible = !gateOff && (my.data?.callOpen ?? false) && myChildren.length > 0

  // Gäst, avslagen kallelse eller inga egna kallade barn — och inte admin: ingenting.
  if (!isAdmin && !guardianVisible) {
    return null
  }

  const closed = hasKickedOff(kickoffUtc)

  return (
    <section className="attendance" aria-labelledby="kallelse">
      <h2 id="kallelse" className="attendance__heading">
        Kallelse
      </h2>

      {guardianVisible && (
        <GuardianReplies eventId={eventId} childInvitations={myChildren} closed={closed} />
      )}

      {isAdmin && <AdminKallelse truppId={truppId} eventId={eventId} teamName={teamName} />}
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
}: {
  truppId: string
  eventId: string
  teamName: string
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
  const [remindMsg, setRemindMsg] = useState<string | null>(null)

  const rosterChildren = roster.data?.children ?? []
  const rosterTeams = roster.data?.teams ?? []
  const current = selected ?? new Set(summary.data?.children.map((child) => child.childId) ?? [])

  function toggle(childId: string): void {
    const next = new Set(current)
    if (next.has(childId)) {
      next.delete(childId)
    } else {
      next.add(childId)
    }
    setSelected(next)
  }

  function selectTeam(): void {
    setSelected(
      new Set(rosterChildren.filter((child) => child.teamName === teamName).map((c) => c.id)),
    )
  }

  function selectAll(): void {
    setSelected(new Set(rosterChildren.map((child) => child.id)))
  }

  function submit(): void {
    setFailure(null)
    send.mutate([...current], {
      onError: (error) =>
        setFailure(
          error instanceof ApiError ? error.message : 'Kallelsen gick inte att skicka just nu.',
        ),
    })
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

      {roster.isLoading && <p className="state">Hämtar truppen…</p>}
      {roster.isError && (
        <p className="state state--error" role="alert">
          Kunde inte hämta truppens barn.
        </p>
      )}

      {roster.data && (
        <>
          <div className="actions">
            <button type="button" className="button button--small" onClick={selectTeam}>
              Hela laget {teamName}
            </button>
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

          <div className="actions">
            <button type="button" className="button" disabled={send.isPending} onClick={submit}>
              {send.isPending ? 'Skickar…' : 'Skicka kallelse'}
            </button>
          </div>
        </>
      )}

      {summary.data && summary.data.callOpen && (
        <div className="attendance__summary">
          <h4 className="attendance__subheading">Svar hittills</h4>
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
