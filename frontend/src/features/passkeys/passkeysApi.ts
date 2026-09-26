import type {
  CredentialCreationOptionsJSON,
  CredentialRequestOptionsJSON,
  PublicKeyCredentialWithAssertionJSON,
  PublicKeyCredentialWithAttestationJSON,
} from '@github/webauthn-json'

import { getAuthJson, postJson } from '@/lib/api'
import { setAccessToken } from '@/lib/session'

/** WebAuthn-options i den JSON-form webbläsaren och Fido2NetLib delar (base64url). */
export type PasskeyCreationOptions = NonNullable<CredentialCreationOptionsJSON['publicKey']>
export type PasskeyRequestOptions = NonNullable<CredentialRequestOptionsJSON['publicKey']>

/** En passkey så som Mitt konto listar den. Speglar `PasskeyDto` i backend. */
export interface PasskeySummary {
  id: string
  deviceLabel: string | null
  createdUtc: string
  lastUsedUtc: string | null
}

/** Startar registrering — svaret är WebAuthn-options att skicka till webbläsaren. */
export const beginPasskeyRegistration = (): Promise<PasskeyCreationOptions> =>
  postJson<PasskeyCreationOptions>('/api/v1/passkeys/registrera/start')

/** Slutför registreringen med enhetens svar. */
export const completePasskeyRegistration = (
  attestation: PublicKeyCredentialWithAttestationJSON,
  deviceLabel: string,
): Promise<void> => postJson<void>('/api/v1/passkeys/registrera/klar', { attestation, deviceLabel })

/** Startar inloggning — svaret bär utmaningens id och options (som JSON-sträng). */
export const beginPasskeyLogin = (): Promise<{ challengeId: string; optionsJson: string }> =>
  postJson<{ challengeId: string; optionsJson: string }>('/api/v1/passkeys/logga-in/start')

/** Slutför inloggningen och lägger access-token i minnet (refresh-cookien sätter servern). */
export async function completePasskeyLogin(
  challengeId: string,
  assertion: PublicKeyCredentialWithAssertionJSON,
): Promise<void> {
  const session = await postJson<{ accessToken: string }>('/api/v1/passkeys/logga-in/klar', {
    challengeId,
    assertion,
  })

  setAccessToken(session.accessToken)
}

export const listPasskeys = (signal?: AbortSignal): Promise<PasskeySummary[]> =>
  getAuthJson<PasskeySummary[]>('/api/v1/passkeys', signal)

export const removePasskey = (id: string): Promise<void> =>
  postJson<void>(`/api/v1/passkeys/${encodeURIComponent(id)}`, undefined, { method: 'DELETE' })
