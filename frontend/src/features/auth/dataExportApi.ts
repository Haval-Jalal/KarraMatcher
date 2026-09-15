import { getAuthJson } from '@/lib/api'

/**
 * Registerutdraget (`#67`) — allt servern har om det inloggade kontot.
 *
 * <h3>Litet med flit</h3>
 *
 * Servern lagrar så lite att utdraget ryms på en skärm. Inget om ett barn, ingen statistik —
 * spelarkortet lämnar aldrig telefonen (§KM.2) och beskrivs därför bara med en text om var
 * det finns. Tider kommer i UTC; klienten visar dem i svensk tid (§KM.5).
 */

/** Enum-namn som backend skickar. Klienten översätter dem till svenska. */
export type CarpoolDirectionCode = 'ToMatch' | 'FromMatch' | 'Both'
export type CarpoolOfferStatusCode = 'Open' | 'Withdrawn'
export type CarpoolRequestStatusCode = 'Pending' | 'Accepted' | 'Denied' | 'Retracted'
export type AttendanceStatusCode = 'Coming' | 'CantCome' | 'Maybe'

export interface AccountExportContact {
  email: string
  firstName: string | null
  lastName: string | null
  createdUtc: string
  lastSignedInUtc: string | null
}

export interface CarpoolOfferExport {
  matchOpponent: string
  matchKickoffUtc: string
  direction: CarpoolDirectionCode
  departurePlace: string
  departureUtc: string
  seats: number
  note: string | null
  status: CarpoolOfferStatusCode
  createdUtc: string
}

export interface CarpoolRequestExport {
  matchOpponent: string
  matchKickoffUtc: string
  seats: number
  message: string | null
  responseMessage: string | null
  status: CarpoolRequestStatusCode
  createdUtc: string
}

export interface AttendanceResponseExport {
  matchOpponent: string
  matchKickoffUtc: string
  status: AttendanceStatusCode
  count: number
  createdUtc: string
}

export interface NotificationPreferenceExport {
  teamName: string
  matchChanges: boolean
  carpool: boolean
  reminders: boolean
  updatedUtc: string
}

export interface PushSubscriptionExport {
  teamName: string
  createdUtc: string
  lastUsedUtc: string | null
}

export interface DeviceOnlyDataNotice {
  message: string
}

export interface AccountExport {
  exportedUtc: string
  account: AccountExportContact
  carpoolOffers: CarpoolOfferExport[]
  carpoolRequests: CarpoolRequestExport[]
  attendanceResponses: AttendanceResponseExport[]
  notificationSettings: NotificationPreferenceExport[]
  pushSubscriptions: PushSubscriptionExport[]
  playerCard: DeviceOnlyDataNotice
}

/** Hämtar mitt eget registerutdrag. Kräver inloggning. */
export function getAccountExport(signal?: AbortSignal): Promise<AccountExport> {
  return getAuthJson<AccountExport>('/api/v1/auth/export', signal)
}
