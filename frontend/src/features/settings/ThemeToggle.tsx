import { useState } from 'react'

import { getStoredTheme, setTheme, type ThemePreference } from '@/lib/theme'

/**
 * Tema-väljaren i Inställningar (`#redesign`): System / Ljust / Mörkt.
 *
 * En segmenterad kontroll (samma som händelseformulärets Typ) med riktiga radioknappar — den
 * fungerar med tangentbord och skärmläsare, och valet bärs av markering, inte av färg ensam.
 * Valet sparas per enhet; System betyder "följ telefonens läge".
 */

const OPTIONS: { value: ThemePreference; label: string }[] = [
  { value: 'system', label: 'System' },
  { value: 'light', label: 'Ljust' },
  { value: 'dark', label: 'Mörkt' },
]

export function ThemeToggle() {
  const [theme, setThemeState] = useState<ThemePreference>(() => getStoredTheme())

  return (
    <fieldset className="form__field">
      <legend>Tema</legend>
      <div className="seg">
        {OPTIONS.map((option) => (
          <label key={option.value} className="seg__option">
            <input
              type="radio"
              name="tema"
              className="visually-hidden"
              checked={theme === option.value}
              onChange={() => {
                setTheme(option.value)
                setThemeState(option.value)
              }}
            />
            <span>{option.label}</span>
          </label>
        ))}
      </div>
    </fieldset>
  )
}
