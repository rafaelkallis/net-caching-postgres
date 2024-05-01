using System.Diagnostics.Tracing;

namespace RafaelKallis.Extensions.Caching.Postgres;

internal class PostgresCacheEventSource : EventSource
{
    public const string EventSourceName = "RafaelKallis.Extensions.Caching.Postgres";

    const int CacheGetEventId = 1;
    const int CacheSetEventId = 2;
    const int CacheRefreshEventId = 3;
    const int CacheRemoveEventId = 4;
    const int CacheGarbageCollectionEventId = 5;


    private long _cacheGetCount;
    private long _cacheGetHitCount;
    private IncrementingPollingCounter? _cacheGetCounter;
    private PollingCounter? _cacheGetHitRatioCounter;
    private EventCounter? _cacheGetDurationCounter;
    private IncrementingEventCounter? _cacheBytesReadCounter;

    private IncrementingEventCounter? _cacheSetCounter;
    private EventCounter? _cacheSetDurationCounter;
    private IncrementingEventCounter? _cacheBytesWrittenCounter;

    private IncrementingEventCounter? _cacheRefreshCounter;
    private EventCounter? _cacheRefreshDurationCounter;

    private IncrementingEventCounter? _cacheRemoveCounter;
    private EventCounter? _cacheRemoveDurationCounter;

    private IncrementingEventCounter? _cacheGarbageCollectionsCounter;
    private EventCounter? _cacheGarbageCollectionDurationCounter;
    private EventCounter? _cacheGarbageCollectionRemovedEntriesCounter;

    public static readonly PostgresCacheEventSource Log = new();

    private PostgresCacheEventSource() : base(EventSourceName)
    { }

    [Event(CacheGetEventId, Level = EventLevel.Informational)]
    public void CacheGet(string key, TimeSpan duration, bool hit, long bytesRead)
    {
        if (IsEnabled())
        {
            Interlocked.Increment(ref _cacheGetCount);
            if (hit)
            {
                Interlocked.Increment(ref _cacheGetHitCount);
            }
            _cacheGetDurationCounter?.WriteMetric(duration.TotalMilliseconds);
            _cacheBytesReadCounter?.Increment(Convert.ToDouble(bytesRead));
            WriteEvent(CacheGetEventId, key, hit, bytesRead);
        }
    }

    [Event(CacheSetEventId, Level = EventLevel.Informational)]
    public void CacheSet(string key, TimeSpan duration, long bytesWritten)
    {
        if (IsEnabled())
        {
            _cacheSetCounter?.Increment();
            _cacheBytesWrittenCounter?.Increment(Convert.ToDouble(bytesWritten));
            _cacheSetDurationCounter?.WriteMetric(duration.TotalMilliseconds);
            WriteEvent(CacheSetEventId, key, bytesWritten);
        }
    }

    [Event(CacheRefreshEventId, Level = EventLevel.Informational)]
    public void CacheRefresh(string key, TimeSpan duration)
    {
        if (IsEnabled())
        {
            _cacheRefreshCounter?.Increment();
            _cacheRemoveDurationCounter?.WriteMetric(duration.TotalMilliseconds);
            WriteEvent(CacheRefreshEventId, key);
        }
    }

    [Event(CacheRemoveEventId, Level = EventLevel.Informational)]
    public void CacheRemove(string key, TimeSpan duration)
    {
        if (IsEnabled())
        {
            _cacheRemoveCounter?.Increment();
            _cacheRemoveDurationCounter?.WriteMetric(duration.TotalMilliseconds);
            WriteEvent(CacheRemoveEventId, key);
        }
    }

    [Event(CacheGarbageCollectionEventId, Level = EventLevel.Informational)]
    public void CacheGarbageCollection(TimeSpan duration, long removedEntriesCount)
    {
        if (IsEnabled())
        {
            _cacheGarbageCollectionsCounter?.Increment();
            _cacheGarbageCollectionDurationCounter?.WriteMetric(duration.TotalMilliseconds);
            _cacheGarbageCollectionRemovedEntriesCounter?.WriteMetric(Convert.ToDouble(removedEntriesCount));
            WriteEvent(CacheGarbageCollectionEventId, duration.TotalMilliseconds, removedEntriesCount);
        }
    }

