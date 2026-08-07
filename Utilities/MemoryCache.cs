using System.Collections.Concurrent;

namespace ForumCenter.Utilities;





public static class MemoryCache
{
    private static readonly ConcurrentDictionary<string, string> _cache = new();
    private static readonly ConcurrentDictionary<string, long> _timestamps = new();
    private const long CacheTtlMs = 60_000; 

    
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

    
    public static void Put(string key, string value)
    {
        _cache[key] = value;
        _timestamps[key] = Environment.TickCount64;
    }

    
    public static void Clear()
    {
        _cache.Clear();
        _timestamps.Clear();
    }
}
