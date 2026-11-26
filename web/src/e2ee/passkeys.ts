import { formatPasskeyName } from '@/utils/deviceEnvironment'
import { beginPasskeyDiagnostic } from './passkeyDiagnostics'
import { fromBase64Url, randomBytes, toBase64Url } from './encoding'
import { registerE2eeCancellation } from './keysAsync'
import { selectPasskeyUnlock } from './deviceStore'
import { passkeyApi } from './passkeyApi'
import { unwrapUmkWithPasskey, wrapUmkWithPasskey } from './passkeyCipher'

type PrfInputs = AuthenticationExtensionsClientInputs & { prf: { eval?: { first: Uint8Array<ArrayBuffer> } } }
type PrfOutputs = AuthenticationExtensionsClientOutputs & { prf?: { enabled?: boolean; results?: { first?: ArrayBuffer } } }
type OptionsJson = Record<string, unknown> & { challenge: string; user?: { id: string; name: string; displayName: string };
  rp?: PublicKeyCredentialRpEntity; rpId?: string; pubKeyCredParams?: PublicKeyCredentialParameters[];
  excludeCredentials?: Array<{ id: string; type: PublicKeyCredentialType }>;
  allowCredentials?: Array<{ id: string; type: PublicKeyCredentialType }> }

export class PasskeyError extends Error {
  constructor(public code: 'unsupported' | 'prf-unavailable' | 'not-allowed' | 'cancelled' | 'failed') { super('Passkey operation ' + code); this.name = 'PasskeyError' }
}

export function supportsPasskeyUnlock(): boolean {
  return typeof window !== 'undefined' && window.isSecureContext && typeof PublicKeyCredential !== 'undefined' &&
    typeof navigator.credentials?.create === 'function' && typeof navigator.credentials?.get === 'function' && Boolean(crypto.subtle)
}

function buffer(value: string): Uint8Array<ArrayBuffer> { return new Uint8Array(fromBase64Url(value)) }
function publicKey(value: unknown): OptionsJson {
  if (!value || typeof value !== 'object' || typeof (value as OptionsJson).challenge !== 'string') throw new PasskeyError('failed')
  return value as OptionsJson
}
function descriptors(values: OptionsJson['allowCredentials']): PublicKeyCredentialDescriptor[] {
  return (values ?? []).map((value) => ({ type: 'public-key', id: buffer(value.id) }))
}
function credential(value: Credential | null): PublicKeyCredential {
  if (!value || value.type !== 'public-key' || !('rawId' in value)) throw new PasskeyError('failed')
  return value as PublicKeyCredential
}
function prfResult(value: PublicKeyCredential): Uint8Array | null {
  const result = (value.getClientExtensionResults() as PrfOutputs).prf?.results?.first
  return result?.byteLength === 32 ? new Uint8Array(result) : null
}

function prfMetadata(value: PublicKeyCredential) {
  const output = (value.getClientExtensionResults() as PrfOutputs).prf
  return { attachment: value.authenticatorAttachment ?? undefined, prfPresent: Boolean(output), ...(typeof output?.enabled === 'boolean' ? { prfEnabled: output.enabled } : {}),
    firstLength: output?.results?.first?.byteLength ?? 0 }
}

/** Explicit allowlist: never send getClientExtensionResults() / PRF outputs to the server. */
export function serializePasskey(value: PublicKeyCredential): Record<string, unknown> {
  const response = value.response
  const clientDataJSON = toBase64Url(new Uint8Array(response.clientDataJSON))
  const body = 'attestationObject' in response
    ? { clientDataJSON, attestationObject: toBase64Url(new Uint8Array((response as AuthenticatorAttestationResponse).attestationObject)),
        transports: (response as AuthenticatorAttestationResponse).getTransports?.() ?? [] }
    : { clientDataJSON, authenticatorData: toBase64Url(new Uint8Array((response as AuthenticatorAssertionResponse).authenticatorData)),
        signature: toBase64Url(new Uint8Array((response as AuthenticatorAssertionResponse).signature)),
        userHandle: (response as AuthenticatorAssertionResponse).userHandle
          ? toBase64Url(new Uint8Array((response as AuthenticatorAssertionResponse).userHandle!)) : null }
  return { id: toBase64Url(new Uint8Array(value.rawId)), rawId: toBase64Url(new Uint8Array(value.rawId)), type: 'public-key', response: body }
}

