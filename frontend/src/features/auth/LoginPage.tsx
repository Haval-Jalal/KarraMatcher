import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate, useSearch } from '@tanstack/react-router'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { loginWithPasskey, passkeysSupported } from '@/features/passkeys'
import { ApiError } from '@/lib/api'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { getProfile, requestLoginCode, verifyLoginCode, type AccountProfile } from './authApi'
import { NameForm } from './NameForm'
import { useAuth } from './useAuth'

/**
 * Inloggning i två steg: adress, sedan kod.
 *
 * <h3>Vad den ska kännas som</h3>
 *
 * En förälder gör det här en gång per telefon och ska sedan aldrig tänka på det igen.
 * Därför inga lösenord, inga villkor att bocka i, och ingenting att komma ihåg — bara en
 * adress och sex siffror ur ett mejl.
 *
 * <h3>Varför texten inte säger om adressen fanns</h3>
 *
 * "Vi har skickat en kod om adressen finns hos oss" är avsiktligt luddigt. Servern svarar
 * likadant oavsett, och gränssnittet får inte avslöja det servern håller tyst om —
 * annars blir inloggningsrutan en adresslista för den som frågar tillräckligt många
 * gånger.
 */

/**
 * Vart en lyckad inloggning ska leda.
 *
 * <h3>Varför kontrollen ligger här och inte bara i routern</h3>
 *
 * Routens `validateSearch` rensar den typade sökträngen, men den är inte det som avgör
 * vart `navigate` går. Kontrollen hör hemma där omdirigeringen faktiskt sker — annars
 * skyddar den bara så länge ingen läser värdet någon annanstans.
 *
 * En open redirect är som dyrast precis efter en inloggning: den som klickat på länken
 * litar på sidan hen kommer till. Allt som inte är en adress inuti appen kastas därför
 * bort. `//` räknas som utanför — det är en protokollrelativ adress till en annan värd.
 */
function safeDestination(next: unknown): string {
  return typeof next === 'string' && next.startsWith('/') && !next.startsWith('//') ? next : '/'
}

/**
 * Vila innan en ny kod får begäras. Räcker för att en förälder inte ska trycka i frustration
 * och slå i backendens rate-limit på `request-code`, men kort nog att inte kännas som ett straff.
 */
const RESEND_COOLDOWN_SECONDS = 30

const emailSchema = z.object({
  email: z.string().min(1, 'Fyll i din mejladress.').email('Mejladressen ser inte riktig ut.'),
})

const codeSchema = z.object({
  code: z
    .string()
    .min(1, 'Fyll i koden från mejlet.')
    .regex(/^\d{6}$/, 'Koden är sex siffror.'),
})

type EmailForm = z.infer<typeof emailSchema>
type CodeForm = z.infer<typeof codeSchema>

export function LoginPage() {
  const [email, setEmail] = useState<string | null>(null)
  const [failure, setFailure] = useState<string | null>(null)

  /*
   * Namnet fragas bara av den som inte har nagot (`#154`). En atervandande foralder ska
   * mota noll extra steg -- inloggningen ar redan tva ovanpa en lank hen klickade pa.
   */
  const [askName, setAskName] = useState<AccountProfile | null>(null)
  const navigate = useNavigate()
  const search = useSearch({ from: '/logga-in' })
  const { refresh } = useAuth()

  // Passkey-inloggning visas bara där webbläsaren stöder den; e-postkoden finns alltid kvar.
  const passkeySupported = passkeysSupported()

  async function handlePasskeyLogin(): Promise<void> {
    setFailure(null)

    try {
      await loginWithPasskey()
      refresh()
      await navigate({ to: safeDestination(search.next) })
    } catch {
      // Avbruten Face ID, ingen passkey på enheten, eller ett fel — koden är alltid vägen in.
      setFailure('Passkey-inloggningen gick inte. Logga in med en kod via mejl i stället.')
    }
  }

  useDocumentTitle('Logga in')

  return (
    <main>
      <header className="app-header">
        <h1>Logga in</h1>
        <p className="app-header__subtitle">
          Logga in för att se lagets schema, kallelser och samåkning — appen är bara för medlemmar.
          Spelarkortet på din telefon når du utan konto.
        </p>
      </header>

      {email === null ? (
        <>
          {passkeySupported && (
            <div className="actions">
              <button
                type="button"
                className="button"
                onClick={() => {
                  void handlePasskeyLogin()
                }}
              >
                Logga in med passkey
              </button>
            </div>
          )}

          <EmailStep
            onSent={(sent) => {
              setEmail(sent)
              setFailure(null)
            }}
            onFailure={setFailure}
          />
        </>
      ) : askName !== null ? (
        <NameForm
          profile={askName}
          submitLabel="Spara och fortsätt"
          onSaved={() => {
            void navigate({ to: safeDestination(search.next) })
          }}
          onSkip={() => {
            void navigate({ to: safeDestination(search.next) })
          }}
        />
      ) : (
        <CodeStep
          email={email}
          onVerified={(profile) => {
            refresh()

            if (profile !== null && profile.needsName) {
              setAskName(profile)

              return
            }

            void navigate({ to: safeDestination(search.next) })
          }}
          onFailure={setFailure}
          onStartOver={() => {
            setEmail(null)
            setFailure(null)
          }}
        />
      )}

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}
    </main>
  )
}

