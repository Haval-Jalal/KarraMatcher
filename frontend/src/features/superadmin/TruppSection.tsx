import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import type { Club, Sport, Trupp } from './superadminApi'
import { superadminError } from './superadminError'
import { useCreateTrupp, useTrupper, useUpdateTrupp } from './useSuperadmin'

const schema = z.object({
  clubId: z.string().min(1, 'Välj en klubb.'),
  sportId: z.string().min(1, 'Välj en sport.'),
  name: z.string().trim().min(1, 'Fyll i truppens namn.'),
  season: z.string().trim().min(1, 'Fyll i säsongen.'),
})

type FormValues = z.infer<typeof schema>

/**
 * Trupperna: lista, skapa (under en klubb och en sport) och ändra (§KM.3, `#192`).
 * Klubbarna och sporterna kommer som props — sidan har redan hämtat dem.
 */
export function TruppSection({ clubs, sports }: { clubs: Club[]; sports: Sport[] }) {
  const trupper = useTrupper()
  const create = useCreateTrupp()
  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { clubId: '', sportId: '', name: '', season: '' },
  })

  const canAdd = clubs.length > 0 && sports.length > 0

  return (
    <section className="admin-section" aria-labelledby="trupp-rubrik">
      <h2 id="trupp-rubrik">Trupper</h2>

      {trupper.isLoading && <p className="state">Hämtar…</p>}
      {trupper.isError && (
        <p className="state state--error" role="alert">
          Kunde inte hämta trupperna.
        </p>
      )}

      {trupper.data && (
        <ul className="admin-list">
          {trupper.data.length === 0 && <li className="state">Inga trupper än.</li>}
          {trupper.data.map((trupp) => (
            <TruppRow key={trupp.id} trupp={trupp} sports={sports} />
          ))}
        </ul>
      )}

      {!canAdd ? (
        <p className="state">Skapa minst en klubb och en sport först.</p>
      ) : (
        <form
          className="form"
          noValidate
          onSubmit={(event) => {
            void handleSubmit(async (values) => {
              try {
                await create.mutateAsync({
                  clubId: values.clubId,
                  sportId: values.sportId,
                  name: values.name.trim(),
                  season: values.season.trim(),
                })
                setFailure(null)
                reset()
              } catch (error) {
                setFailure(superadminError(error))
              }
            })(event)
          }}
        >
          <div className="form__field">
            <label htmlFor="trupp-klubb">Klubb</label>
            <select
              id="trupp-klubb"
              aria-invalid={errors.clubId ? true : undefined}
              {...register('clubId')}
            >
              <option value="">Välj klubb…</option>
              {clubs.map((club) => (
                <option key={club.id} value={club.id}>
                  {club.name}
                </option>
              ))}
            </select>
            {errors.clubId && <p className="form__error">{errors.clubId.message}</p>}
          </div>

          <div className="form__field">
            <label htmlFor="trupp-sport">Sport</label>
            <select
              id="trupp-sport"
              aria-invalid={errors.sportId ? true : undefined}
              {...register('sportId')}
            >
              <option value="">Välj sport…</option>
              {sports.map((sport) => (
                <option key={sport.id} value={sport.id}>
                  {sport.name}
                </option>
              ))}
            </select>
            {errors.sportId && <p className="form__error">{errors.sportId.message}</p>}
          </div>

          <div className="form__field">
            <label htmlFor="trupp-namn">Namn (t.ex. P2016)</label>
            <input id="trupp-namn" type="text" autoComplete="off" {...register('name')} />
            {errors.name && <p className="form__error">{errors.name.message}</p>}
          </div>

          <div className="form__field">
            <label htmlFor="trupp-sasong">Säsong (t.ex. 2026)</label>
            <input id="trupp-sasong" type="text" autoComplete="off" {...register('season')} />
            {errors.season && <p className="form__error">{errors.season.message}</p>}
          </div>

          {failure !== null && (
            <p className="state state--error" role="alert">
              {failure}
            </p>
          )}

          <div className="actions">
            <button type="submit" className="button" disabled={isSubmitting}>
              {isSubmitting ? 'Lägger till…' : 'Lägg till trupp'}
            </button>
          </div>
        </form>
      )}
    </section>
  )
}

function TruppRow({ trupp, sports }: { trupp: Trupp; sports: Sport[] }) {
  const update = useUpdateTrupp()
  const [editing, setEditing] = useState(false)
  const [sportId, setSportId] = useState(trupp.sportId)
  const [name, setName] = useState(trupp.name)
  const [season, setSeason] = useState(trupp.season)
  const [failure, setFailure] = useState<string | null>(null)

  if (!editing) {
    return (
      <li className="admin-list__row">
        <span>
          <strong>{trupp.name}</strong> {trupp.season} — {trupp.clubName} · {trupp.sportName}
        </span>
        <button type="button" className="button button--small" onClick={() => setEditing(true)}>
          Ändra
        </button>
      </li>
    )
  }

  return (
    <li className="admin-list__row">
      <select
        aria-label="Sport"
        value={sportId}
        onChange={(event) => setSportId(event.target.value)}
      >
        {sports.map((sport) => (
          <option key={sport.id} value={sport.id}>
            {sport.name}
          </option>
        ))}
      </select>
      <input aria-label="Namn" type="text" value={name} onChange={(e) => setName(e.target.value)} />
      <input
        aria-label="Säsong"
        type="text"
        value={season}
        onChange={(e) => setSeason(e.target.value)}
      />
      <div className="actions">
        <button
          type="button"
          className="button button--small"
          disabled={update.isPending || name.trim() === '' || season.trim() === ''}
          onClick={() => {
            void update
              .mutateAsync({ id: trupp.id, sportId, name: name.trim(), season: season.trim() })
              .then(() => {
                setEditing(false)
                setFailure(null)
              })
              .catch((error: unknown) => setFailure(superadminError(error)))
          }}
        >
          {update.isPending ? 'Sparar…' : 'Spara'}
        </button>
        <button
          type="button"
          className="button button--small"
          onClick={() => {
            setSportId(trupp.sportId)
            setName(trupp.name)
            setSeason(trupp.season)
            setEditing(false)
            setFailure(null)
          }}
        >
          Avbryt
        </button>
      </div>
      {failure !== null && (
        <p className="form__error" role="alert">
          {failure}
        </p>
      )}
    </li>
  )
}
