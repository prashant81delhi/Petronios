using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SantanderHackerNews.Api;
using Polly;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddOptions<HackerNewsOptions>().Bind(builder.Configuration.GetSection("HackerNews")).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddTransient<OutboundBulkheadHandler>();
builder.Services.AddTransient<OutboundRateLimitHandler>();
builder.Services.AddHttpClient<IHackerNewsClient, HackerNewsClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HackerNewsOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SantanderHackerNews/1.0");
}).AddHttpMessageHandler<OutboundRateLimitHandler>().AddHttpMessageHandler<OutboundBulkheadHandler>().AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.FailureRatio = 0.5;
});
builder.Services.AddConfiguredCache(builder.Configuration);
builder.Services.AddSingleton<BestStoriesService>();
builder.Services.AddHostedService<CacheRefreshService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("inbound", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    var requestId = context.Request.Headers["X-Request-Id"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(requestId))
        requestId = context.TraceIdentifier;

    context.Response.Headers["X-Request-Id"] = requestId;
    using (app.Logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = requestId }))
    {
        await next();
    }
});
app.UseRateLimiter();
app.UseSwagger();
app.UseSwaggerUI();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready");
app.MapControllers().RequireRateLimiting("inbound");
app.Run();

public partial class Program { }
