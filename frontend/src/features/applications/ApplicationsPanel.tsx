import { useState } from 'react'

import { ApiError } from '@/lib/api'
import { formatFullDate } from '@/lib/time'

import { useApplications, useApproveApplication, useDenyApplication } from './useApplications'

function messageOf(error: unknown): string {
  if (error instanceof ApiError) {
    return error.offline ? 'Ingen anslutning. Försök igen.' : error.message
  }

  return 'Något gick fel. Försök igen om en stund.'
}

/**
 * Ansökningskön för en trupp (§KM.3, `#194`): godkänn eller neka.
 *
 * Återanvänds i både trupp-adminens vy och superadmin-konsolen — samma trupp-id, samma
 * server-grind (<c>AdminOfTrupp</c>). En godkänd ansökan blir förälderns medlemskap.
 */
export function ApplicationsPanel({ truppId }: { truppId: string }) {
  const applications = useApplications(truppId)
  const approve = useApproveApplication(truppId)
  const deny = useDenyApplication(truppId)
  const [failure, setFailure] = useState<string | null>(null)

  const busy = approve.isPending || deny.isPending

  return (
    <div className="admin-subsection">
      <h3>Ansökningar</h3>

      {applications.isLoading && <p className="state">Hämtar…</p>}
      {applications.isError && (
        <p className="state state--error" role="alert">
          Kunde inte hämta ansökningarna.
        </p>
      )}

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}

      {applications.data && (
        <ul className="admin-list">
          {applications.data.length === 0 && <li className="state">Inga väntande ansökningar.</li>}
          {applications.data.map((application) => (
            <li key={application.id} className="admin-list__row">
              <span>
                <strong>{application.applicantName ?? application.applicantEmail}</strong>{' '}
                <code>{application.applicantEmail}</code>{' '}
                <span className="admin-muted">
                  ansökte {formatFullDate(application.createdUtc)}
                </span>
              </span>
              <div className="actions">
                <button
                  type="button"
                  className="button button--small"
                  disabled={busy}
                  onClick={() => {
                    setFailure(null)
                    void approve
                      .mutateAsync(application.id)
                      .catch((error: unknown) => setFailure(messageOf(error)))
                  }}
                >
                  Godkänn
                </button>
                <button
                  type="button"
                  className="button button--small"
                  disabled={busy}
                  onClick={() => {
                    setFailure(null)
                    void deny
                      .mutateAsync(application.id)
                      .catch((error: unknown) => setFailure(messageOf(error)))
                  }}
                >
                  Neka
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
