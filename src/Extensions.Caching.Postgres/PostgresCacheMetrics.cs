using System.Diagnostics;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.Options;

namespace RafaelKallis.Extensions.Caching.Postgres;

/// <summary>
/// Metrics related to the Posgres cache.
/// </summary>
public sealed class PostgresCacheMetrics
{
    /// <summary>
    /// The name of the postgres cache meter.
    /// </summary>
    public const string MeterName = "Caching.Postgres";

    /// <summary>
    /// The telemetry attribute of the operation type.
    /// </summary>
    public const string TypeAttribute = "cache.operation.type";

    /// <summary>
    /// The telemetry attribute for the cache key.
    /// </summary>
    public const string KeyAttribute = "cache.operation.key";

    private readonly bool _includeKeyInTelemetry;

    private readonly Counter<long> _operationCount;
    private readonly Histogram<double> _operationDuration;
    private readonly Histogram<long> _operationIO;

    private long _gets;
    private long _getHits;

    private readonly Counter<long> _gcCount;
    private readonly Histogram<double> _gcDuration;
    private readonly Histogram<long> _gcRemovedEntriesCount;

    /// <inheritdoc cref="PostgresCacheMetrics" />
    public PostgresCacheMetrics(IMeterFactory meterFactory, IOptions<PostgresCacheOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _includeKeyInTelemetry = options.Value.IncludeKeyInTelemetry;

        // https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation#best-practices-1
#pragma warning disable CA2000 // Dispose objects before losing scope
        Meter meter = meterFactory.Create(MeterName);
#pragma warning restore CA2000 // Dispose objects before losing scope

        _operationCount = meter.CreateCounter<long>("cache.operation.count",
            unit: "{operation}",
            description: "The number of cache operations.");

        _operationDuration = meter.CreateHistogram<double>("cache.operation.duration",
            unit: "ms",
            description: "The duration of cache operations.");

        _operationIO = meter.CreateHistogram<long>("cache.operation.io",
            unit: "By",
            description: "The amount of bytes read and written during cache operations.");

        meter.CreateObservableGauge("cache.hit_ratio",
            description: "The hit ratio of the cache.",
            observeValue: () => Convert.ToDouble(Interlocked.Read(ref _getHits)) / Convert.ToDouble(Interlocked.Read(ref _gets)));

        _gcCount = meter.CreateCounter<long>("cache.gc.count",
            unit: "{run}",
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
            {TypeAttribute, "get"},
        };
        AddKey(tags, key);
        _operationCount.Add(1, tags);
        _operationDuration.Record(duration.TotalMilliseconds, tags);
        _operationIO.Record(bytesRead, tags);
    }

    internal void Set(string key, TimeSpan duration, long bytesWritten)
    {
        TagList tags = new()
        {
            {TypeAttribute, "set"},
        };
        AddKey(tags, key);
        _operationCount.Add(1, tags);
        _operationDuration.Record(duration.TotalMilliseconds, tags);
        _operationIO.Record(bytesWritten, tags);
    }

    internal void Refresh(string key, TimeSpan duration)
    {
        TagList tags = new()
        {
            {TypeAttribute, "refresh"},
        };
        AddKey(tags, key);
        _operationCount.Add(1, tags);
        _operationDuration.Record(duration.TotalMilliseconds, tags);
    }

    internal void Remove(string key, TimeSpan duration)
    {
        TagList tags = new()
        {
            {TypeAttribute, "remove"},
        };
        AddKey(tags, key);
        _operationCount.Add(1, tags);
        _operationDuration.Record(duration.TotalMilliseconds, tags);
    }

    internal void GarbageCollection(TimeSpan duration, long removedEntriesCount)
    {
        _gcCount.Add(1);
        _gcDuration.Record(duration.TotalMilliseconds);
        _gcRemovedEntriesCount.Record(removedEntriesCount);
    }

    private void AddKey(TagList tags, string key)
    {
        if (_includeKeyInTelemetry)
        {
            tags.Add(KeyAttribute, key);
        }
    }
}