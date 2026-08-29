import type { Team } from './types'

interface TeamPickerProps {
  teams: Team[]
  selectedSlug: string | null
  onSelect: (slug: string) => void
}

/**
 * Lagknapparna. Ren presentation — all datahämtning ligger i `TeamPickerSection`.
 *
 * <h3>Varför färgen inte bär betydelsen ensam</h3>
 *
 * Lagfärgen syns som en rund bricka, aldrig som bakgrund bakom text. Två av de fyra
 * färgerna gör det omöjligt att hålla WCAG-kontrast som textbakgrund: Vit (#D9D9D9) mot
 * ljust läge och Svart (#161616) mot mörkt. Med brickan står texten alltid mot sidans
 * bakgrund, och kontrasten är densamma för alla lag.
 *
 * Brickan har en tunn ring, så att både den vita och den svarta syns oavsett om appen
 * körs i ljust eller mörkt läge.
 *
 * Valt lag markeras med tre samverkande signaler och inte bara färg (WCAG 1.4.1):
 * `aria-pressed` för skärmläsare, en bock som syns, och en kraftigare ram.
 */
export function TeamPicker({ teams, selectedSlug, onSelect }: TeamPickerProps) {
  return (
    <div className="team-picker" role="group" aria-label="Välj lag">
      {teams.map((team) => {
        const isSelected = team.slug === selectedSlug

        return (
          <button
            key={team.slug}
            type="button"
            className="team-picker__option"
            aria-pressed={isSelected}
            style={{ '--team-color': team.colorHex } as React.CSSProperties}
            onClick={() => {
              onSelect(team.slug)
            }}
          >
            <span className="team-picker__swatch" aria-hidden="true" />
            <span className="team-picker__name">{team.name}</span>
            <span className="team-picker__check" aria-hidden="true">
              {isSelected ? '✓' : ''}
            </span>
          </button>
        )
      })}
    </div>
  )
}
