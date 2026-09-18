import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { ApplicationsPanel } from '@/features/applications'
import { ChildrenPanel } from '@/features/children'
import { InvitationsPanel } from '@/features/invitations'

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

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<LagValues>({
    resolver: zodResolver(lagSchema),
    defaultValues: { name: '', colorHex: '#d9a21b', slug: '' },
  })

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

      <form
        className="form"
        noValidate
        onSubmit={(event) => {
          void handleSubmit(async (values) => {
            try {
              await create.mutateAsync({
                name: values.name.trim(),
                colorHex: values.colorHex,
                slug: values.slug.trim(),
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
          <label htmlFor="lag-namn">Namn (t.ex. Gul)</label>
          <input id="lag-namn" type="text" autoComplete="off" {...register('name')} />
          {errors.name && <p className="form__error">{errors.name.message}</p>}
        </div>

        <div className="form__field">
          <label htmlFor="lag-farg">Lagfärg</label>
          <input id="lag-farg" type="color" {...register('colorHex')} />
          {errors.colorHex && <p className="form__error">{errors.colorHex.message}</p>}
        </div>

        <div className="form__field">
          <label htmlFor="lag-slug">Slug (i länkar, ändras inte sedan)</label>
          <input id="lag-slug" type="text" autoComplete="off" {...register('slug')} />
          {errors.slug && <p className="form__error">{errors.slug.message}</p>}
        </div>

        {failure !== null && (
          <p className="state state--error" role="alert">
            {failure}
          </p>
        )}

        <div className="actions">
          <button type="submit" className="button" disabled={isSubmitting}>
            {isSubmitting ? 'Lägger till…' : 'Lägg till lag'}
          </button>
        </div>
      </form>
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

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<AdminValues>({ resolver: zodResolver(adminSchema), defaultValues: { email: '' } })

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

      <form
        className="form"
        noValidate
        onSubmit={(event) => {
          void handleSubmit(async (values) => {
            try {
              await grant.mutateAsync(values.email.trim())
              setFailure(null)
              reset()
            } catch (error) {
              setFailure(superadminError(error))
            }
          })(event)
        }}
      >
        <div className="form__field">
          <label htmlFor="admin-epost">Adress till ett befintligt konto</label>
          <input id="admin-epost" type="email" autoComplete="off" {...register('email')} />
          {errors.email && <p className="form__error">{errors.email.message}</p>}
        </div>

        {failure !== null && (
          <p className="state state--error" role="alert">
            {failure}
          </p>
        )}

        <div className="actions">
          <button type="submit" className="button" disabled={isSubmitting}>
            {isSubmitting ? 'Tilldelar…' : 'Tilldela admin'}
          </button>
        </div>
      </form>
    </div>
  )
}
