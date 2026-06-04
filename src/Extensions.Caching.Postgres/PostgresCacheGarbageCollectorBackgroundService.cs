using System.Diagnostics;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RafaelKallis.Extensions.Caching.Postgres;

internal sealed partial class PostgresCacheGarbageCollectorBackgroundService(
    ILogger<PostgresCacheGarbageCollectorBackgroundService> logger,
    IServiceProvider serviceProvider,
    IOptions<PostgresCacheOptions> postgresCacheOptions)
    : BackgroundService
{
    private ITimer? _timer;
    private PostgresCacheOptions Options => postgresCacheOptions.Value;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_timer is not null)
        {
            throw new InvalidOperationException("The timer is already initialized.");
        }

        TimeSpan dueTime = TimeSpan.Zero;
        if (Options.UncorrelateGarbageCollection)
        {
#pragma warning disable CA5394 // Do not use insecure randomness
            dueTime = Random.Shared.NextInt64(Options.GarbageCollectionInterval.ToMilliseconds()).AsMillisecondsTimeSpan();
#pragma warning restore CA5394 // Do not use insecure randomness
        }
        TimeSpan period = Options.GarbageCollectionInterval;
        LogStartingGarbageCollectionTimer(dueTime, period);
        _timer = Options.TimeProvider.CreateTimer(_ => GarbageCollectionTimerCallback(stoppingToken), state: null, dueTime, period);
        stoppingToken.Register(StopTimer);
        return Task.CompletedTask;
    }

    private async void GarbageCollectionTimerCallback(CancellationToken ct)
    {
        if (!Options.EnableGarbageCollection)
        {
            logger.LogDebug("Garbage collection is disabled");
            return;
        }
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
        logger.LogDebug("Stopping garbage collection timer");
        _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public override void Dispose()
    {
        base.Dispose();
        _timer?.Dispose();
        _timer = null;
    }

    [LoggerMessage(LogLevel.Debug, "Starting garbage collection timer: dueTime {DueTime}, period {Period}")]
    private partial void LogStartingGarbageCollectionTimer(TimeSpan dueTime, TimeSpan period);
}