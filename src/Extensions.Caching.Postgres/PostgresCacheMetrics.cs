using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RafaelKallis.Extensions.Caching.Postgres;

/// <summary>
/// Metrics related to the Posgres cache.
/// </summary>
public sealed class PostgresCacheMetrics
{
    private readonly Counter<long> _operationCount;
    private readonly Histogram<double> _operationDuration;
    private readonly Histogram<long> _operationValueSize;

    private long _gets;
    private long _getHits;
    private readonly ObservableGauge<double> _hitRatio;

    private readonly Counter<long> _gcCount;
    private readonly Histogram<double> _gcDuration;
    private readonly Histogram<long> _gcRemovedEntriesCount;

    /// <inheritdoc cref="PostgresCacheMetrics" />
    public PostgresCacheMetrics(IMeterFactory meterFactory)
    {
        // https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation#best-practices-1
#pragma warning disable CA2000 // Dispose objects before losing scope
        Meter meter = meterFactory.Create("Caching.Postgres");
#pragma warning restore CA2000 // Dispose objects before losing scope

        _operationCount = meter.CreateCounter<long>("cache.operation.count",
            unit: "{operation}",
            description: "The number of cache operations.");

        _operationDuration = meter.CreateHistogram<double>("cache.operation.duration",
            unit: "ms",
            description: "The duration of cache operations.");

        _operationValueSize = meter.CreateHistogram<long>("cache.operation.value.size",
            unit: "By",
            description: "The size of the payload during a operation.");

        _hitRatio = meter.CreateObservableGauge("cache.hit_ratio",
            description: "The ratio of cache operation hits.",
            unit: "%",
            observeValue: () => Convert.ToDouble(Interlocked.Read(ref _getHits)) / Convert.ToDouble(Interlocked.Read(ref _gets)) * 100);

        _gcCount = meter.CreateCounter<long>("cache.gc.count",
            unit: "{garbage collection}",
            description: "The number of garbage collections.");

        _gcDuration = meter.CreateHistogram<double>("cache.gc.duration",
            unit: "ms",
            description: "The duration of garbage collections.");

        _gcRemovedEntriesCount = meter.CreateHistogram<long>("cache.gc.removed_entries",
            unit: "{entry}",
            description: "The number of entries that were removed during garbage collection, because they expired.");
    }

    internal void Get(string key, TimeSpan duration, bool hit, long bytesRead)
    {
        Interlocked.Increment(ref _gets);
        if (hit)
        {
            Interlocked.Increment(ref _getHits);
        }
        TagList tags = new()
        {
            {"cache.operation.type", "get"},
            {"cache.operation.key", key},
            {"cache.operation.hit", hit},
        };
        _operationCount.Add(1, tags);
        _operationDuration.Record(duration.TotalMicroseconds, tags);
        _operationValueSize.Record(bytesRead, tags);
    }

    internal void Set(string key, TimeSpan duration, long bytesWritten)
    {
        TagList tags = new()
        {
            {"cache.operation.type", "set"},
            {"cache.operation.key", key},
        };
        _operationCount.Add(1, tags);
        _operationDuration.Record(duration.TotalMicroseconds, tags);
        _operationValueSize.Record(bytesWritten, tags);
    }

    internal void Refresh(string key, TimeSpan duration)
    {
        TagList tags = new()
        {
            {"cache.operation.type", "refresh"},
            {"cache.operation.key", key},
        };
        _operationCount.Add(1, tags);
        _operationDuration.Record(duration.TotalMicroseconds, tags);
    }

    internal void Remove(string key, TimeSpan duration)
    {
        TagList tags = new()
        {
            {"cache.operation.type", "remove"},
            {"cache.operation.key", key},
        };
        _operationCount.Add(1, tags);
        _operationDuration.Record(duration.TotalMicroseconds, tags);
    }

    internal void GarbageCollection(TimeSpan duration, long removedEntriesCount)
    {
        _gcCount.Add(1);
        _gcDuration.Record(duration.TotalMilliseconds);
        _gcRemovedEntriesCount.Record(removedEntriesCount);
    }
}