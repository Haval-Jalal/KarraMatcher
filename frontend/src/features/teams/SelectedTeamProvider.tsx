import { useCallback, useMemo, useState, type ReactNode } from 'react'

import { readSetting, writeSetting } from '@/lib/storage'

import { SELECTED_TEAM_STORAGE_KEY, SelectedTeamContext } from './selectedTeamContext'

/**
 * Håller reda på vilket lag föräldern tittar på, och kommer ihåg det mellan besök.
 *
 * De allra flesta har ett barn i ett lag och ska slippa välja om varje gång. Valet lagras
 * som lagets slug och inte som ett index — ett index hade pekat på fel lag så snart ett
 * lag läggs till eller tas bort.
 *
 * Läsningen sker en gång vid uppstart. Saknas lagring, eller är den avstängd, blir valet
 * bara tillfälligt — appen fungerar ändå.
 */
export function SelectedTeamProvider({ children }: { children: ReactNode }) {
  const [selectedSlug, setSelectedSlug] = useState<string | null>(() =>
    readSetting(SELECTED_TEAM_STORAGE_KEY),
  )

  const selectTeam = useCallback((slug: string) => {
    setSelectedSlug(slug)
    writeSetting(SELECTED_TEAM_STORAGE_KEY, slug)
  }, [])

  const value = useMemo(() => ({ selectedSlug, selectTeam }), [selectedSlug, selectTeam])

  return <SelectedTeamContext value={value}>{children}</SelectedTeamContext>
}