function begin() {
  if (!supportsPasskeyUnlock()) throw new PasskeyError('unsupported')
  const controller = new AbortController()
  const unregister = registerE2eeCancellation(() => controller.abort())
  return { signal: controller.signal, check: () => { if (controller.signal.aborted) throw new PasskeyError('cancelled') },
    finish: () => { unregister(); controller.abort() } }
}
function error(value: unknown): PasskeyError {
  if (value instanceof PasskeyError) return value
  return new PasskeyError(value instanceof DOMException && value.name === 'AbortError' ? 'cancelled'
    : value instanceof DOMException && value.name === 'NotAllowedError' ? 'not-allowed' : 'failed')
}

export async function registerPasskey(userId: string, umk: Uint8Array, keyVersion: number): Promise<string> {
  const diagnostic = beginPasskeyDiagnostic('registration')
  let operation: ReturnType<typeof begin> | undefined
  const snapshot = new Uint8Array(umk)
  let prf: Uint8Array | null = null
  let checkedUmk: Uint8Array | null = null
  try {
    operation = begin()
    diagnostic.mark('registration-options')
    const options = await passkeyApi.registrationOptions(operation.signal)
    operation.check()
    if (options.keyVersion !== keyVersion || !options.prfSalt) throw new PasskeyError('failed')
    const json = publicKey(options.publicKey)
    if (!json.user || !json.rp?.id || !json.pubKeyCredParams) throw new PasskeyError('failed')
    const salt = buffer(options.prfSalt)
    if (salt.length !== 32) throw new PasskeyError('failed')
    diagnostic.mark('credential-create', { rpId: json.rp.id, saltLength: salt.length, excludeCount: json.excludeCredentials?.length ?? 0 })
    const created = credential(await navigator.credentials.create({ signal: operation.signal, publicKey: {
      challenge: buffer(json.challenge), rp: json.rp, user: { ...json.user, id: buffer(json.user.id) },
      pubKeyCredParams: json.pubKeyCredParams, excludeCredentials: descriptors(json.excludeCredentials),
      authenticatorSelection: { residentKey: 'preferred', userVerification: 'required' }, attestation: 'none', timeout: 120_000,
      extensions: { prf: { eval: { first: salt } } } as PrfInputs,
    } }))
    operation.check()
    // Verify the get() path before persisting a usable unlock method. Creation-time
    // PRF support alone does not prove that subsequent assertions return PRF output.
    const creationPrf = prfResult(created)
    diagnostic.mark('credential-created', prfMetadata(created))
    prf = creationPrf
    diagnostic.mark('registration-get', { rpId: json.rp.id, allowCount: 1 })
    const evaluated = credential(await navigator.credentials.get({ signal: operation.signal, publicKey: {
      challenge: new Uint8Array(randomBytes(32)), rpId: json.rp.id, timeout: 120_000,
      allowCredentials: [{ type: 'public-key', id: created.rawId }], userVerification: 'required',
      extensions: { prf: { eval: { first: salt } } } as PrfInputs,
    } }))
    operation.check()
    diagnostic.mark('registration-get-result', { ...prfMetadata(evaluated), credentialMatches: toBase64Url(new Uint8Array(evaluated.rawId)) === toBase64Url(new Uint8Array(created.rawId)) })
    if (toBase64Url(new Uint8Array(evaluated.rawId)) !== toBase64Url(new Uint8Array(created.rawId))) throw new PasskeyError('failed')
    const assertionPrf = prfResult(evaluated)
    if (!assertionPrf) throw new PasskeyError('prf-unavailable')
    diagnostic.mark('registration-prf-consistency', { outputsMatch: !creationPrf || assertionPrf.every((byte, index) => byte === creationPrf[index]) })
    try {
      if (creationPrf && !assertionPrf.every((byte, index) => byte === creationPrf[index])) throw new PasskeyError('prf-unavailable')
    } catch (value) { assertionPrf.fill(0); throw value }
    creationPrf?.fill(0)
    prf = assertionPrf
    const context = { userId, keyVersion, credentialId: toBase64Url(new Uint8Array(created.rawId)), rpId: json.rp.id, prfSalt: options.prfSalt }
    diagnostic.mark('local-wrap-check')
    const wrappedUmk = await wrapUmkWithPasskey(prf, snapshot, context)
    checkedUmk = await unwrapUmkWithPasskey(prf, wrappedUmk, context)
    if (!checkedUmk.every((byte, index) => byte === snapshot[index])) throw new PasskeyError('failed')
    operation.check()
    // Optional device model/OS + browser. The server adds a display-only short code.
    const name = await formatPasskeyName()
    operation.check()
    diagnostic.mark('registration-submit')
    const registered = await passkeyApi.register({ challengeId: options.challengeId, name, credential: serializePasskey(created), wrappedUmk }, operation.signal)
    operation.check()
    if (!registered.id) throw new PasskeyError('failed')
    diagnostic.mark('local-preference-save')
    await selectPasskeyUnlock(userId, registered.id)
    operation.check()
    diagnostic.mark('complete')
    return registered.id
  } catch (value) { const failure = error(value); diagnostic.fail(value, failure.code); throw failure }
  finally { prf?.fill(0); snapshot.fill(0); checkedUmk?.fill(0); operation?.finish() }
}

