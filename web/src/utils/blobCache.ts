interface BlobCacheOptions {
  /** 缓存条目上限，超出按 LRU 淘汰 */
  maxEntries?: number
  /** 缓存字节上限，超出按最旧条目淘汰 */
  maxTotalBytes?: number
}

interface CacheEntry {
  promise: Promise<Blob>
  bytes: number
}

/**
 * Blob 会话缓存：同一 key 的并发请求共享同一个 Promise（请求去重），
 * 完成后按 LRU + 字节上限淘汰。仅适用于按 key 内容不变的资源
 * （如内容文件按 ID 寻址），否则会命中过期内容。
 */
export function createBlobCache(
  loader: (key: string) => Promise<Blob>,
  options: BlobCacheOptions = {},
) {
  const maxEntries = options.maxEntries ?? 30
  const maxTotalBytes = options.maxTotalBytes ?? 40 * 1024 * 1024
  const entries = new Map<string, CacheEntry>()
  let totalBytes = 0

  const evict = () => {
    for (const [key, entry] of entries) {
      if (entries.size <= maxEntries && totalBytes <= maxTotalBytes) return
      entries.delete(key)
      totalBytes -= entry.bytes
    }
  }

  const get = (key: string): Promise<Blob> => {
    const hit = entries.get(key)
    if (hit) {
      entries.delete(key)
      entries.set(key, hit)
      return hit.promise
    }

    const entry: CacheEntry = { promise: undefined as never, bytes: 0 }
    entry.promise = loader(key)
      .then((blob) => {
        // 等待期间可能已被淘汰，只有仍在缓存中才计入字节
        if (entries.get(key) === entry) {
          entry.bytes = blob.size
          totalBytes += blob.size
          evict()
        }
        return blob
      })
      .catch((error) => {
        if (entries.get(key) === entry) {
          entries.delete(key)
        }
        throw error
      })
    entries.set(key, entry)
    evict()
    return entry.promise
  }

  return {
    get,
    clear() {
      entries.clear()
      totalBytes = 0
    },
    get size() {
      return entries.size
    },
  }
}
