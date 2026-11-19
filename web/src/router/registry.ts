import type { Router } from 'vue-router'

let currentRouter: Router | undefined

export function setRouter(router: Router): void {
  currentRouter = router
}

export function getRouter(): Router {
  if (!currentRouter) {
    throw new Error('[Router] Router has not been created yet.')
  }

  return currentRouter
}
