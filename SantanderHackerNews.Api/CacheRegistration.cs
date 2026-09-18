using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SantanderHackerNews.Api;

public static class CacheRegistration
{
    public static IServiceCollection AddConfiguredCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration.GetValue<string>("Cache:Provider")?.Trim();
        var redisConnection = configuration.GetConnectionString("Redis")?.Trim();

        if (string.IsNullOrWhiteSpace(provider))
            provider = string.IsNullOrWhiteSpace(redisConnection) ? "Memory" : "Redis";

        switch (provider.ToUpperInvariant())
        {
            case "MEMORY":
                services.AddDistributedMemoryCache();
                break;
            case "REDIS":
                if (string.IsNullOrWhiteSpace(redisConnection))
                    throw new InvalidOperationException(
                        "Redis caching is enabled, but ConnectionStrings:Redis is not configured.");
                services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported cache provider '{provider}'. Use 'Memory' or 'Redis'.");
        }

        return services;
    }
}
