import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { superadminError } from './superadminError'
import { useAdmins, useGrantAdmin, useRevokeAdmin } from './useSuperadmin'

const schema = z.object({ email: z.string().trim().email('Adressen ser inte giltig ut.') })

/**
 * Tilldela admins till en trupp (`#192`, `#261`) — sista steget i uppsättningsguiden.
 *
 * Adminen sköter sedan lag, barn, tränare och inbjudningar i truppen från sin egen vy.
 */
export function AdminStep({ truppId }: { truppId: string }) {
  const admins = useAdmins(truppId)
  const grant = useGrantAdmin(truppId)
  const revoke = useRevokeAdmin(truppId)
  const [failure, setFailure] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    reset,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<{ email: string }>({ resolver: zodResolver(schema), defaultValues: { email: '' } })

  const emailField = register('email')

  useEffect(() => {
    setFocus('email')
  }, [setFocus])

  return (
    <div className="admin-subsection">
      <h4>Admins</h4>

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
                  setSuccess(null)
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
              const email = values.email.trim()
              await grant.mutateAsync(email)
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
            {isSubmitting ? 'Sparar…' : 'Tilldela admin'}
          </button>
        </div>
      </form>
    </div>
  )
}
