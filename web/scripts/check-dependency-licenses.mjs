import { readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const reportPath = path.resolve(repositoryRoot, process.argv[2] ?? 'frontend-licenses.json')
const policy = readJson(path.join(repositoryRoot, 'dependency-license-policy.json'))
const report = readJson(reportPath)
const approvedLicenses = new Set(policy.approvedLicenses)
const rejectedLicenses = Object.keys(report).filter((license) => !approvedLicenses.has(license))

if (rejectedLicenses.length > 0) {
  console.error(`发现未审批的生产依赖许可证: ${rejectedLicenses.join(', ')}`)
  process.exit(1)
}

const packageCount = Object.values(report).reduce((total, packages) => total + packages.length, 0)
console.info(`Dependency licenses: ${packageCount} packages, ${Object.keys(report).length} approved license expressions`)

function readJson(filePath) {
  return JSON.parse(readFileSync(filePath, 'utf8').replace(/^\uFEFF/, ''))
}
