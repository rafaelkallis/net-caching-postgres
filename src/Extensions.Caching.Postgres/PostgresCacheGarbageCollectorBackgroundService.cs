using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RafaelKallis.Extensions.Caching.Postgres;

internal sealed class PostgresCacheGarbageCollectorBackgroundService : BackgroundService
{
    private ITimer? _timer;
    private readonly ILogger<PostgresCacheGarbageCollectorBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<PostgresCacheOptions> _postgresCacheOptions;
    private readonly TimeProvider _timeProvider;

    public PostgresCacheGarbageCollectorBackgroundService(ILogger<PostgresCacheGarbageCollectorBackgroundService> logger,
        IServiceProvider serviceProvider,
        IOptions<PostgresCacheOptions> postgresCacheOptions,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _postgresCacheOptions = postgresCacheOptions;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.CompletedTask;
        if (_timer is not null)
        {
            throw new InvalidOperationException("The timer is already initialized.");
        }

        TimeSpan dueTime = TimeSpan.Zero;
        if (_postgresCacheOptions.Value.UncorrelateGarbageCollection)
        {
            dueTime = Random.Shared.NextInt64(_postgresCacheOptions.Value.GarbageCollectionInterval.ToMilliseconds()).AsMillisecondsTimeSpan();
        }
        TimeSpan period = _postgresCacheOptions.Value.GarbageCollectionInterval;
        _logger.LogDebug("Initializing garbage collection with {DueTime} {Period}", dueTime, period);
        _timer = _timeProvider.CreateTimer(GarbageCollectionTimerCallback, state: stoppingToken, dueTime, period);
        stoppingToken.Register(StopTimer);
    }

    private async void GarbageCollectionTimerCallback(object? state)
    {
        ArgumentNullException.ThrowIfNull(state);
        CancellationToken ct = (CancellationToken)state;
        if (!_postgresCacheOptions.Value.EnableGarbageCollection)
        {
            _logger.LogDebug("Garbage collection is disabled");
            return;
        }
        _logger.LogInformation("Starting garbage collection");
        try
        {
            await using AsyncServiceScope asyncServiceScope = _serviceProvider.CreateAsyncScope();
            PostgresCache postgresCache = asyncServiceScope.ServiceProvider.GetRequiredService<PostgresCache>();
            await postgresCache.RunGarbageCollection(ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Cancelled");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while running garbage collection");
            throw;
        }
    }

    private void StopTimer()
    {
        _logger.LogDebug("Stopping garbage collection");
        _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public override void Dispose()
    {
        base.Dispose();
        _timer?.Dispose();
        _timer = null;
    }
}