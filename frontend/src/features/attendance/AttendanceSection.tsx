import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { ApiError } from '@/lib/api'
import { hasKickedOff } from '@/lib/time'

import { openAttendanceCall, remindNonResponders, type AttendanceStatus } from './attendanceApi'
import { AttendanceResponseForm } from './AttendanceResponseForm'
import {
  attendanceStateQueryKey,
  attendanceSummaryQueryKey,
  useAttendanceState,
  useAttendanceSummary,
} from './useAttendance'

/**
 * Kallelsen på matchsidan (`#57`, `#58`, §KM.7).
 *
 * <h3>Osynlig tills klubben slår på den</h3>
 *
 * Kallelsen levereras avstängd. Servern svarar `404` för ett lag där den är av, och då
 * renderar den här sektionen ingenting alls — inte en tom rubrik, inte en inaktiv knapp.
 * Funktionen finns helt enkelt inte förrän en administratör slår på den (§KM.7).
 *
 * <h3>Tränaren kallar, den vuxna svarar, tränaren ser summan</h3>
 *
 * Är kallelsen på men inte öppnad för matchen ser en tränare knappen att kalla. När den är
 * öppnad ser alla svarsformuläret — även tränaren, som också kan ha en familj som ska med —
 * och tränaren ser dessutom summeringen: hur många som kommer, och vilka vuxna som svarat
 * (`#58`). Ingen lista över dem som *inte* svarat: den kräver en koppling som inte finns än
 * (§KM.1), och byggs i `#63`.
 */
