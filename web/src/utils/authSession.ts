let accessToken: string | undefined

export function getAccessToken() {
  return accessToken
}

export function setAccessToken(token: string) {
  accessToken = token
}

export function clearAccessToken() {
  accessToken = undefined
}

// 清理旧版本持久化的令牌。升级后由 HttpOnly Cookie 管理 Refresh Token，
// Access Token 只在当前页面内存中存在。
if (typeof window !== 'undefined') {
  window.localStorage.removeItem('access_token')
  window.localStorage.removeItem('refresh_token')
}
