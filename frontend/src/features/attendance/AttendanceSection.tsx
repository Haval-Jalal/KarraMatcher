import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { ApiError } from '@/lib/api'
import { hasKickedOff } from '@/lib/time'

import { openAttendanceCall, type AttendanceStatus } from './attendanceApi'
import { AttendanceResponseForm } from './AttendanceResponseForm'
import { attendanceStateQueryKey, useAttendanceState } from './useAttendance'

/**
 * Kallelsen på matchsidan (`#57`, §KM.7).
 *
 * <h3>Osynlig tills klubben slår på den</h3>
 *
 * Kallelsen levereras avstängd. Servern svarar `404` för ett lag där den är av, och då
 * renderar den här sektionen ingenting alls — inte en tom rubrik, inte en inaktiv knapp.
 * Funktionen finns helt enkelt inte förrän en administratör slår på den (§KM.7).
 *
 * <h3>Tränaren kallar, den vuxna svarar</h3>
 *
 * Är kallelsen på men inte öppnad för matchen ser en tränare knappen att kalla; en förälder
 * ser ingenting än. När den är öppnad ser alla svarsformuläret — även tränaren, som också
 * kan ha en familj som ska med.
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

  const isSignedIn = status === 'inloggad'
  const isManager = canManage(teamSlug)

  const { data, isPending, error } = useAttendanceState(matchId, isSignedIn)

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
    await queryClient.invalidateQueries({ queryKey: attendanceStateQueryKey(matchId) })
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
            ? `Du svarade: ${summary(data.myResponse.status, data.myResponse.count)}.`
            : 'Matchen har spelats.'}
        </p>
      )}

      {data.callOpen && !closed && (
        <>
          {data.myResponse && (
            <p className="attendance__current" role="status">
              Ditt svar: <strong>{summary(data.myResponse.status, data.myResponse.count)}</strong>.
              Du kan ändra det ända fram till avspark.
            </p>
          )}
          <AttendanceResponseForm
            matchId={matchId}
            current={data.myResponse}
            onSubmitted={reload}
          />
        </>
      )}
    </section>
  )
}

function summary(status: AttendanceStatus, count: number): string {
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
