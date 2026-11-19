export type AuthNavigationDecision =
  | { type: 'allow' }
  | { type: 'redirect-default' }
  | { type: 'redirect-login'; redirect: string }

export interface AuthNavigationContext {
  isLoginRoute: boolean
  isAnonymous: boolean
  isLoggedIn: boolean
  targetPath: string
}

export interface SessionRestoreContext {
  isLoginRoute: boolean
  isAnonymous: boolean
  hasPersistedIdentity: boolean
}

/**
 * Protected routes need a session before authorization is evaluated. The login
 * route also restores a session so a valid refresh cookie can return to admin.
 * An anonymous page may carry a persisted identity after a hard reload; restore
 * it so authenticated UI such as the account menu stays accurate.
 */
export function shouldRestoreSession(context: SessionRestoreContext): boolean {
  return context.isLoginRoute || !context.isAnonymous || context.hasPersistedIdentity
}

export function resolveAuthNavigation(context: AuthNavigationContext): AuthNavigationDecision {
  if (context.isLoginRoute) {
    return context.isLoggedIn ? { type: 'redirect-default' } : { type: 'allow' }
  }

  if (context.isAnonymous) {
    return { type: 'allow' }
  }

  if (!context.isLoggedIn) {
    return { type: 'redirect-login', redirect: context.targetPath }
  }

  return { type: 'allow' }
}
