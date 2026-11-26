import { apiConfig } from '@/config/apiConfig'

export interface SetupStatus {
  initialized: boolean
  defaultAdminUserName?: string | null
  siteName?: string | null
  defaultLanguage?: string | null
  defaultTheme?: string | null
  registrationEnabled?: boolean
  loginMaxFailedAttempts?: number
  lockoutDurationMinutes?: number
}

export type SetupProgressStage =
  | 'idle'
  | 'preparing'
  | 'administrator'
  | 'system-account'
  | 'seeding'
  | 'tenant'
  | 'system-modules'
  | 'roles'
  | 'users'
  | 'modules'
  | 'routes'
  | 'permissions'
  | 'configurations'
  | 'approval-numbering'
  | 'organization'
  | 'admin-role'
  | 'basic-role'
  | 'finalizing'
  | 'completed'
  | 'failed'

export interface SetupProgress {
  running: boolean
  stage: SetupProgressStage
  steps?: string[]
  percent: number
  completed: boolean
  error?: string | null
}

export interface InitializeSystemRequest {
  adminUserName: string
  adminPassword: string
  adminNickName?: string
  siteName: string
  defaultLanguage: string
  defaultTheme: string
  registrationEnabled: boolean
  loginMaxFailedAttempts: number
  lockoutDurationMinutes: number
}

const baseUrl = apiConfig.baseUrl.replace(/\/$/, '')

async function readErrorMessage(response: Response, fallback: string): Promise<string> {
  try {
    const payload = (await response.json()) as {
      error?: { message?: string }
      message?: string
      title?: string
    }
    return payload.error?.message || payload.message || payload.title || fallback
  } catch {
    return fallback
  }
}

export async function fetchSetupStatus(): Promise<SetupStatus> {
  const response = await fetch(`${baseUrl}/api/admin/setup/status`, {
    headers: { Accept: 'application/json' },
  })

  if (!response.ok) {
    throw new Error(await readErrorMessage(response, 'Setup status request failed'))
  }

  return (await response.json()) as SetupStatus
}

export async function fetchSetupProgress(): Promise<SetupProgress> {
  const response = await fetch(`${baseUrl}/api/admin/setup/progress`, {
    headers: { Accept: 'application/json' },
  })

  if (!response.ok) {
    throw new Error(await readErrorMessage(response, 'Setup progress request failed'))
  }

  return (await response.json()) as SetupProgress
}

export async function initializeSystem(input: InitializeSystemRequest): Promise<void> {
  const response = await fetch(`${baseUrl}/api/admin/setup/initialize`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(input),
  })

  if (!response.ok) {
    throw new Error(await readErrorMessage(response, 'System initialization failed'))
  }
}