export async function unlockWithPasskey(
  userId: string,
  id: string,
  expectedKeyVersion: number,
  config: { selectUnlock?: boolean } = {},
): Promise<Uint8Array> {
  const diagnostic = beginPasskeyDiagnostic('unlock')
  let operation: ReturnType<typeof begin> | undefined
  let prf: Uint8Array | null = null
  let umk: Uint8Array | null = null
  try {
    operation = begin()
    diagnostic.mark('unlock-options')
    const options = await passkeyApi.unlockOptions(id, operation.signal)
    operation.check()
    if (options.keyVersion !== expectedKeyVersion || !options.prfSalt) throw new PasskeyError('failed')
    const json = publicKey(options.publicKey)
    diagnostic.mark('unlock-get', { rpId: json.rpId, allowCount: json.allowCredentials?.length ?? 0, saltLength: buffer(options.prfSalt).length })
    const asserted = credential(await navigator.credentials.get({ signal: operation.signal, publicKey: {
      challenge: buffer(json.challenge), rpId: json.rpId, timeout: 120_000, userVerification: 'required',
      allowCredentials: descriptors(json.allowCredentials), extensions: { prf: { eval: { first: buffer(options.prfSalt) } } } as PrfInputs,
    } }))
    operation.check()
    diagnostic.mark('unlock-get-result', { ...prfMetadata(asserted), credentialMatches: json.allowCredentials?.some(item => item.id === toBase64Url(new Uint8Array(asserted.rawId))) ?? false })
    if (!json.allowCredentials?.some(item => item.id === toBase64Url(new Uint8Array(asserted.rawId)))) throw new PasskeyError('failed')
    prf = prfResult(asserted)
    if (!prf) throw new PasskeyError('prf-unavailable')
    diagnostic.mark('unlock-submit')
    const saved = await passkeyApi.unlock({ challengeId: options.challengeId, credential: serializePasskey(asserted) }, operation.signal)
    operation.check()
    if (!saved.credentialId || !saved.rpId || !saved.wrappedUmk || saved.keyVersion !== expectedKeyVersion ||
        saved.rpId !== json.rpId || saved.prfSalt !== options.prfSalt || saved.credentialId !== toBase64Url(new Uint8Array(asserted.rawId))) throw new PasskeyError('failed')
    diagnostic.mark('local-unwrap')
    umk = await unwrapUmkWithPasskey(prf, saved.wrappedUmk, { userId, keyVersion: expectedKeyVersion,
      credentialId: saved.credentialId, rpId: saved.rpId, prfSalt: saved.prfSalt! })
    operation.check()
    if (config.selectUnlock !== false) {
      diagnostic.mark('local-preference-save')
      await selectPasskeyUnlock(userId, id)
      operation.check()
    }
    const result = umk
    umk = null
    diagnostic.mark('complete')
    return result
  } catch (value) { const failure = error(value); diagnostic.fail(value, failure.code); throw failure }
  finally { prf?.fill(0); umk?.fill(0); operation?.finish() }
}
