import { describe, expect, it } from 'vitest'
import {
  buildAccessLogQuery,
  buildScopeGroups,
  createAccessLogFilter,
  formatListInput,
  parseListInput,
} from './index'

describe('open platform list input', () => {
  it('trims, splits and de-duplicates input', () => {
    expect(parseListInput(' blog:article:read , blog:article:write\nblog:article:read ')).toEqual([
      'blog:article:read',
      'blog:article:write',
    ])
  })

  it('accepts full-width separators and blank lines', () => {
    expect(parseListInput('10.0.0.1；10.0.0.2，\n\n10.0.0.3')).toEqual([
      '10.0.0.1',
      '10.0.0.2',
      '10.0.0.3',
    ])
  })

  it('returns an empty list for blank input', () => {
    expect(parseListInput('   \n  ')).toEqual([])
    expect(formatListInput(undefined)).toBe('')
    expect(formatListInput([])).toBe('')
  })

  it('round-trips a list through the textarea format', () => {
    expect(formatListInput(['a', 'b'])).toBe('a\nb')
    expect(parseListInput(formatListInput(['a', 'b']))).toEqual(['a', 'b'])
  })
})

describe('open platform scope options', () => {
  it('labels each option with the permission name and code, grouped by module', () => {
    expect(
      buildScopeGroups([
        { code: 'blog:article:read', name: '查看文章' },
        { code: 'blog:comment:query', name: '查询评论' },
        { code: 'system:user:query', name: '' },
      ]),
    ).toEqual([
      {
        module: 'blog',
        options: [
          { label: '查看文章（blog:article:read）', value: 'blog:article:read' },
          { label: '查询评论（blog:comment:query）', value: 'blog:comment:query' },
        ],
      },
      {
        module: 'system',
        options: [{ label: 'system:user:query', value: 'system:user:query' }],
      },
    ])
  })

  it('keeps the server ordering of groups and options', () => {
    const groups = buildScopeGroups([
      { code: 'system:user:query', name: '查询用户' },
      { code: 'blog:article:create', name: '创建文章' },
      { code: 'system:user:delete', name: '删除用户' },
    ])

    expect(groups.map((group) => group.module)).toEqual(['system', 'blog'])
    const systemGroup = groups.find((group) => group.module === 'system')
    expect(systemGroup?.options.map((option) => option.value)).toEqual([
      'system:user:query',
      'system:user:delete',
    ])
  })

  it('trims codes and drops blank or duplicated entries', () => {
    expect(
      buildScopeGroups([
        { code: ' blog:article:read ', name: '查看文章' },
        { code: 'blog:article:read', name: '别名' },
        { code: '   ', name: '空权限码' },
      ]),
    ).toEqual([
      {
        module: 'blog',
        options: [{ label: '查看文章（blog:article:read）', value: 'blog:article:read' }],
      },
    ])
  })

  it('falls back to the other group when a code carries no module prefix', () => {
    expect(buildScopeGroups([{ code: '*:*:*', name: '超级权限' }])).toEqual([
      { module: 'other', options: [{ label: '超级权限（*:*:*）', value: '*:*:*' }] },
    ])
  })

  it('returns an empty list when the permission list is missing', () => {
    expect(buildScopeGroups(undefined)).toEqual([])
    expect(buildScopeGroups([])).toEqual([])
  })
})

describe('open platform access log query', () => {
  it('only sends pagination when no filter is set', () => {
    expect(buildAccessLogQuery(createAccessLogFilter(), { page: 2, pageSize: 20 })).toEqual({
      skipCount: 20,
      maxResultCount: 20,
    })
  })

  it('trims filters and maps the result switch', () => {
    const query = buildAccessLogQuery(
      { appId: ' op_123 ', succeeded: 'failed', clientIp: ' 10.0.0 ', startTime: '', endTime: '' },
      { page: 1, pageSize: 10 },
    )

    expect(query).toEqual({
      skipCount: 0,
      maxResultCount: 10,
      appId: 'op_123',
      succeeded: false,
      clientIp: '10.0.0',
    })
  })

  it('passes the selected time range through unchanged', () => {
    const query = buildAccessLogQuery(
      {
        ...createAccessLogFilter(),
        succeeded: 'succeeded',
        startTime: '2026-09-14T00:00:00',
        endTime: '2026-09-14T12:00:00',
      },
      { page: 1, pageSize: 10 },
    )

    expect(query).toMatchObject({
      succeeded: true,
      startTime: '2026-09-14T00:00:00',
      endTime: '2026-09-14T12:00:00',
    })
  })
})
