using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SantanderHackerNews.Api;

public sealed class HackerNewsOptions
{
    [Required, Url] public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0/";
    [Range(1, 100)] public int StoryCount { get; set; } = 20;
    [Range(1, 3600)] public int CacheSeconds { get; set; } = 60;
    [Range(1, 3600)] public int StaleCacheSeconds { get; set; } = 300;
    [Range(1, 120)] public int TimeoutSeconds { get; set; } = 10;
    [Range(1, 100)] public int MaxConcurrentItemRequests { get; set; } = 8;
    public int RefreshIntervalSeconds { get; set; } = 30;
}

public sealed record HackerNewsItem(
    int Id, string? Title, string? By, int? Score, int? Time, string? Url,
    [property: JsonPropertyName("descendants")] int? Descendants);

public sealed record StoryDto(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("uri")] string? Uri,
    [property: JsonPropertyName("postedBy")] string? PostedBy,
    [property: JsonPropertyName("time")] DateTimeOffset? Time,
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("commentCount")] int CommentCount);
