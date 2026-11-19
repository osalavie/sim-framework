/** 解密/解锁失败一律 fail-closed，绝不返回密文或空串冒充明文。 */
export class E2eeError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'E2eeError'
  }
}

export class E2eeDecryptError extends E2eeError {
  constructor(message = 'E2EE decryption failed.') {
    super(message)
    this.name = 'E2eeDecryptError'
  }
}

export class E2eeLockedError extends E2eeError {
  constructor(message = 'E2EE key vault is locked.') {
    super(message)
    this.name = 'E2eeLockedError'
  }
}

export class E2eeCancelledError extends E2eeError {
  constructor(message = 'E2EE unlock was cancelled.') {
    super(message)
    this.name = 'E2eeCancelledError'
  }
}
