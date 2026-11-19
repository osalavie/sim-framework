import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest'

beforeEach(() => { vi.resetModules(); vi.spyOn(console, 'info').mockImplementation(() => {}) })
afterEach(() => vi.restoreAllMocks())

describe('passkey diagnostics', () => {
  it('retains only diagnostic metadata and never raw credentials or exception bodies', async () => {
    const { beginPasskeyDiagnostic, getPasskeyDiagnosticReport } = await import('./passkeyDiagnostics')
    const diagnostic = beginPasskeyDiagnostic('registration')
    const secret = 'SECRET-prf-umk-credential-body'
    diagnostic.mark('registration-get-result', { prfPresent: true, firstLength: 32,
      credential: secret, prf: secret, umk: secret } as never)
    diagnostic.fail(Object.assign(new Error(secret), { name: 'TypeError', response: { status: 400, data: secret },
      config: { data: secret } }), 'failed')
    const report = getPasskeyDiagnosticReport()
    expect(report).not.toContain(secret)
    expect(report).toContain('TypeError')
    expect(report).toContain('httpStatus')
    expect(report).toContain('registration-get-result.failed')
    expect(vi.mocked(console.info).mock.calls.flat().join('')).not.toContain(secret)
  })

  it('reports Edge rather than its embedded Chrome version', async () => {
    vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 Chrome/152.0.0.0 Safari/537.36 Edg/152.0.4191.66')
    const { beginPasskeyDiagnostic, getPasskeyDiagnosticReport } = await import('./passkeyDiagnostics')
    beginPasskeyDiagnostic('registration')
    expect(JSON.parse(getPasskeyDiagnosticReport()).events[0].browser).toBe('Edg/152.0.4191.66')
  })

  it('bounds tab-local history and separates operation identifiers', async () => {
    const { beginPasskeyDiagnostic, getPasskeyDiagnosticReport } = await import('./passkeyDiagnostics')
    const first = beginPasskeyDiagnostic('registration')
    for (let i = 0; i < 110; i++) first.mark('registration-get')
    const second = beginPasskeyDiagnostic('unlock')
    second.mark('unlock-get')
    const events = JSON.parse(getPasskeyDiagnosticReport()).events
    expect(events).toHaveLength(100)
    expect(new Set(events.map((event: { id: string }) => event.id)).size).toBe(2)
    expect(events.at(-1).stage).toBe('unlock-get')
  })
})
