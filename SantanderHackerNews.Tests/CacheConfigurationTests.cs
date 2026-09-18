using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SantanderHackerNews.Api;

namespace SantanderHackerNews.Tests;

public sealed class CacheConfigurationTests
{
    [Fact]
    public async Task Defaults_to_local_cache_and_stores_values()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddConfiguredCache(configuration);

        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IDistributedCache>();
        await cache.SetStringAsync("key", "value");

        Assert.Equal("value", await cache.GetStringAsync("key"));
        Assert.Contains("Memory", cache.GetType().Name);
    }

    [Fact]
    public void Redis_requires_a_connection_string()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:Provider"] = "Redis"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddConfiguredCache(configuration));

        Assert.Contains("ConnectionStrings:Redis", exception.Message);
    }

    [Fact]
    public void Explicit_redis_provider_registers_distributed_redis_cache()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cache:Provider"] = "Redis",
                ["ConnectionStrings:Redis"] = "localhost:6379"
            })
            .Build();

        var services = new ServiceCollection().AddConfiguredCache(configuration);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IDistributedCache) &&
            descriptor.ImplementationType?.Name.Contains("Redis", StringComparison.OrdinalIgnoreCase) == true);
    }
}
