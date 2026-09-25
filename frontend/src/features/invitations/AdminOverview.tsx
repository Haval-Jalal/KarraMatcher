import { useApplications } from '@/features/applications'
import { useRoster } from '@/features/children'

import { useInvitations } from './useInvitations'

/**
 * Trupp-adminens översikt (`#279`): det första man ser efter att en trupp valts.
 *
 * Några lugna nyckeltal så det som behöver uppmärksamhet syns direkt — i stället för fem
 * paneler på en gång. Siffrorna läses ur samma hooks som sektionerna använder (delad cache),
 * och en "Att göra"-rad tar en rakt till Ansökningar när föräldrar väntar på svar.
 */
export function AdminOverview({ truppId, onGotoApplications }: Props) {
  const roster = useRoster(truppId)
  const applications = useApplications(truppId)
  const invitations = useInvitations(truppId)

  const children = roster.data?.children?.length ?? 0
  const teams = roster.data?.teams?.length ?? 0
  const pending = applications.data?.length ?? 0
  const invites = invitations.data?.length ?? 0

  const stats: Stat[] = [
    { key: 'barn', value: children, label: 'barn' },
    { key: 'lag', value: teams, label: teams === 1 ? 'lag' : 'lag' },
    {
      key: 'ansokningar',
      value: pending,
      label: pending === 1 ? 'väntande ansökan' : 'väntande ansökningar',
      attention: pending > 0,
    },
    {
      key: 'inbjudningar',
      value: invites,
      label: invites === 1 ? 'öppen inbjudan' : 'öppna inbjudningar',
    },
  ]

  return (
    <div className="admin-overview">
      <ul className="stat-cards">
        {stats.map((stat) => (
          <li
            key={stat.key}
            className={stat.attention ? 'stat-card stat-card--attention' : 'stat-card'}
          >
            <span className="stat-card__value">{stat.value}</span>
            <span className="stat-card__label">{stat.label}</span>
          </li>
        ))}
      </ul>

      {pending > 0 && (
        <button type="button" className="admin-todo" onClick={onGotoApplications}>
          <span className="admin-todo__dot" aria-hidden="true" />
          {pending === 1
            ? 'En förälder väntar på svar på sin ansökan'
            : `${String(pending)} föräldrar väntar på svar på sin ansökan`}
        </button>
      )}
    </div>
  )
}

interface Props {
  truppId: string
  /** Tar admin till Ansökningar-sektionen (Att göra-raden). */
  onGotoApplications: () => void
}

interface Stat {
  key: string
  value: number
  label: string
  attention?: boolean
}
