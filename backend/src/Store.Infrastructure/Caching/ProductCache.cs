using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Store.Application.Abstractions;

namespace Store.Infrastructure.Caching;

/// <summary>
/// Caches product list pages. All entries share one CancellationTokenSource, so
/// Invalidate() evicts every page/filter combination at once.
/// </summary>
public sealed class ProductCache : IProductCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;
    private readonly ILogger<ProductCache> _logger;
    private CancellationTokenSource _reset = new();

    public ProductCache(IMemoryCache cache, ILogger<ProductCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        // Take the token before loading: if an invalidation happens while we query,
        // the entry is stored with an already-cancelled token and dropped straight away.
        var token = Volatile.Read(ref _reset).Token;

        var value = await factory(cancellationToken);

        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(Lifetime)
            .AddExpirationToken(new CancellationChangeToken(token));
        _cache.Set(key, value, options);

        return value;
    }

    public void Invalidate()
    {
        // Not disposed on purpose: a concurrent reader may still be grabbing its Token.
        var old = Interlocked.Exchange(ref _reset, new CancellationTokenSource());
        old.Cancel();
        _logger.LogDebug("Product list cache invalidated");
    }
}
