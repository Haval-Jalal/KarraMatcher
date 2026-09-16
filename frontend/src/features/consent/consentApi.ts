import { getJson, postJson } from '@/lib/api'

/** Vårdnadshavarsamtycke (§KM.6, `#195`). Allt kräver inloggning — samtycket hör till kontot. */

export interface ConsentText {
  version: string
  text: string
}

export interface MyConsent {
  hasConsentedToCurrent: boolean
  version: string | null
  grantedUtc: string | null
  text: string | null
}

export const getCurrentConsent = (): Promise<ConsentText> =>
  getJson<ConsentText>('/api/v1/consent/current')

export const getMyConsent = (): Promise<MyConsent> => getJson<MyConsent>('/api/v1/consent/me')

export const grantConsent = (version: string): Promise<void> =>
  postJson<void>('/api/v1/consent', { version })
