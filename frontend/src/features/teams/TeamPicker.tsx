import { Link } from '@tanstack/react-router'

import type { Team } from './types'

interface TeamPickerProps {
  teams: Team[]
  /** Laget som visas just nu, eller null på startsidan innan något valts. */
  currentSlug: string | null
}

/**
 * Lagväljaren.
 *
 * <h3>Länkar, inte knappar</h3>
 *
 * Att välja lag byter adress sedan #19. En kontroll som byter adress ska vara en länk: då
 * går den att öppna i ny flik, kopiera och dela, och skärmläsaren säger "länk" i stället
 * för "växlingsknapp". Aktuellt lag märks med `aria-current="page"`, som betyder just
 * "det här är sidan du är på" — `aria-pressed` hade sagt att knappen är intryckt, vilket
 * är något annat.
 *
 * <h3>Varför färgen inte bär betydelsen ensam</h3>
 *
 * Lagfärgen syns som en rund bricka, aldrig som bakgrund bakom text. Två av de fyra
 * färgerna gör det omöjligt att hålla WCAG-kontrast som textbakgrund: Vit (#D9D9D9) mot
 * ljust läge och Svart (#161616) mot mörkt. Med brickan står texten alltid mot sidans
 * bakgrund, och kontrasten är densamma för alla lag.
 *
 * Ramen runt det aktuella laget använder inte heller lagfärgen — Gul ligger på 2,15:1 och
 * Vit på 1,32:1 mot underlaget, så för hälften av lagen hade markeringen varit osynlig.
 * Tillståndet bärs av ram, bock och `aria-current`; identiteten av brickan.
 */
export function TeamPicker({ teams, currentSlug }: TeamPickerProps) {
  return (
    <nav className="team-picker" aria-label="Välj lag">
      {teams.map((team) => {
        const isCurrent = team.slug === currentSlug

        return (
          <Link
            key={team.slug}
            to="/lag/$slug"
            params={{ slug: team.slug }}
            className="team-picker__option"
            aria-current={isCurrent ? 'page' : undefined}
            style={{ '--team-color': team.colorHex } as React.CSSProperties}
          >
            <span className="team-picker__swatch" aria-hidden="true" />
            <span className="team-picker__name">{team.name}</span>
            <span className="team-picker__check" aria-hidden="true">
              {isCurrent ? '✓' : ''}
            </span>
          </Link>
        )
      })}
    </nav>
  )
}
