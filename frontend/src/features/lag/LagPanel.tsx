import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { ApiError } from '@/lib/api'
import { slugify } from '@/lib/slugify'

import { useCreateLag, useLag } from './useLag'

/**
 * Lagen i en trupp: lista och skapa (`#192`, `#261`).
 *
 * Truppens admin skapar färg-lagen (Gul/Blå/Vit/Svart) och sköter dem. Panelen bor i
 * admin-vyn; superadmin når samma endpoint vid behov (den kortsluts av policyn server-side).
 */
export function LagPanel({ truppId }: { truppId: string }) {
  const lag = useLag(truppId)
  const create = useCreateLag(truppId)
  const [failure, setFailure] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [slugEdited, setSlugEdited] = useState(false)

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<{ name: string; colorHex: string; slug: string }>({
    resolver: zodResolver(
      z.object({
        name: z.string().trim().min(1, 'Fyll i lagets namn.'),
        colorHex: z.string().regex(/^#[0-9a-fA-F]{6}$/, 'Färgen måste vara en hex-kod.'),
        slug: z
          .string()
          .trim()
          .regex(/^[a-z0-9]+(-[a-z0-9]+)*$/, 'Bara små bokstäver a–z, siffror och bindestreck.'),
      }),
    ),
    defaultValues: { name: '', colorHex: '#d9a21b', slug: '' },
  })

  const nameField = register('name')
  const slugField = register('slug')

  useEffect(() => {
    setFocus('name')
  }, [setFocus])

  return (
    <section className="admin-section" aria-labelledby={`lag-rubrik-${truppId}`}>
      <h2 id={`lag-rubrik-${truppId}`}>Lag</h2>

      {lag.isLoading && <p className="state">Hämtar…</p>}
      {lag.isError && (
        <p className="state state--error" role="alert">
          Kunde inte hämta lagen.
        </p>
      )}
      {lag.data && (
        <ul className="admin-list">
          {lag.data.length === 0 && <li className="state">Inga lag än.</li>}
          {lag.data.map((item) => (
            <li key={item.id} className="admin-list__row">
              <span>
                <span
                  className="admin-color"
                  style={{ backgroundColor: item.colorHex }}
                  aria-hidden="true"
                />
                <strong>{item.name}</strong> <code>{item.slug}</code>
              </span>
            </li>
          ))}
        </ul>
      )}

      <form
        className="form"
        noValidate
        onSubmit={(event) => {
          void handleSubmit(async (values) => {
            try {
              const created = values.name.trim()
              await create.mutateAsync({
                name: created,
                colorHex: values.colorHex,
                slug: values.slug.trim(),
              })
              reset()
              setSlugEdited(false)
              setFailure(null)
              setSuccess(`${created} sparades.`)
              setFocus('name')
            } catch (error) {
              setSuccess(null)
              setFailure(
                error instanceof ApiError && error.status === 409
                  ? 'Namnet eller sluggen är redan taget i truppen.'
                  : 'Det gick inte att spara just nu. Försök igen om en stund.',
              )
            }
          })(event)
        }}
      >
        <div className="form__field">
          <label htmlFor={`lag-namn-${truppId}`}>Namn (t.ex. Gul)</label>
          <input
            id={`lag-namn-${truppId}`}
            type="text"
            autoComplete="off"
            {...nameField}
            onChange={(event) => {
              void nameField.onChange(event)
              setSuccess(null)
              if (!slugEdited) {
                setValue('slug', slugify(event.target.value))
              }
            }}
          />
          {errors.name && <p className="form__error">{errors.name.message}</p>}
        </div>

        <div className="form__field">
          <label htmlFor={`lag-farg-${truppId}`}>Lagfärg</label>
          <input id={`lag-farg-${truppId}`} type="color" {...register('colorHex')} />
          {errors.colorHex && <p className="form__error">{errors.colorHex.message}</p>}
        </div>

        <div className="form__field">
          <label htmlFor={`lag-slug-${truppId}`}>Slug (i länkar, ändras inte sedan)</label>
          <input
            id={`lag-slug-${truppId}`}
            type="text"
            autoComplete="off"
            {...slugField}
            onChange={(event) => {
              void slugField.onChange(event)
              setSlugEdited(true)
            }}
          />
          {errors.slug && <p className="form__error">{errors.slug.message}</p>}
        </div>

        {success !== null && (
          <p className="form__success" role="status">
            {success}
          </p>
        )}

        {failure !== null && (
          <p className="state state--error" role="alert">
            {failure}
          </p>
        )}

        <div className="actions">
          <button type="submit" className="button" disabled={isSubmitting}>
            {isSubmitting ? 'Sparar…' : 'Lägg till lag'}
          </button>
        </div>
      </form>
    </section>
  )
}
