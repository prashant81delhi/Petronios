using Microsoft.Extensions.Options;

namespace SantanderHackerNews.Api;

public sealed class CacheRefreshService(IServiceScopeFactory scopeFactory, IOptions<HackerNewsOptions> options, ILogger<CacheRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, options.Value.RefreshIntervalSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { using var scope = scopeFactory.CreateScope(); await scope.ServiceProvider.GetRequiredService<BestStoriesService>().RefreshAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogWarning(ex, "Background Hacker News refresh failed"); }
        }
    }
}
