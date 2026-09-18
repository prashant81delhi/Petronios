using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace SantanderHackerNews.Api;

public sealed class BestStoriesService(
    IHackerNewsClient client,
    IDistributedCache cache,
    IOptions<HackerNewsOptions> options,
    ILogger<BestStoriesService> logger)
{
    private const string CacheKeyPrefix = "hn:best-stories:v2:";
    private readonly SemaphoreSlim refreshLock = new(1, 1);

    public Task<IReadOnlyList<StoryDto>> GetAsync(bool forceRefresh, CancellationToken cancellationToken) =>
        GetAsync(options.Value.StoryCount, forceRefresh, cancellationToken);

    public async Task<IReadOnlyList<StoryDto>> GetAsync(int count, bool forceRefresh, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeyPrefix + count;
        var cached = await cache.GetStringAsync(cacheKey, cancellationToken);
        var stale = cached;
        if (!forceRefresh && cached is not null)
            return JsonSerializer.Deserialize<List<StoryDto>>(cached) ?? [];
        if (!await refreshLock.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken))
            return stale is not null ? JsonSerializer.Deserialize<List<StoryDto>>(stale) ?? [] : [];
        try
        {
            cached = await cache.GetStringAsync(cacheKey, cancellationToken);
            if (!forceRefresh && cached is not null) return JsonSerializer.Deserialize<List<StoryDto>>(cached) ?? [];
            IReadOnlyList<int> ids;
            try { ids = await client.GetBestStoryIdsAsync(cancellationToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Hacker News unavailable; serving stale cache");
                return stale is not null ? JsonSerializer.Deserialize<List<StoryDto>>(stale) ?? [] : [];
            }
            var result = new ConcurrentBag<StoryDto>();
            using var gate = new SemaphoreSlim(options.Value.MaxConcurrentItemRequests);
            var tasks = ids.Select(async id =>
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    var item = await client.GetItemAsync(id, cancellationToken);
                    if (item?.Title is not null)
                        result.Add(new StoryDto(item.Title, item.Url, item.By,
                            item.Time is null ? null : DateTimeOffset.FromUnixTimeSeconds(item.Time.Value),
                            item.Score ?? 0, item.Descendants ?? 0));
                }
                catch (Exception ex) { logger.LogWarning(ex, "Unable to load Hacker News item {Id}", id); }
                finally { gate.Release(); }
            });
            await Task.WhenAll(tasks);
            var stories = result.OrderByDescending(x => x.Score).Take(count).ToList();
            var serialized = JsonSerializer.Serialize(stories);
            await cache.SetStringAsync(cacheKey, serialized, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(options.Value.CacheSeconds)
            }, cancellationToken);
            return stories;
        }
        finally { refreshLock.Release(); }
    }

    public Task RefreshAsync(CancellationToken cancellationToken) => GetAsync(true, cancellationToken);
}
