using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SantanderHackerNews.Api;

namespace SantanderHackerNews.Tests;

public sealed class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient client;
    public ApiIntegrationTests(ApiFactory factory) => client = factory.CreateClient();

    [Fact]
    public async Task Best_stories_endpoint_returns_json()
    {
        var response = await client.GetAsync("/api/stories/best");
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task Liveness_endpoint_is_not_rate_limited()
    {
        var response = await client.GetAsync("/health/live");

        response.EnsureSuccessStatusCode();
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Best_stories_endpoint_rejects_an_invalid_count()
    {
        var response = await client.GetAsync("/api/stories/best?n=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            services.AddDistributedMemoryCache();
            services.RemoveAll<IHackerNewsClient>();
            services.AddSingleton<IHackerNewsClient, FakeHackerNewsClient>();
        });

    private sealed class FakeHackerNewsClient : IHackerNewsClient
    {
        public Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken _) => Task.FromResult<IReadOnlyList<int>>([1]);
        public Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken _) =>
            Task.FromResult<HackerNewsItem?>(new(id, "Integration story", "tester", 100, 1700000000, null, 2));
    }
}
