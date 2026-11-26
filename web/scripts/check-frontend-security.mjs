import { createHash } from 'node:crypto'
import { readFile, readdir, writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const root = fileURLToPath(new URL('../', import.meta.url))
const dist = path.join(root, 'dist')
const manifestName = 'release-integrity.json'
const verify = process.argv.includes('--verify')

async function inventory(directory, prefix = '') {
  const files = {}
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const relative = prefix + entry.name
    const absolute = path.join(directory, entry.name)
    if (entry.isSymbolicLink()) throw new Error(`Symlink in release: ${relative}`)
    if (entry.isDirectory()) Object.assign(files, await inventory(absolute, `${relative}/`))
    else if (relative !== manifestName) {
      if (relative.endsWith('.map')) throw new Error(`Source map in release: ${relative}`)
      files[relative] = createHash('sha256').update(await readFile(absolute)).digest('hex')
    }
  }
  return files
}

for (const relative of ['deploy/nginx/simframework.conf']) {
  const config = await readFile(path.join(root, '..', relative), 'utf8')
  const policies = [...config.matchAll(/add_header Content-Security-Policy "([^"]+)"/g)]
  if (!policies.length) throw new Error(`Missing CSP: ${relative}`)
  for (const [, policy] of policies) {
    for (const directive of ["script-src 'self'", "worker-src 'self'", "object-src 'none'", "base-uri 'self'"]) {
      if (!policy.includes(`${directive};`)) throw new Error(`Missing ${directive}: ${relative}`)
    }
  }
}

const html = await readFile(path.join(dist, 'index.html'), 'utf8')
for (const [, attrs, body] of html.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script\s*>/gi)) {
  const source = /\bsrc=["']([^"']+)["']/i.exec(attrs)?.[1]
  if (!source || body.trim() || !source.startsWith('/assets/') || source.includes('..')) {
    throw new Error('Release HTML must only load bundled same-origin scripts.')
  }
}

const files = await inventory(dist)
if (verify) {
  const expected = JSON.parse(await readFile(path.join(dist, manifestName), 'utf8'))
  if (JSON.stringify(Object.entries(files).sort()) !== JSON.stringify(Object.entries(expected.files).sort())) {
    throw new Error('Release contents differ from the integrity manifest.')
  }
} else {
  await writeFile(path.join(dist, manifestName), JSON.stringify({ algorithm: 'sha256', files }, null, 2) + '\r\n')
}
console.log(`Frontend security checks passed; ${Object.keys(files).length} release files ${verify ? 'verified' : 'hashed'}.`)
