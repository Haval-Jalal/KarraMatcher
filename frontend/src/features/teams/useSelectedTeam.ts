import { use } from 'react'

import { SelectedTeamContext } from './selectedTeamContext'

/**
 * Vilket lag som är valt. Kastar utanför providern — ett tyst `null` hade gjort att
 * lagvalet slutade fungera utan att någon förstod varför.
 */
export function useSelectedTeam() {
  const value = use(SelectedTeamContext)

  if (value === null) {
    throw new Error('useSelectedTeam måste användas inuti SelectedTeamProvider.')
  }

  return value
}
