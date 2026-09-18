using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using SantanderHackerNews.Api;

namespace SantanderHackerNews.Tests;

public sealed class BestStoriesServiceTests
{
    [Fact]
    public async Task Fetches_and_caches_stories()
    {
        var client = new FakeClient();
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var service = new BestStoriesService(client, cache, Options.Create(new HackerNewsOptions { StoryCount = 2 }), NullLogger<BestStoriesService>.Instance);
        var first = await service.GetAsync(false, CancellationToken.None);
        var second = await service.GetAsync(false, CancellationToken.None);
        Assert.Equal(2, first.Count);
        Assert.Equal(first, second);
        Assert.Equal(1, client.IdCalls);
    }

    private sealed class FakeClient : IHackerNewsClient
    {
        public int IdCalls { get; private set; }
        public Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken _) { IdCalls++; return Task.FromResult<IReadOnlyList<int>>([1, 2]); }
        public Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken _) =>
            Task.FromResult<HackerNewsItem?>(new HackerNewsItem(id, $"Story {id}", "author", id * 10, 1700000000, null, 1));
    }
}
