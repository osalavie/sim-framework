import { h, withDirectives } from 'vue'
import type { DataTableColumn, DataTableColumns } from 'naive-ui'
import type { AuditFields } from '@/components/AuditInfo/useAuditNames'
import { tooltipDirective } from '@/directives/tooltip'
import { t } from '@/i18n'
import { formatDate } from '@/utils/date'

interface AuditColumnOptions {
  resolveName: (id?: string | null) => string
  resolveAccount: (id?: string | null) => string | undefined
  getSortOrder?: (field: string) => 'ascend' | 'descend' | false
}

const nameCell = (name: string, account: string | undefined) =>
  withDirectives(
    h(
      'span',
      { class: 'po-audit-name', style: 'display:block;overflow-wrap:anywhere;word-break:break-word' },
      name,
    ),
    [[tooltipDirective, account ?? '']],
  )

const timeColumn = (
  title: string,
  key: 'creationTime' | 'lastModificationTime',
  getSortOrder?: AuditColumnOptions['getSortOrder'],
): DataTableColumn<AuditFields> => ({
  title,
  key,
  width: 180,
  resizable: true,
  ellipsis: { tooltip: true },
  ...(getSortOrder ? { sorter: 'default' as const, sortOrder: getSortOrder(key) } : {}),
  render: (row) => (row[key] ? formatDate(row[key], 'YYYY-MM-DD HH:mm') : '-'),
})

const identityColumn = (
  title: string,
  key: 'creatorId' | 'lastModifierId',
  options: AuditColumnOptions,
): DataTableColumn<AuditFields> => ({
  title,
  key,
  width: 150,
  resizable: true,
  ellipsis: { tooltip: false },
  render: (row) => nameCell(options.resolveName(row[key]), options.resolveAccount(row[key])),
})

/** 统一的创建人/创建时间/修改人/修改时间四列。 */
export function createAuditColumns<T extends AuditFields>(
  options: AuditColumnOptions,
): DataTableColumns<T> {
  return [
    timeColumn(t('Common:CreatedAt'), 'creationTime', options.getSortOrder),
    identityColumn(t('Audit:Creator'), 'creatorId', options),
    timeColumn(t('Common:UpdatedAt'), 'lastModificationTime', options.getSortOrder),
    identityColumn(t('Audit:LastModifier'), 'lastModifierId', options),
  ] as DataTableColumns<T>
}