export function AttendanceSection({
  matchId,
  teamSlug,
  kickoffUtc,
}: {
  matchId: string
  teamSlug: string
  kickoffUtc: string
}) {
  const { status, canManage } = useAuth()
  const queryClient = useQueryClient()
  const [calling, setCalling] = useState(false)
  const [callFailed, setCallFailed] = useState(false)
  const [reminding, setReminding] = useState(false)
  const [remindResult, setRemindResult] = useState<string | null>(null)

  const isSignedIn = status === 'inloggad'
  const isManager = canManage(teamSlug)

  const { data, isPending, error } = useAttendanceState(matchId, isSignedIn)

  // Summeringen hämtas bara för en tränare med en öppnad kallelse — annars finns inget att
  // summera, och anropet skulle ändå svara 403 eller 404.
  const { data: summary } = useAttendanceSummary(
    teamSlug,
    matchId,
    isSignedIn && isManager && data?.callOpen === true,
  )

  // Gästen och den vars lag saknar kallelsen (404) ser ingenting — funktionen finns inte
  // för dem (§KM.7).
  if (!isSignedIn) {
    return null
  }

  if (error instanceof ApiError && error.status === 404) {
    return null
  }

  if (data === undefined) {
    // Ett riktigt fel är värt en rad, men inte en hel felruta som stjäl matchsidan — resten
    // av matchen står kvar. Under själva hämtningen visas ingenting, så inget hoppar till.
    if (error && !isPending) {
      return (
        <section className="attendance" aria-labelledby="kallelse">
          <h2 id="kallelse" className="attendance__heading">
            Kallelse
          </h2>
          <p className="attendance__note" role="alert">
            Kallelsen kan inte hämtas just nu.
          </p>
        </section>
      )
    }

    return null
  }

  async function reload(): Promise<void> {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: attendanceStateQueryKey(matchId) }),
      queryClient.invalidateQueries({ queryKey: attendanceSummaryQueryKey(matchId) }),
    ])
  }

  async function call(): Promise<void> {
    setCalling(true)
    setCallFailed(false)

    try {
      await openAttendanceCall(teamSlug, matchId)
      await reload()
    } catch {
      setCallFailed(true)
    } finally {
      setCalling(false)
    }
  }

  async function remind(): Promise<void> {
    setReminding(true)
    setRemindResult(null)

    try {
      const result = await remindNonResponders(teamSlug, matchId)
      setRemindResult(
        result.reminded === 1
          ? 'Påminde 1 förälder.'
          : `Påminde ${String(result.reminded)} föräldrar.`,
      )
      await reload()
    } catch {
      setRemindResult('Påminnelsen gick inte att skicka just nu. Försök igen om en stund.')
    } finally {
      setReminding(false)
    }
  }

  const closed = hasKickedOff(kickoffUtc)

  return (
    <section className="attendance" aria-labelledby="kallelse">
      <h2 id="kallelse" className="attendance__heading">
        Kallelse
      </h2>

      {!data.callOpen && isManager && (
        <div className="actions">
          <button
            type="button"
            className="button"
            disabled={calling}
            onClick={() => {
              void call()
            }}
          >
            {calling ? 'Kallar…' : 'Kalla till matchen'}
          </button>
          {callFailed && (
            <p className="state state--error" role="alert">
              Kallelsen gick inte att öppna just nu. Försök igen om en stund.
            </p>
          )}
        </div>
      )}

      {!data.callOpen && !isManager && (
        <p className="attendance__note">Tränaren har inte kallat till den här matchen än.</p>
      )}

      {data.callOpen && closed && (
        <p className="attendance__note">
          {data.myResponse
            ? `Du svarade: ${summarize(data.myResponse.status, data.myResponse.count)}.`
            : 'Matchen har spelats.'}
        </p>
      )}

      {data.callOpen && !closed && (
        <>
          {data.myResponse && (
            <p className="attendance__current" role="status">
              Ditt svar: <strong>{summarize(data.myResponse.status, data.myResponse.count)}</strong>
              . Du kan ändra det ända fram till avspark.
            </p>
          )}
          <AttendanceResponseForm
            matchId={matchId}
            current={data.myResponse}
            onSubmitted={reload}
          />
        </>
      )}

      {/* Tränarens summering (#58). Bara den som sköter laget, och bara när kallelsen är öppnad. */}
      {data.callOpen && isManager && summary && (
        <div className="attendance__summary">
          <h3 className="attendance__subheading">Svar hittills</h3>
          <p className="attendance__totals">
            <strong>{summary.comingPeople}</strong> kommer
            {summary.maybePeople > 0 ? `, ${String(summary.maybePeople)} kanske` : ''}
            {summary.cantComeFamilies > 0 ? `, ${String(summary.cantComeFamilies)} kan inte` : ''}
          </p>

          {summary.responders.length === 0 ? (
            <p className="attendance__note">Ingen har svarat än.</p>
          ) : (
            <ul className="attendance__responders">
              {summary.responders.map((responder) => (
                <li key={responder.id}>
                  {responder.name ?? 'Namnlöst konto'} —{' '}
                  {summarize(responder.status, responder.count)}
                </li>
              ))}
            </ul>
          )}

          {summary.notAnsweredCount > 0 && (
            <div className="attendance__unanswered">
              <p className="attendance__note">
                {summary.notAnsweredCount} har inte svarat
                {summary.notAnsweredNames.length > 0
                  ? `: ${summary.notAnsweredNames.join(', ')}`
                  : ''}
                .
              </p>
              <button
                type="button"
                className="button button--action"
                disabled={reminding}
                onClick={() => {
                  void remind()
                }}
              >
                {reminding ? 'Påminner…' : 'Påminn dem som inte svarat'}
              </button>
              {remindResult !== null && (
                <p className="attendance__note" role="status">
                  {remindResult}
                </p>
              )}
            </div>
          )}
        </div>
      )}
    </section>
  )
}

function summarize(status: AttendanceStatus, count: number): string {
  switch (status) {
    case 'Coming':
      return `Kommer (${String(count)})`
    case 'CantCome':
      return 'Kan inte'
    case 'Maybe':
      return count > 0 ? `Kanske (${String(count)})` : 'Kanske'
    default:
      return 'Okänt'
  }
}
