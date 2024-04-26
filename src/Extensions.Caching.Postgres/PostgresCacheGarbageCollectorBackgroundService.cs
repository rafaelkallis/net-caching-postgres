using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RafaelKallis.Extensions.Caching.Postgres;

internal sealed class PostgresCacheGarbageCollectorBackgroundService(
    ILogger<PostgresCacheGarbageCollectorBackgroundService> logger,
    IServiceProvider serviceProvider,
    IOptions<PostgresCacheOptions> postgresCacheOptions,
    TimeProvider timeProvider)
    : BackgroundService
{
    private ITimer? _timer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.CompletedTask;
        if (_timer is not null)
        {
            throw new InvalidOperationException("The timer is already initialized.");
        }

        TimeSpan dueTime = TimeSpan.Zero;
        if (postgresCacheOptions.Value.UncorrelateGarbageCollection)
        {
            dueTime = Random.Shared.NextInt64(postgresCacheOptions.Value.GarbageCollectionInterval.ToMilliseconds()).AsMillisecondsTimeSpan();
        }
        TimeSpan period = postgresCacheOptions.Value.GarbageCollectionInterval;
        logger.LogDebug("Initializing garbage collection with {DueTime} {Period}", dueTime, period);
        _timer = timeProvider.CreateTimer(GarbageCollectionTimerCallback, state: stoppingToken, dueTime, period);
        stoppingToken.Register(StopTimer);
    }

    private async void GarbageCollectionTimerCallback(object? state)
    {
        ArgumentNullException.ThrowIfNull(state);
        CancellationToken ct = (CancellationToken)state;
        if (!postgresCacheOptions.Value.EnableGarbageCollection)
        {
            logger.LogDebug("Garbage collection is disabled");
            return;
        }
        logger.LogInformation("Starting garbage collection");
        try
        {
            await using AsyncServiceScope asyncServiceScope = serviceProvider.CreateAsyncScope();
            PostgresCache postgresCache = asyncServiceScope.ServiceProvider.GetRequiredService<PostgresCache>();
            await postgresCache.RunGarbageCollection(ct);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Cancelled");
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while running garbage collection");
            throw;
        }
    }

    private void StopTimer()
    {
        logger.LogDebug("Stopping garbage collection");
        _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public override void Dispose()
    {
        base.Dispose();
        _timer?.Dispose();
        _timer = null;
    }
}