using System.Net;
using System.Net.Http.Json;
using SantanderHackerNews.Api;

namespace SantanderHackerNews.Tests;

public sealed class HackerNewsClientTests
{
    [Fact]
    public async Task Reads_best_story_ids_without_external_service()
    {
        var handler = new StubHandler();
        var client = new HackerNewsClient(new HttpClient(handler) { BaseAddress = new Uri("https://test/") });
        var ids = await client.GetBestStoryIdsAsync(CancellationToken.None);
        Assert.Equal([42, 43], ids);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { 42, 43 }) });
    }
}
