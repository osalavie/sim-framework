<template>
  <!--
    第三方提供方回调落点（与 Provider 控制台登记的 Redirect URI 一致）。
    这里不渲染界面：处理完成后直接跳转，失败时回到发起页（登录页或账户安全页）提示错误。
  -->
  <main class="external-callback-page"></main>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { LocationQueryValue, RouteLocationRaw } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { routeConfig } from '@/config/routeConfig'
import { t } from '@/i18n'
import { useDiscreteApi } from '@/hooks/discreteApi'
import { formatErrorMessage } from '@/utils/errorHandler'
import {
  consumeExternalAuthOrigin,
  savePendingExternalAuthNotice,
  savePendingExternalAuthResult,
} from '@/modules/authorization/utils/externalAuth'
import type { ExternalAuthOrigin } from '@/modules/authorization/utils/externalAuth'
import type { ExternalLoginCallback } from '@/modules/authorization/api/user/userModels'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()

function readQueryValue(value?: LocationQueryValue | LocationQueryValue[]): string | undefined {
  const raw = Array.isArray(value) ? value[0] : value
  return raw ?? undefined
}

function getCallbackQuery(): ExternalLoginCallback {
  const query: ExternalLoginCallback = {}

  for (const [key, value] of Object.entries(route.query)) {
    const raw = readQueryValue(value)
    if (raw !== undefined) query[key] = raw
  }

  return query
}

function getProviderFromState(): string | undefined {
  const state = readQueryValue(route.query.state)
  const provider = state?.split('.')[0]?.trim()
  return provider || undefined
}

/**
 * 后端业务异常消息不带本地化，这里按错误码映射为界面语言文案；
 * 未匹配的错误码沿用 formatErrorMessage 的默认提取逻辑。
 */
function resolveErrorMessage(error: unknown): string {
  const code = (error as { response?: { data?: { error?: { code?: string } } } })
    ?.response?.data?.error?.code
  if (code === 'Sim:System:ExternalLoginRegistrationDisabled') {
    return t('Auth:ExternalLoginRegistrationDisabled')
  }
  return formatErrorMessage(error, t('Auth:ExternalLoginFailed'))
}

/** 发起页优先取流程开始时记录的来源，缺失时按当前登录态兜底。 */
function resolveOrigin(): ExternalAuthOrigin {
  return consumeExternalAuthOrigin() ?? (userStore.isLoggedIn() ? 'security' : 'login')
}

/**
 * 失败回到发起页。发起页为登录页但当前已登录时改回账户安全页，
 * 避免登录路由把已登录用户重定向到后台而丢失这次错误提示。
 */
function originRoute(origin: ExternalAuthOrigin): RouteLocationRaw {
  if (origin === 'security' || userStore.isLoggedIn()) return { name: 'UserSecurity' }
  return { name: 'Login' }
}

async function processCallback() {
  const origin = resolveOrigin()

  try {
    const provider = getProviderFromState()
    if (!provider || !readQueryValue(route.query.state)) {
      throw new Error(t('Auth:ExternalLoginFailed'))
    }

    const response = await userStore.completeExternalLogin(provider, getCallbackQuery())

    if (response.externalLoginBindingCompleted) {
      useDiscreteApi.message.success(t('Auth:ExternalCallbackBindingSuccess'))
      await router.replace({ name: 'UserSecurity' })
      return
    }

    if (response.requiresExternalLoginRegistration || response.requiresTwoFactor) {
      savePendingExternalAuthResult(response)
      await router.replace({ name: 'Login' })
      return
    }

    useDiscreteApi.message.success(t('Auth:ExternalCallbackLoginSuccess'))
    await router.replace(routeConfig.adminDefaultRedirect)
  } catch (error) {
    savePendingExternalAuthNotice(resolveErrorMessage(error))
    await router.replace(originRoute(origin))
  }
}

onMounted(() => {
  void processCallback()
})
</script>

<style scoped>
.external-callback-page {
  min-height: 100dvh;
  background: var(--theme-bg-base);
}
/* Paper Ocean：回调中间页保持不可见过渡，底色统一为纸底令牌 */
.external-callback-page {
  min-height: 100vh;
  background: var(--po-bg);
}
</style>
