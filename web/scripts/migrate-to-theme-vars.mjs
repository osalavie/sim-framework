/**
 * 主题变量迁移脚本
 * 将硬编码的颜色值替换为主题变量
 *
 * 使用方法:
 * node scripts/migrate-to-theme-vars.mjs
 */

import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

// 颜色映射表
const colorMappings = {
  // 文本颜色
  '#333': 'var(--theme-text-base)',
  '#333333': 'var(--theme-text-base)',
  'color: #333': 'color: var(--theme-text-base)',
  'color:#333': 'color: var(--theme-text-base)',

  '#666': 'var(--theme-text-secondary)',
  '#666666': 'var(--theme-text-secondary)',
  'color: #666': 'color: var(--theme-text-secondary)',
  'color:#666': 'color: var(--theme-text-secondary)',

  '#999': 'var(--theme-text-tertiary)',
  '#999999': 'var(--theme-text-tertiary)',
  'color: #999': 'color: var(--theme-text-tertiary)',
  'color:#999': 'color: var(--theme-text-tertiary)',

  // 背景颜色
  '#fff': 'var(--theme-bg-base)',
  '#ffffff': 'var(--theme-bg-base)',
  'background: #fff': 'background: var(--theme-bg-base)',
  'background-color: #fff': 'background-color: var(--theme-bg-base)',
  'background-color: #ffffff': 'background-color: var(--theme-bg-base)',

  '#f5f5f5': 'var(--theme-bg-secondary)',
  'background: #f5f5f5': 'background: var(--theme-bg-secondary)',
  'background-color: #f5f5f5': 'background-color: var(--theme-bg-secondary)',

  '#f0f0f0': 'var(--theme-bg-tertiary)',
  'background: #f0f0f0': 'background: var(--theme-bg-tertiary)',
  'background-color: #f0f0f0': 'background-color: var(--theme-bg-tertiary)',

  // 边框颜色
  '#e0e0e0': 'var(--theme-border)',
  'border: 1px solid #e0e0e0': 'border: 1px solid var(--theme-border)',
  'border-color: #e0e0e0': 'border-color: var(--theme-border)',

  '#d0d0d0': 'var(--theme-border-hover)',
  'border-color: #d0d0d0': 'border-color: var(--theme-border-hover)',

  '#e8e8e8': 'var(--theme-divider)',
  'border-bottom: 1px solid #e8e8e8': 'border-bottom: 1px solid var(--theme-divider)',

  // 主色
  '#2196f3': 'var(--theme-primary)',
  'background: #2196f3': 'background: var(--theme-primary)',
  'background-color: #2196f3': 'background-color: var(--theme-primary)',
  'border-top-color: #2196f3': 'border-top-color: var(--theme-primary)',
}

function migrateFile(filePath) {
  try {
    let content = fs.readFileSync(filePath, 'utf8')
    let modified = false

    // 替换颜色值
    for (const [oldColor, newVar] of Object.entries(colorMappings)) {
      if (content.includes(oldColor)) {
        content = content.replace(new RegExp(oldColor.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'g'), newVar)
        modified = true
      }
    }

    if (modified) {
      fs.writeFileSync(filePath, content, 'utf8')
      console.log(`✅ 已更新: ${filePath}`)
      return true
    }

    return false
  } catch (error) {
    console.error(`❌ 处理文件失败 ${filePath}:`, error.message)
    return false
  }
}

function walkDirectory(dir, fileExtensions = ['.vue', '.scss', '.css']) {
  const files = fs.readdirSync(dir)
  let updatedCount = 0

  for (const file of files) {
    const filePath = path.join(dir, file)
    const stat = fs.statSync(filePath)

    if (stat.isDirectory()) {
      // 跳过 node_modules 和其他不需要处理的目录
      if (!['node_modules', '.git', 'dist', 'build'].includes(file)) {
        updatedCount += walkDirectory(filePath, fileExtensions)
      }
    } else if (fileExtensions.some(ext => file.endsWith(ext))) {
      if (migrateFile(filePath)) {
        updatedCount++
      }
    }
  }

  return updatedCount
}

// 主函数
function main() {
  console.log('🚀 开始迁移颜色值到主题变量...\n')

  const srcDir = path.join(__dirname, '..', 'src')
  const updatedCount = walkDirectory(srcDir)

  console.log(`\n✨ 迁移完成! 共更新了 ${updatedCount} 个文件`)
  console.log('\n📝 请检查更新的文件，确保替换正确')
  console.log('💡 某些特殊情况可能需要手动调整\n')
}

main()