    protected override void OnEventCommand(EventCommandEventArgs command)
    {
        base.OnEventCommand(command);
        if (command.Command == EventCommand.Enable)
        {
            _cacheGetCounter ??= new("gets-per-second",
                this,
                () => Interlocked.Read(ref _cacheGetCount))
            {
                DisplayName = "Gets",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _cacheGetHitRatioCounter ??= new(
                "gets-hit-ratio",
                this,
                () => Convert.ToDouble(Interlocked.Read(ref _cacheGetHitCount)) / Convert.ToDouble(Interlocked.Read(ref _cacheGetCount)) * 100)
            {
                DisplayName = "Gets Hit Ratio",
                DisplayUnits = "%"
            };

            _cacheGetDurationCounter ??= new("get-duration", this)
            {
                DisplayName = "Get Duration",
                DisplayUnits = "ms",
            };

            _cacheBytesReadCounter ??= new("bytes-read-per-second", this)
            {
                DisplayName = "Bytes Read Rate",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _cacheSetCounter ??= new("sets-per-second", this)
            {
                DisplayName = "Sets",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _cacheSetDurationCounter ??= new("set-duration", this)
            {
                DisplayName = "Set Duration",
                DisplayUnits = "ms",
            };

            _cacheBytesWrittenCounter ??= new("bytes-written-per-second", this)
            {
                DisplayName = "Bytes Written",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _cacheRefreshCounter ??= new("refreshes-per-second", this)
            {
                DisplayName = "Refreshes",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _cacheRefreshDurationCounter ??= new("refresh-duration", this)
            {
                DisplayName = "Refresh Duration",
                DisplayUnits = "ms",
            };

            _cacheRemoveCounter ??= new("removes-per-second", this)
            {
                DisplayName = "Removes",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _cacheRemoveDurationCounter ??= new("remove-duration", this)
            {
                DisplayName = "Remove Duration",
                DisplayUnits = "ms",
            };

            _cacheGarbageCollectionsCounter ??= new("garbage-collections", this)
            {
                DisplayName = "Garbage Collections",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1),
            };

            _cacheGarbageCollectionDurationCounter ??= new("garbage-collection-duration", this)
            {
                DisplayName = "Garbage Collection Duration",
                DisplayUnits = "ms",
            };

            _cacheGarbageCollectionRemovedEntriesCounter ??= new("garbage-collection-removed-entries", this)
            {
                DisplayName = "Garbage Collection Removed Entries",
                DisplayUnits = "cache entries",
            };
        }
    }

    protected override void Dispose(bool disposing)
    {
        _cacheGetCounter?.Dispose();
        _cacheGetCounter = null;
        _cacheGetHitRatioCounter?.Dispose();
        _cacheGetHitRatioCounter = null;
        _cacheGetDurationCounter?.Dispose();
        _cacheGetDurationCounter = null;
        _cacheBytesReadCounter?.Dispose();
        _cacheBytesReadCounter = null;

        _cacheSetCounter?.Dispose();
        _cacheSetCounter = null;
        _cacheSetDurationCounter?.Dispose();
        _cacheSetDurationCounter = null;
        _cacheBytesWrittenCounter?.Dispose();
        _cacheBytesWrittenCounter = null;

        _cacheRefreshCounter?.Dispose();
        _cacheRefreshCounter = null;
        _cacheRefreshDurationCounter?.Dispose();
        _cacheRefreshDurationCounter = null;

        _cacheRemoveCounter?.Dispose();
        _cacheRemoveCounter = null;
        _cacheRemoveDurationCounter?.Dispose();
        _cacheRemoveDurationCounter = null;

        _cacheGarbageCollectionsCounter?.Dispose();
        _cacheGarbageCollectionsCounter = null;
        _cacheGarbageCollectionDurationCounter?.Dispose();
        _cacheGarbageCollectionDurationCounter = null;
        _cacheGarbageCollectionRemovedEntriesCounter?.Dispose();
        _cacheGarbageCollectionRemovedEntriesCounter = null;

        base.Dispose(disposing);
    }
}