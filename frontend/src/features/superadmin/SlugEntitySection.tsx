import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { slugify } from './slugify'
import { superadminError } from './superadminError'

/** En post med namn och stabil slug — en sport eller en klubb (`#192`). */
export interface SlugEntity {
  id: string
  name: string
  slug: string
}

const schema = z.object({
  name: z.string().trim().min(1, 'Fyll i namnet.'),
  slug: z
    .string()
    .trim()
    .regex(/^[a-z0-9]+(-[a-z0-9]+)*$/, 'Bara små bokstäver a–z, siffror och bindestreck.'),
})

type FormValues = z.infer<typeof schema>

/**
 * Lista + skapa + byt namn för en sport eller en klubb — samma form (namn + stabil slug),
 * så en komponent räcker för båda (§KM.3, `#192`).
 */
export function SlugEntitySection({
  title,
  singular,
  items,
  isLoading,
  isError,
  onCreate,
  onRename,
}: {
  title: string
  singular: string
  items: SlugEntity[]
  isLoading: boolean
  isError: boolean
  onCreate: (name: string, slug: string) => Promise<unknown>
  onRename: (id: string, name: string) => Promise<unknown>
}) {
  const [failure, setFailure] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)
  // Slug föreslås ur namnet tills man ändrar den för hand — då slutar vi skriva över den.
  const [slugEdited, setSlugEdited] = useState(false)

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { name: '', slug: '' } })

  const nameField = register('name')
  const slugField = register('slug')

  // Fokus i namnfältet när formuläret öppnas, så man kan börja skriva direkt.
  useEffect(() => {
    if (adding) {
      setFocus('name')
    }
  }, [adding, setFocus])

  function closeForm(): void {
    setAdding(false)
    setFailure(null)
    setSuccess(null)
    setSlugEdited(false)
    reset()
  }

  return (
    <section className="admin-section" aria-labelledby={`${singular}-rubrik`}>
      <h2 id={`${singular}-rubrik`}>{title}</h2>

      {isLoading && <p className="state">Hämtar…</p>}
      {isError && (
        <p className="state state--error" role="alert">
          Kunde inte hämta {title.toLowerCase()}.
        </p>
      )}

      {!isLoading && !isError && (
        <ul className="admin-list">
          {items.length === 0 && <li className="state">Inget här än.</li>}
          {items.map((item) => (
            <EntityRow key={item.id} item={item} singular={singular} onRename={onRename} />
          ))}
        </ul>
      )}

      {!adding ? (
        <div className="actions">
          <button
            type="button"
            className="button"
            onClick={() => {
              setAdding(true)
            }}
          >
            {`Lägg till ${singular}`}
          </button>
        </div>
      ) : (
        <form
          className="form"
          noValidate
          onSubmit={(event) => {
            void handleSubmit(async (values) => {
              try {
                const created = values.name.trim()
                await onCreate(created, values.slug.trim())
                // Kvar i formuläret för att lägga till fler: fälten töms, kvittot syns, och
                // markören står redo i namnfältet igen.
                reset()
                setSlugEdited(false)
                setFailure(null)
                setSuccess(`${created} sparades.`)
                setFocus('name')
              } catch (error) {
                setSuccess(null)
                setFailure(superadminError(error))
              }
            })(event)
          }}
        >
          <div className="form__field">
            <label htmlFor={`${singular}-namn`}>Namn</label>
            <input
              id={`${singular}-namn`}
              type="text"
              autoComplete="off"
              aria-invalid={errors.name ? true : undefined}
              {...nameField}
              onChange={(event) => {
                void nameField.onChange(event)
                setSuccess(null)
                // Föreslå slug ur namnet tills den ändrats för hand.
                if (!slugEdited) {
                  setValue('slug', slugify(event.target.value))
                }
              }}
            />
            {errors.name && <p className="form__error">{errors.name.message}</p>}
          </div>

          <div className="form__field">
            <label htmlFor={`${singular}-slug`}>Slug (i länkar, ändras inte sedan)</label>
            <input
              id={`${singular}-slug`}
              type="text"
              autoComplete="off"
              aria-invalid={errors.slug ? true : undefined}
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
              {isSubmitting ? 'Sparar…' : 'Spara'}
            </button>
            <button type="button" className="button" onClick={closeForm}>
              Stäng
            </button>
          </div>
        </form>
      )}
    </section>
  )
}

function EntityRow({
  item,
  singular,
  onRename,
}: {
  item: SlugEntity
  singular: string
  onRename: (id: string, name: string) => Promise<unknown>
}) {
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(item.name)
  const [failure, setFailure] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  if (!editing) {
    return (
      <li className="admin-list__row">
        <span>
          <strong>{item.name}</strong> <code>{item.slug}</code>
        </span>
        <button type="button" className="button button--small" onClick={() => setEditing(true)}>
          Byt namn
        </button>
      </li>
    )
  }

  return (
    <li className="admin-list__row">
      <label htmlFor={`byt-${item.id}`} className="visually-hidden">
        Nytt namn för {singular}
      </label>
      <input
        id={`byt-${item.id}`}
        type="text"
        value={name}
        onChange={(event) => setName(event.target.value)}
      />
      <div className="actions">
        <button
          type="button"
          className="button button--small"
          disabled={saving || name.trim() === ''}
          onClick={() => {
            setSaving(true)
            void onRename(item.id, name.trim())
              .then(() => {
                setEditing(false)
                setFailure(null)
              })
              .catch((error: unknown) => setFailure(superadminError(error)))
              .finally(() => setSaving(false))
          }}
        >
          {saving ? 'Sparar…' : 'Spara'}
        </button>
        <button
          type="button"
          className="button button--small"
          onClick={() => {
            setName(item.name)
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
