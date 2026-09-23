import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { getAuthJson, postJson } from '@/lib/api'
import {
  disableTeamPush,
  enableTeamPush,
  isPushSupported,
  isTeamPushEnabled,
  notificationPermission,
} from '@/lib/push'

// API-lagret mockas: den här modulens jobb är webbläsarmekaniken och kontraktet mot servern,
// inte fetch-stacken. Vi vaktar att rätt endpoint anropas med rätt kropp.
vi.mock('@/lib/api', () => ({
  getAuthJson: vi.fn(),
  postJson: vi.fn(),
}))

const getAuthJsonMock = vi.mocked(getAuthJson)
const postJsonMock = vi.mocked(postJson)

interface EnvOptions {
  permission?: NotificationPermission
  requestResult?: NotificationPermission
  existingSubscription?: unknown
  subscribeResult?: unknown
  supported?: boolean
}

const subscription = {
  endpoint: 'https://push.example/abc',
  toJSON: () => ({ endpoint: 'https://push.example/abc', keys: { p256dh: 'P256', auth: 'AUTH' } }),
}

function installPushEnv({
  permission = 'default',
  requestResult = 'granted',
  existingSubscription = null,
  subscribeResult = subscription,
  supported = true,
}: EnvOptions = {}) {
  const subscribe = vi
    .fn<(options: PushSubscriptionOptionsInit) => Promise<unknown>>()
    .mockResolvedValue(subscribeResult)
  const getSubscription = vi.fn<() => Promise<unknown>>().mockResolvedValue(existingSubscription)

  if (supported) {
    vi.stubGlobal('PushManager', function PushManager() {})
    vi.stubGlobal('Notification', {
      permission,
      requestPermission: vi.fn().mockResolvedValue(requestResult),
    })
    vi.stubGlobal('navigator', {
      serviceWorker: { ready: Promise.resolve({ pushManager: { getSubscription, subscribe } }) },
    })
  } else {
    // En webbläsare utan stöd: inga av de tre globalerna finns.
    vi.stubGlobal('navigator', {})
  }

  return { subscribe, getSubscription }
}

beforeEach(() => {
  localStorage.clear()
  getAuthJsonMock.mockReset()
  postJsonMock.mockReset()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('stöd och tillstånd', () => {
  it('säger att stöd saknas när globalerna inte finns', () => {
    installPushEnv({ supported: false })

    expect(isPushSupported()).toBe(false)
    expect(notificationPermission()).toBe('denied')
  })

  it('läser tillståndet ur webbläsaren när stöd finns', () => {
    installPushEnv({ permission: 'granted' })

    expect(isPushSupported()).toBe(true)
    expect(notificationPermission()).toBe('granted')
  })
})

describe('på/av-läget per lag', () => {
  it('är av tills enheten slagit på för laget', () => {
    installPushEnv({ permission: 'granted' })

    expect(isTeamPushEnabled('gul')).toBe(false)
  })

  it('kräver både tillstånd och lokal flagga', () => {
    installPushEnv({ permission: 'default' })
    localStorage.setItem('karra.push.gul', '1')

    // Flaggan finns men tillståndet är inte givet: räknas som av.
    expect(isTeamPushEnabled('gul')).toBe(false)
  })
})

describe('slå på notiser', () => {
  it('svarar unsupported utan stöd, utan att röra servern', async () => {
    installPushEnv({ supported: false })

    expect(await enableTeamPush('gul')).toBe('unsupported')
    expect(getAuthJsonMock).not.toHaveBeenCalled()
    expect(postJsonMock).not.toHaveBeenCalled()
  })

  it('svarar denied när tillståndet nekas', async () => {
    installPushEnv({ permission: 'default', requestResult: 'denied' })

    expect(await enableTeamPush('gul')).toBe('denied')
    expect(postJsonMock).not.toHaveBeenCalled()
  })

  it('prenumererar och skickar adressen till laget', async () => {
    const { subscribe } = installPushEnv({ permission: 'granted' })
    getAuthJsonMock.mockResolvedValue({ publicKey: 'BEl62iUYgUivxIkv69yViEuiBIa-Ib9-Skq' })
    postJsonMock.mockResolvedValue(undefined)

    expect(await enableTeamPush('gul')).toBe('enabled')

    // userVisibleOnly är ett krav i Chrome; nyckeln kommer som byte, inte som text.
    const options = subscribe.mock.calls[0]?.[0]
    expect(options?.userVisibleOnly).toBe(true)
    expect(options?.applicationServerKey).toBeInstanceOf(Uint8Array)
    expect(postJsonMock).toHaveBeenCalledWith(
      '/api/v1/teams/gul/push',
      { endpoint: 'https://push.example/abc', p256dh: 'P256', auth: 'AUTH' },
      { method: 'POST' },
    )
    expect(isTeamPushEnabled('gul')).toBe(true)
  })

  it('återanvänder en befintlig prenumeration i stället för att skapa en ny', async () => {
    const { subscribe } = installPushEnv({
      permission: 'granted',
      existingSubscription: subscription,
    })
    getAuthJsonMock.mockResolvedValue({ publicKey: 'BEl62iUYgUivxIkv' })
    postJsonMock.mockResolvedValue(undefined)

    expect(await enableTeamPush('gul')).toBe('enabled')
    expect(subscribe).not.toHaveBeenCalled()
  })

  it('svarar error och lämnar flaggan av när något går fel', async () => {
    installPushEnv({ permission: 'granted' })
    getAuthJsonMock.mockRejectedValue(new Error('push av på servern'))

    expect(await enableTeamPush('gul')).toBe('error')
    expect(isTeamPushEnabled('gul')).toBe(false)
  })
})

describe('slå av notiser', () => {
  it('avregistrerar adressen mot laget och städar flaggan', async () => {
    installPushEnv({ permission: 'granted', existingSubscription: subscription })
    postJsonMock.mockResolvedValue(undefined)
    localStorage.setItem('karra.push.gul', '1')

    expect(await disableTeamPush('gul')).toBe(true)
    expect(postJsonMock).toHaveBeenCalledWith(
      '/api/v1/teams/gul/push',
      { endpoint: 'https://push.example/abc' },
      { method: 'DELETE' },
    )
    expect(localStorage.getItem('karra.push.gul')).toBe('')
  })

  it('städar flaggan även när ingen prenumeration finns kvar', async () => {
    installPushEnv({ permission: 'granted', existingSubscription: null })
    localStorage.setItem('karra.push.gul', '1')

    expect(await disableTeamPush('gul')).toBe(true)
    expect(postJsonMock).not.toHaveBeenCalled()
    expect(localStorage.getItem('karra.push.gul')).toBe('')
  })

  it('svarar false när avregistreringen mot servern misslyckas', async () => {
    installPushEnv({ permission: 'granted', existingSubscription: subscription })
    postJsonMock.mockRejectedValue(new Error('nätet nere'))

    expect(await disableTeamPush('gul')).toBe(false)
  })
})
