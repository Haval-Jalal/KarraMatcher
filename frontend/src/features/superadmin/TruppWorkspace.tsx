import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { ApplicationsPanel } from '@/features/applications'
import { ChildrenPanel } from '@/features/children'
import { CoachesPanel } from '@/features/coaches'
import { InvitationsPanel } from '@/features/invitations'

import { slugify } from './slugify'
import type { Trupp } from './superadminApi'
import { superadminError } from './superadminError'
import { useAdmins, useCreateLag, useGrantAdmin, useLag, useRevokeAdmin } from './useSuperadmin'

/**
 * Arbetsyta för en vald trupp: dess lag och dess admins (§KM.3, `#192`).
 *
 * Lag och admins hör till en trupp, så de bor bakom ett trupp-val i stället för som egna
 * toppnivålistor — det speglar hierarkin och håller vyn läsbar när trupperna blir fler.
 */
export function TruppWorkspace({ trupper }: { trupper: Trupp[] }) {
  const [truppId, setTruppId] = useState<string | null>(null)

  return (
    <section className="admin-section" aria-labelledby="arbetsyta-rubrik">
      <h2 id="arbetsyta-rubrik">Lag och admins</h2>

      {trupper.length === 0 ? (
        <p className="state">Skapa en trupp först.</p>
      ) : (
        <div className="form__field">
          <label htmlFor="valj-trupp">Välj trupp</label>
          <select
            id="valj-trupp"
            value={truppId ?? ''}
            onChange={(event) => setTruppId(event.target.value === '' ? null : event.target.value)}
          >
            <option value="">Välj trupp…</option>
            {trupper.map((trupp) => (
              <option key={trupp.id} value={trupp.id}>
                {trupp.clubName} · {trupp.name} {trupp.season}
              </option>
            ))}
          </select>
        </div>
      )}

      {truppId !== null && (
        <>
          <LagPanel truppId={truppId} />
          <AdminPanel truppId={truppId} />
          <InvitationsPanel truppId={truppId} />
          <ApplicationsPanel truppId={truppId} />
          <CoachesPanel truppId={truppId} />
          <ChildrenPanel truppId={truppId} />
        </>
      )}
    </section>
  )
}

const lagSchema = z.object({
  name: z.string().trim().min(1, 'Fyll i lagets namn.'),
  colorHex: z.string().regex(/^#[0-9a-fA-F]{6}$/, 'Färgen måste vara en hex-kod.'),
  slug: z
    .string()
    .trim()
    .regex(/^[a-z0-9]+(-[a-z0-9]+)*$/, 'Bara små bokstäver a–z, siffror och bindestreck.'),
})

type LagValues = z.infer<typeof lagSchema>

function LagPanel({ truppId }: { truppId: string }) {
  const lag = useLag(truppId)
  const create = useCreateLag(truppId)
  const [failure, setFailure] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)
  const [slugEdited, setSlugEdited] = useState(false)

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<LagValues>({
    resolver: zodResolver(lagSchema),
    defaultValues: { name: '', colorHex: '#d9a21b', slug: '' },
  })

  const nameField = register('name')
  const slugField = register('slug')

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
    <div className="admin-subsection">
      <h3>Lag</h3>

      {lag.isLoading && <p className="state">Hämtar…</p>}
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

      {!adding ? (
        <div className="actions">
          <button
            type="button"
            className="button"
            onClick={() => {
              setAdding(true)
            }}
          >
            Lägg till lag
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
                await create.mutateAsync({
                  name: created,
                  colorHex: values.colorHex,
                  slug: values.slug.trim(),
                })
                // Kvar i formuläret för fler lag: fälten töms, kvittot syns, fokus åter.
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
            <label htmlFor="lag-namn">Namn (t.ex. Gul)</label>
            <input
              id="lag-namn"
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
            <label htmlFor="lag-farg">Lagfärg</label>
            <input id="lag-farg" type="color" {...register('colorHex')} />
            {errors.colorHex && <p className="form__error">{errors.colorHex.message}</p>}
          </div>

          <div className="form__field">
            <label htmlFor="lag-slug">Slug (i länkar, ändras inte sedan)</label>
            <input
              id="lag-slug"
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
              {isSubmitting ? 'Sparar…' : 'Spara'}
            </button>
            <button type="button" className="button" onClick={closeForm}>
              Stäng
            </button>
          </div>
        </form>
      )}
    </div>
  )
}

const adminSchema = z.object({
  email: z.string().trim().email('Adressen ser inte giltig ut.'),
})

type AdminValues = z.infer<typeof adminSchema>

function AdminPanel({ truppId }: { truppId: string }) {
  const admins = useAdmins(truppId)
  const grant = useGrantAdmin(truppId)
  const revoke = useRevokeAdmin(truppId)
  const [failure, setFailure] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)

  const {
    register,
    handleSubmit,
    reset,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<AdminValues>({ resolver: zodResolver(adminSchema), defaultValues: { email: '' } })

  const emailField = register('email')

  useEffect(() => {
    if (adding) {
      setFocus('email')
    }
  }, [adding, setFocus])

  function closeForm(): void {
    setAdding(false)
    setFailure(null)
    setSuccess(null)
    reset()
  }

  return (
    <div className="admin-subsection">
      <h3>Admins</h3>

      {admins.isLoading && <p className="state">Hämtar…</p>}
      {admins.data && (
        <ul className="admin-list">
          {admins.data.length === 0 && <li className="state">Inga admins än.</li>}
          {admins.data.map((admin) => (
            <li key={admin.accountId} className="admin-list__row">
              <span>
                <strong>{admin.displayName ?? admin.email}</strong> <code>{admin.email}</code>
              </span>
              <button
                type="button"
                className="button button--small"
                disabled={revoke.isPending}
                onClick={() => {
                  setFailure(null)
                  void revoke
                    .mutateAsync(admin.accountId)
                    .catch((error: unknown) => setFailure(superadminError(error)))
                }}
              >
                Ta bort
              </button>
            </li>
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
            Tilldela admin
          </button>
        </div>
      ) : (
        <form
          className="form"
          noValidate
          onSubmit={(event) => {
            void handleSubmit(async (values) => {
              try {
                const email = values.email.trim()
                await grant.mutateAsync(email)
                // Kvar i formuläret för fler admins: fältet töms, kvittot syns, fokus åter.
                reset()
                setFailure(null)
                setSuccess(`${email} tillagd som admin.`)
                setFocus('email')
              } catch (error) {
                setSuccess(null)
                setFailure(superadminError(error))
              }
            })(event)
          }}
        >
          <div className="form__field">
            <label htmlFor="admin-epost">Adress till ett befintligt konto</label>
            <input
              id="admin-epost"
              type="email"
              autoComplete="off"
              {...emailField}
              onChange={(event) => {
                void emailField.onChange(event)
                setSuccess(null)
              }}
            />
            {errors.email && <p className="form__error">{errors.email.message}</p>}
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
    </div>
  )
}
