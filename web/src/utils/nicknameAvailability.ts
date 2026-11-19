import { userApi } from '@/modules/authorization/api/user'

export const NICKNAME_CHECK_DELAY = 400

/**
 * 昵称查重防抖器。连续调用时只有最后一次会发起请求，前一次按“暂未发现冲突”结束，
 * 避免表单异步校验长时间挂起；最终提交仍由后端唯一约束兜底。
 */
export function createDebouncedNicknameChecker(delay = NICKNAME_CHECK_DELAY) {
  let timer: ReturnType<typeof setTimeout> | undefined
  let sequence = 0
  let settlePrevious: ((available: boolean) => void) | null = null

  return (nickName: string, excludeUserId?: string): Promise<boolean> => {
    settlePrevious?.(true)
    const current = ++sequence

    return new Promise<boolean>((resolve) => {
      settlePrevious = resolve
      if (timer) clearTimeout(timer)
      timer = setTimeout(async () => {
        try {
          const available = await userApi.checkNickNameAvailability(nickName, excludeUserId)
          if (current === sequence) {
            settlePrevious = null
            resolve(available)
          }
        } catch {
          if (current === sequence) {
            settlePrevious = null
            resolve(true)
          }
        }
      }, delay)
    })
  }
}
