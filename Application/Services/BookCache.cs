using Application.Interfaces;
using Domain.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Application.Services;

// Инвалидация: версия входит в каждый ключ. Invalidate() увеличивает версию,
// старые записи перестают находиться и удаляются по абсолютному TTL.
public class BookCache(IMemoryCache cache, ILogger<BookCache> logger) : IBookCache
{
    private static readonly TimeSpan AbsoluteTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SlidingTtl = TimeSpan.FromMinutes(1);

    private int _version;

    public string BookKey(int id) => $"books:v{Volatile.Read(ref _version)}:id:{id}";

    public string ListKey(BookFilter f) =>
        $"books:v{Volatile.Read(ref _version)}:list:{f.Search?.ToLowerInvariant()}|{f.OnlyAvailable}|{f.MinYear}|{f.MaxYear}|{f.SortBy}|{f.Desc}|{f.Page}|{f.PageSize}";

    public bool TryGet<T>(string key, out T? value)
    {
        if (cache.TryGetValue(key, out var cached) && cached is T typed)
        {
            logger.LogInformation("Cache HIT key={Key}", key);
            value = typed;
            return true;
        }

        logger.LogInformation("Cache MISS key={Key}", key);
        value = default;
        return false;
    }

    public void Set<T>(string key, T value)
    {
        cache.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = AbsoluteTtl,
            SlidingExpiration = SlidingTtl
        });
    }

    public void Invalidate() => Interlocked.Increment(ref _version);
}
