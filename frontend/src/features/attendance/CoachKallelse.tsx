import { useState } from 'react'

import { useAutoFocus } from '@/hooks/useAutoFocus'
import { ApiError } from '@/lib/api'

import { answeredChildrenBeingDropped } from './attendanceApi'
import type { AttendanceReply } from './attendanceApi'
import {
  useCoachKallelseRoster,
  useRemindTeam,
  useSetTeamKallelse,
  useTeamKallelseSummary,
} from './useAttendance'

/**
 * Färg-lag-tränarens kallelse-panel (§KM.7, `#redesign`).
 *
 * <para>
 * Samma vy som adminens, men lag-scopad: tränaren skickar för <b>sitt eget lags</b> händelse
 * och väljer barn ur <b>hela truppen</b> (fyll-på av individer ur andra färg-lag). Servern är
 * grinden — <c>CoachOfTeam</c> + att händelsen hör till laget; det här speglar bara det.
 * </para>
 */
export function CoachKallelse({
  slug,
  eventId,
  teamName,
}: {
  slug: string
  eventId: string
  teamName: string
}) {
  const roster = useCoachKallelseRoster(slug, true)
  const summary = useTeamKallelseSummary(slug, eventId, true)
  const send = useSetTeamKallelse(slug, eventId)
  const remind = useRemindTeam(slug, eventId)

  // null tills tränaren rört urvalet: då speglar vyn de barn som redan är kallade (ur summeringen).
  const [selected, setSelected] = useState<Set<string> | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
  // Kvitto på att kallelsen skickades — annars vet tränaren (och en skärmläsare) inte att det
  // gick fram och kan skicka om i osäkerhet (`#473`, speglar admin-panelens #394-kvitto).
  const [sentMsg, setSentMsg] = useState<string | null>(null)
  const [remindMsg, setRemindMsg] = useState<string | null>(null)
  // Namnen på svarade barn som urvalet skulle kasta — kräver en bekräftelse innan svaren
  // raderas (`#472`). Nollas så fort urvalet ändras igen.
  const [dropWarning, setDropWarning] = useState<string[] | null>(null)
  // Varningen ersätter "Skicka kallelse"-knappen; flytta fokus dit (WCAG 2.4.3, #598).
  const dropWarningRef = useAutoFocus<HTMLParagraphElement>(dropWarning !== null)

  const rosterChildren = roster.data?.children ?? []
  const rosterTeams = roster.data?.teams ?? []
  const current = selected ?? new Set(summary.data?.children.map((child) => child.childId) ?? [])
  const ownTeamId = rosterTeams.find((team) => team.name === teamName)?.id ?? null

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
      new Set(rosterChildren.filter((child) => child.teamId === ownTeamId).map((c) => c.id)),
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

    // Varna en gång innan svarade barn kastas ur kallelsen (full synk, `#472`).
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

      {roster.isPending && (
        <p className="state" role="status">
          Hämtar truppen…
        </p>
      )}
      {roster.isError && (
        <p className="state state--error" role="alert">
          {roster.error instanceof ApiError && roster.error.offline
            ? 'Ingen anslutning. Försök igen.'
            : 'Kunde inte hämta truppens barn.'}
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
            <fieldset key={group.key} className="roster-group">
              {/* fieldset/legend så en skärmläsare hör vilket lag en kryssruta tillhör (#605). */}
              <legend>
                {group.colorHex !== null && (
                  <span
                    className="admin-color"
                    style={{ backgroundColor: group.colorHex }}
                    aria-hidden="true"
                  />
                )}
                {group.name}
              </legend>
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
            </fieldset>
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
      return 'Inget svar'
  }
}
