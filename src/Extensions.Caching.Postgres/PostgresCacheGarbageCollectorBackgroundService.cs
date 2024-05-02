using System.Runtime.CompilerServices;

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

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_timer is not null)
        {
            throw new InvalidOperationException("The timer is already initialized.");
        }

        TimeSpan dueTime = TimeSpan.Zero;
        if (postgresCacheOptions.Value.UncorrelateGarbageCollection)
        {
#pragma warning disable CA5394 // Do not use insecure randomness
            dueTime = Random.Shared.NextInt64(postgresCacheOptions.Value.GarbageCollectionInterval.ToMilliseconds()).AsMillisecondsTimeSpan();
#pragma warning restore CA5394 // Do not use insecure randomness
        }
        TimeSpan period = postgresCacheOptions.Value.GarbageCollectionInterval;
        logger.LogDebug("Initializing garbage collection with {DueTime} {Period}", dueTime, period);
        _timer = timeProvider.CreateTimer(GarbageCollectionTimerCallback, state: stoppingToken, dueTime, period);
        stoppingToken.Register(StopTimer);
        return Task.CompletedTask;
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
            AsyncServiceScope asyncServiceScope = serviceProvider.CreateAsyncScope();
            await using ConfiguredAsyncDisposable _ = asyncServiceScope.ConfigureAwait(false);
            PostgresCache postgresCache = asyncServiceScope.ServiceProvider.GetRequiredService<PostgresCache>();
            await postgresCache.RunGarbageCollection(ct).ConfigureAwait(false);
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