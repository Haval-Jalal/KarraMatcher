import { createContext } from 'react'

export const SELECTED_TEAM_STORAGE_KEY = 'karra.valt-lag'

export interface SelectedTeamValue {
  selectedSlug: string | null
  selectTeam: (slug: string) => void
}

/**
 * Valt lag är delad UI-state och bor därför i Context (CLAUDE.md → Frontend, Global
 * state-strategi). Med lokal `useState` i varje komponent hade lagväljaren och headern
 * haft varsin uppfattning om vilket lag som var valt.
 */
export const SelectedTeamContext = createContext<SelectedTeamValue | null>(null)
