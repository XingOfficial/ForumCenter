using System.Collections.Concurrent;

namespace ForumCenter.Utilities;

/// <summary>
/// 简单内存缓存 - 移植自 Kotlin ApiService.kt 的缓存逻辑
/// 用于第一页帖子列表的快速显示（1分钟 TTL）
/// </summary>
public static class MemoryCache
{
    private static readonly ConcurrentDictionary<string, string> _cache = new();
    private static readonly ConcurrentDictionary<string, long> _timestamps = new();
    private const long CacheTtlMs = 60_000; // 1分钟

    /// <summary>读取缓存（过期返回 null）</summary>
    public static string? Get(string key)
    {
        if (!_timestamps.TryGetValue(key, out var ts))
            return null;

        if (Environment.TickCount64 - ts > CacheTtlMs)
        {
            _cache.TryRemove(key, out _);
            _timestamps.TryRemove(key, out _);
            return null;
        }

        return _cache.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>写入缓存</summary>
    public static void Put(string key, string value)
    {
        _cache[key] = value;
        _timestamps[key] = Environment.TickCount64;
    }

    /// <summary>清除所有缓存</summary>
    public static void Clear()
    {
        _cache.Clear();
        _timestamps.Clear();
    }
}
