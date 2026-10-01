import type { Child, Roster } from '@/features/children'

import type { KallelseTarget, TargetMode } from './target'

/**
 * Väljaren för kallelse-målgruppen (§KM.7, `#333`): hela truppen, valda lag, eller namngivna barn.
 *
 * Kontrollerad — läget och urvalet ägs av föräldern (skapa-formuläret), så förvalen kan bytas när
 * aktivitetstypen ändras. Barnen visas med förnamn + initial (§KM.1), aldrig hela efternamnet.
 */

const MODE_LABEL: Record<TargetMode, string> = {
  trupp: 'Hela truppen',
  lag: 'Valda lag',
  children: 'Namngivna barn',
}

const UNASSIGNED = '__unassigned__'

export function TargetGroupPicker({
  roster,
  value,
  onChange,
}: {
  roster: Roster
  value: KallelseTarget
  onChange: (next: KallelseTarget) => void
}) {
  function setMode(mode: TargetMode): void {
    onChange({ ...value, mode })
  }

  function toggleTeam(teamId: string): void {
    const has = value.teamIds.includes(teamId)
    onChange({
      ...value,
      teamIds: has ? value.teamIds.filter((id) => id !== teamId) : [...value.teamIds, teamId],
    })
  }

  function toggleChild(childId: string): void {
    const has = value.childIds.includes(childId)
    onChange({
      ...value,
      childIds: has ? value.childIds.filter((id) => id !== childId) : [...value.childIds, childId],
    })
  }

  // Barnen grupperade per lag, plus en grupp för de otilldelade — samma indelning som kallelsen
  // på händelsesidan, så admin känner igen sig.
  const groups: { key: string; name: string; children: Child[] }[] = [
    ...roster.teams.map((team) => ({
      key: team.id,
      name: team.name,
      children: roster.children.filter((child) => child.teamId === team.id),
    })),
    {
      key: UNASSIGNED,
      name: 'Otilldelade',
      children: roster.children.filter((child) => child.teamId === null),
    },
  ].filter((group) => group.children.length > 0)

  return (
    <fieldset className="form__field">
      <legend>Kallelse-målgrupp</legend>

      {(['trupp', 'lag', 'children'] as const).map((mode) => (
        <label key={mode} className="opt">
          <input
            type="radio"
            name="kallelse-malgrupp"
            checked={value.mode === mode}
            onChange={() => setMode(mode)}
          />
          <span className="opt__txt">{MODE_LABEL[mode]}</span>
        </label>
      ))}

      {value.mode === 'lag' && (
        <div className="form__field">
          {roster.teams.length === 0 ? (
            <p className="admin-muted">Truppen har inga lag ännu.</p>
          ) : (
            <div className="pills">
              {roster.teams.map((team) => {
                const picked = value.teamIds.includes(team.id)

                return (
                  <label key={team.id} className="tpill">
                    <input
                      type="checkbox"
                      className="visually-hidden"
                      checked={picked}
                      onChange={() => toggleTeam(team.id)}
                    />
                    {/* Bock = icke-färg-signal för valt lag (WCAG 1.4.1, `#483`); färgen ensam
                        räckte inte för lågseende/färgblinda. Platsen reserveras alltid. */}
                    <span className="tpill__check" aria-hidden="true">
                      {picked ? '✓' : ''}
                    </span>
                    <span
                      className="tpill__dot"
                      style={{ background: team.colorHex }}
                      aria-hidden="true"
                    />
                    {team.name}
                  </label>
                )
              })}
            </div>
          )}
        </div>
      )}

      {value.mode === 'children' && (
        <div className="form__field">
          {groups.length === 0 ? (
            <p className="admin-muted">Truppen har inga barn ännu.</p>
          ) : (
            groups.map((group) => (
              <fieldset key={group.key} className="form__field">
                <legend>{group.name}</legend>
                {group.children.map((child) => (
                  <label key={child.id} className="form__checkbox">
                    <input
                      type="checkbox"
                      checked={value.childIds.includes(child.id)}
                      onChange={() => toggleChild(child.id)}
                    />{' '}
                    {child.displayName}
                  </label>
                ))}
              </fieldset>
            ))
          )}
        </div>
      )}
    </fieldset>
  )
}