function EmailStep({
  onSent,
  onFailure,
}: {
  onSent: (email: string) => void
  onFailure: (message: string) => void
}) {
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<EmailForm>({ resolver: zodResolver(emailSchema) })

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        void handleSubmit(async ({ email }) => {
          try {
            await requestLoginCode(email)
            onSent(email)
          } catch (error) {
            onFailure(
              error instanceof ApiError && error.offline
                ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
                : 'Kunde inte skicka koden just nu. Försök igen om en stund.',
            )
          }
        })(event)
      }}
    >
      <div className="form__field">
        <label htmlFor="epost">Mejladress</label>
        <input
          id="epost"
          type="email"
          inputMode="email"
          autoComplete="email"
          aria-describedby={errors.email ? 'epost-fel' : undefined}
          aria-invalid={errors.email ? true : undefined}
          {...register('email')}
        />
        {errors.email && (
          <p className="form__error" id="epost-fel">
            {errors.email.message}
          </p>
        )}
      </div>

      <button type="submit" className="button" disabled={isSubmitting}>
        {isSubmitting ? 'Skickar…' : 'Skicka kod'}
      </button>
    </form>
  )
}

function CodeStep({
  email,
  onVerified,
  onFailure,
  onStartOver,
}: {
  email: string
  onVerified: (profile: AccountProfile | null) => void
  onFailure: (message: string) => void
  onStartOver: () => void
}) {
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<CodeForm>({ resolver: zodResolver(codeSchema) })

  // En kod skickades nyss (steget innan), så nedräkningen börjar direkt: "skicka ny" väntar.
  const [cooldown, setCooldown] = useState(RESEND_COOLDOWN_SECONDS)
  const [resending, setResending] = useState(false)
  const [resent, setResent] = useState(false)

  // En enda intervall räknar ned till noll och stannar där (samma värde → ingen omrendering).
  // Skapas när steget visas, städas när det lämnas.
  useEffect(() => {
    const id = window.setInterval(() => {
      setCooldown((seconds) => (seconds <= 0 ? 0 : seconds - 1))
    }, 1000)

    return () => {
      window.clearInterval(id)
    }
  }, [])

  async function resend(): Promise<void> {
    setResending(true)
    setResent(false)

    try {
      await requestLoginCode(email)
      setResent(true)
      setCooldown(RESEND_COOLDOWN_SECONDS)
    } catch (error) {
      onFailure(
        error instanceof ApiError && error.offline
          ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
          : 'Kunde inte skicka en ny kod just nu. Försök igen om en stund.',
      )
    } finally {
      setResending(false)
    }
  }

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        void handleSubmit(async ({ code }) => {
          try {
            await verifyLoginCode(email, code)

            /*
             * Profilen hamtas har och inte i sessionssvaret. Ett namn i token hade drojt
             * upp till en kvart med att visa en andring, och sessionens kontrakt ska handla
             * om sessionen. Misslyckas hamtningen ar inloggningen anda gjord -- da hoppas
             * namnfragan over i stallet for att stoppa nagon pa vagen in.
             */
            let profile: AccountProfile | null = null

            try {
              profile = await getProfile()
            } catch {
              profile = null
            }

            onVerified(profile)
          } catch (error) {
            onFailure(
              error instanceof ApiError && error.offline
                ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
                : 'Koden stämmer inte, eller har gått ut. Begär en ny om det dröjt en stund.',
            )
          }
        })(event)
      }}
    >
      <p className="state" role="status">
        Om <strong>{email}</strong> finns hos oss har vi skickat en kod dit. Den gäller i tio
        minuter.
      </p>

      <div className="form__field">
        <label htmlFor="kod">Kod från mejlet</label>
        <input
          id="kod"
          type="text"
          inputMode="numeric"
          autoComplete="one-time-code"
          maxLength={6}
          aria-describedby={errors.code ? 'kod-fel' : undefined}
          aria-invalid={errors.code ? true : undefined}
          {...register('code')}
        />
        {errors.code && (
          <p className="form__error" id="kod-fel">
            {errors.code.message}
          </p>
        )}
      </div>

      <div className="actions">
        <button type="submit" className="button" disabled={isSubmitting}>
          {isSubmitting ? 'Kontrollerar…' : 'Logga in'}
        </button>
        <button type="button" className="button" onClick={onStartOver}>
          Byt adress
        </button>
      </div>

      {/*
        Fick koden inte fram, eller gick den ut? Skicka en ny utan att börja om (`aria-live`
        annonserar kvittot utan att bli ett andra `role="status"` vid sidan av rutan ovan).
      */}
      <p className="admin-muted" aria-live="polite">
        {resent
          ? 'En ny kod är på väg till din inkorg.'
          : 'Fick du ingen kod? Kolla skräpposten — eller skicka en ny.'}
      </p>

      <button
        type="button"
        className="button button--action"
        disabled={cooldown > 0 || resending}
        onClick={() => {
          void resend()
        }}
      >
        {resending ? 'Skickar…' : cooldown > 0 ? `Skicka ny kod (${cooldown} s)` : 'Skicka ny kod'}
      </button>
    </form>
  )
}
