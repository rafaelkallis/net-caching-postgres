using System.Diagnostics;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.Options;

namespace RafaelKallis.Extensions.Caching.Postgres;

/// <summary>
/// Metrics related to the Postgres cache.
/// </summary>
public sealed class PostgresCacheMetrics
{
    /// <summary>
    /// The name of the postgres cache meter.
    /// </summary>
    public const string MeterName = "Caching.Postgres";

    internal const string TypeAttribute = "cache.operation.type";
    internal const string KeyAttribute = "cache.operation.key";

    private readonly bool _includeKeyInTelemetry;

    internal readonly Counter<long> OperationCount;
    internal readonly Histogram<double> OperationDuration;
    internal readonly Histogram<long> OperationIO;

    private long _gets;
    private long _getHits;

    internal readonly Counter<long> GcCount;
    internal readonly Histogram<double> GcDuration;
    internal readonly Histogram<long> GcRemovedEntriesCount;

    /// <inheritdoc cref="PostgresCacheMetrics"/>
    public PostgresCacheMetrics(IMeterFactory meterFactory, IOptions<PostgresCacheOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _includeKeyInTelemetry = options.Value.IncludeKeyInTelemetry;

        using Meter meter = meterFactory.Create(MeterName);

        OperationCount = meter.CreateCounter<long>("cache.operation.count",
            unit: "{operation}",
            description: "The number of cache operations.");

        OperationDuration = meter.CreateHistogram<double>("cache.operation.duration",
            unit: "s",
            description: "The duration of cache operations.");

        OperationIO = meter.CreateHistogram<long>("cache.operation.io",
            unit: "By",
            description: "The amount of bytes read and written during cache operations.");

        meter.CreateObservableGauge("cache.hit_ratio",
            description: "The hit ratio of the cache.",
            observeValue: () => Convert.ToDouble(Interlocked.Read(ref _getHits)) / Convert.ToDouble(Interlocked.Read(ref _gets)));

        GcCount = meter.CreateCounter<long>("cache.gc.count",
            unit: "{run}",
            description: "The number of garbage collections.");

        GcDuration = meter.CreateHistogram<double>("cache.gc.duration",
            unit: "s",
            description: "The duration of garbage collections.");

        GcRemovedEntriesCount = meter.CreateHistogram<long>("cache.gc.removed_entries",
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
        OperationCount.Add(1, tags);
        OperationDuration.Record(duration.TotalSeconds, tags);
        OperationIO.Record(bytesRead, tags);
    }

    internal void Set(string key, TimeSpan duration, long bytesWritten)
    {
        TagList tags = new()
        {
            {TypeAttribute, "set"},
        };
        AddKey(tags, key);
        OperationCount.Add(1, tags);
        OperationDuration.Record(duration.TotalSeconds, tags);
        OperationIO.Record(bytesWritten, tags);
    }

    internal void Refresh(string key, TimeSpan duration)
    {
        TagList tags = new()
        {
            {TypeAttribute, "refresh"},
        };
        AddKey(tags, key);
        OperationCount.Add(1, tags);
        OperationDuration.Record(duration.TotalSeconds, tags);
    }

    internal void Remove(string key, TimeSpan duration)
    {
        TagList tags = new()
        {
            {TypeAttribute, "remove"},
        };
        AddKey(tags, key);
        OperationCount.Add(1, tags);
        OperationDuration.Record(duration.TotalSeconds, tags);
    }

    internal void GarbageCollection(TimeSpan duration, long removedEntriesCount)
    {
        GcCount.Add(1);
        GcDuration.Record(duration.TotalSeconds);
        GcRemovedEntriesCount.Record(removedEntriesCount);
    }

    private void AddKey(TagList tags, string key)
    {
        if (_includeKeyInTelemetry)
        {
            tags.Add(KeyAttribute, key);
        }
    }
}