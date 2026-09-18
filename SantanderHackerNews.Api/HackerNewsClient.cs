using System.Net;
using System.Net.Http.Json;

namespace SantanderHackerNews.Api;

public interface IHackerNewsClient
{
    Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken);
    Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken cancellationToken);
}

public sealed class HackerNewsClient(HttpClient httpClient) : IHackerNewsClient
{
    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("beststories.json", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<int>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<HackerNewsItem?> GetItemAsync(int id, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"item/{id}.json", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HackerNewsItem>(cancellationToken: cancellationToken);
    }
}
