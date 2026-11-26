const REMEMBERED_USER_NAME_KEY = 'login-remembered-user-name'

export function getRememberedLoginUserName(): string {
  return window.localStorage.getItem(REMEMBERED_USER_NAME_KEY)?.trim() ?? ''
}

export function saveRememberedLoginUserName(userName: string) {
  window.localStorage.setItem(REMEMBERED_USER_NAME_KEY, userName.trim())
}

export function clearRememberedLoginUserName() {
  window.localStorage.removeItem(REMEMBERED_USER_NAME_KEY)
}
