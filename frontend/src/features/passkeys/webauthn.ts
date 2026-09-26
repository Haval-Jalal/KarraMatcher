import { create, get, supported } from '@github/webauthn-json'

import {
  beginPasskeyLogin,
  beginPasskeyRegistration,
  completePasskeyLogin,
  completePasskeyRegistration,
  type PasskeyRequestOptions,
} from './passkeysApi'

/**
 * WebAuthn-ceremonierna i webbläsaren. `@github/webauthn-json` sköter översättningen mellan
 * serverns base64url-JSON och de ArrayBuffers `navigator.credentials` kräver — så vi slipper
 * den felkänsliga konverteringen för hand.
 */

/** Sant när webbläsaren stöder passkeys. Faller aldrig — en gammal webbläsare ska bara sakna knappen. */
export function passkeysSupported(): boolean {
  try {
    return supported()
  } catch {
    return false
  }
}

/** Registrerar en ny passkey på enheten och sparar den på kontot. */
export async function registerPasskey(deviceLabel: string): Promise<void> {
  const options = await beginPasskeyRegistration()
  const attestation = await create({ publicKey: options })
  await completePasskeyRegistration(attestation, deviceLabel)
}

/** Loggar in med en passkey (usernameless). Sätter sessionen vid framgång. */
export async function loginWithPasskey(): Promise<void> {
  const { challengeId, optionsJson } = await beginPasskeyLogin()
  const options = JSON.parse(optionsJson) as PasskeyRequestOptions
  const assertion = await get({ publicKey: options })
  await completePasskeyLogin(challengeId, assertion)
}
