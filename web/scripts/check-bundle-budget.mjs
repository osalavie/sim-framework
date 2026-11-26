import { readFileSync, readdirSync, statSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const distDirectory = path.join(repositoryRoot, 'dist')
const manifest = JSON.parse(
  readFileSync(path.join(distDirectory, '.vite', 'manifest.json'), 'utf8'),
)
const budget = JSON.parse(
  readFileSync(path.join(repositoryRoot, 'performance-budget.json'), 'utf8'),
)

const assetFiles = walk(distDirectory)
const javascriptFiles = assetFiles.filter((file) => file.endsWith('.js'))
const cssFiles = assetFiles.filter((file) => file.endsWith('.css'))
const initialFiles = collectInitialFiles(manifest)
const errors = []

const totalJavaScriptBytes = sumSizes(javascriptFiles)
const totalCssBytes = sumSizes(cssFiles)
const initialJavaScriptBytes = sumSizes(
  [...initialFiles]
    .filter((file) => file.endsWith('.js'))
    .map((file) => path.join(distDirectory, file)),
)

if (initialJavaScriptBytes > budget.maxInitialJavaScriptBytes) {
  errors.push(
    `入口 JavaScript ${formatBytes(initialJavaScriptBytes)} 超过预算 ${formatBytes(budget.maxInitialJavaScriptBytes)}`,
  )
}

for (const file of javascriptFiles) {
  const size = statSync(file).size
  if (size > budget.maxAsyncChunkBytes && !initialFiles.has(relativeToDist(file))) {
    errors.push(
      `异步块 ${relativeToDist(file)} 为 ${formatBytes(size)}，超过预算 ${formatBytes(budget.maxAsyncChunkBytes)}`,
    )
  }
}

if (totalJavaScriptBytes > budget.maxTotalJavaScriptBytes) {
  errors.push(
    `JavaScript 总量 ${formatBytes(totalJavaScriptBytes)} 超过预算 ${formatBytes(budget.maxTotalJavaScriptBytes)}`,
  )
}

if (totalCssBytes > budget.maxTotalCssBytes) {
  errors.push(`CSS 总量 ${formatBytes(totalCssBytes)} 超过预算 ${formatBytes(budget.maxTotalCssBytes)}`)
}

console.info(
  `Bundle budget: initial JS ${formatBytes(initialJavaScriptBytes)}, total JS ${formatBytes(totalJavaScriptBytes)}, total CSS ${formatBytes(totalCssBytes)}`,
)

if (errors.length > 0) {
  for (const error of errors) console.error(error)
  process.exit(1)
}

function collectInitialFiles(buildManifest) {
  const files = new Set()
  const visited = new Set()

  function visit(key) {
    if (visited.has(key)) return
    visited.add(key)

    const entry = buildManifest[key]
    if (!entry) return
    if (entry.file) files.add(entry.file)
    for (const css of entry.css ?? []) files.add(css)
    for (const dependency of entry.imports ?? []) visit(dependency)
  }

  for (const [key, entry] of Object.entries(buildManifest)) {
    if (entry.isEntry) visit(key)
  }

  return files
}

function walk(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const fullPath = path.join(directory, entry.name)
    return entry.isDirectory() ? walk(fullPath) : [fullPath]
  })
}

function sumSizes(files) {
  return files.reduce((total, file) => total + statSync(file).size, 0)
}

function relativeToDist(file) {
  return path.relative(distDirectory, file).replaceAll('\\', '/')
}

function formatBytes(bytes) {
  return `${(bytes / 1024 / 1024).toFixed(2)} MiB`
}
