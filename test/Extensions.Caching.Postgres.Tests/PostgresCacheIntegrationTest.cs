using System.Security.Cryptography;

using Microsoft.Extensions.Diagnostics.Metrics.Testing;

using Xunit.Sdk;

using DistributedCacheEntryOptions = Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions;

namespace RafaelKallis.Extensions.Caching.Postgres.Tests;

public sealed class PostgresCacheIntegrationTest(ITestOutputHelper output, PostgresFixture postgresFixture) : IntegrationTest(output, postgresFixture), IDisposable
{
    private const int ValueSize = 1024;

    private MetricCollector<long> _operationCountCollector = null!;
    private MetricCollector<double> _operationDurationCollector = null!;
    private MetricCollector<long> _operationIOCollector = null!;
    private MetricCollector<long> _gcCountCollector = null!;
    private MetricCollector<double> _gcDurationCollector = null!;
    private MetricCollector<long> _gcRemovedEntriesCountCollector = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        PostgresCacheMetrics metrics = WebApplication.Services.GetRequiredService<PostgresCacheMetrics>();
        _operationCountCollector = new(metrics.OperationCount, FakeTimeProvider);
        _operationDurationCollector = new(metrics.OperationDuration, FakeTimeProvider);
        _operationIOCollector = new(metrics.OperationIO, FakeTimeProvider);
        _gcCountCollector = new(metrics.GcCount, FakeTimeProvider);
        _gcDurationCollector = new(metrics.GcDuration, FakeTimeProvider);
        _gcRemovedEntriesCountCollector = new(metrics.GcRemovedEntriesCount, FakeTimeProvider);
    }

    public void Dispose() { }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();

        _operationCountCollector.Dispose();
        _operationDurationCollector.Dispose();
        _operationIOCollector.Dispose();
        _gcCountCollector.Dispose();
        _gcDurationCollector.Dispose();
        _gcRemovedEntriesCountCollector.Dispose();
    }

    [Theory]
    [CombinatorialData]
    public async Task Get_WhenItemExists_ShouldGet(bool async, bool pgBouncer, bool usePreparedStatements)
    {
        if (pgBouncer)
        {
            PostgresCacheOptions.ConnectionString = PostgresFixture.ConnectionStringPgBouncer;
        }
        PostgresCacheOptions.UsePreparedStatements = usePreparedStatements;

        string key = Guid.NewGuid().ToString();
        byte[] value = RandomNumberGenerator.GetBytes(ValueSize);
        CacheEntry cacheEntry = new(key,
            value,
            ExpiresAt: FakeTimeProvider.GetUtcNow().AddMinutes(1),
            SlidingExpiration: TimeSpan.FromMinutes(5),
            AbsoluteExpiration: null);
        await PostgresFixture.Insert(cacheEntry);

        byte[]? resultValue;
        if (async)
        {
            resultValue = await PostgresCache.GetAsync(key, default);
        }
        else
        {
            resultValue = PostgresCache.Get(key);
        }

        resultValue.Should().NotBeNull();
        resultValue.Should().BeEquivalentTo(value);

        CacheEntry? cacheEntry2 = await PostgresFixture.SelectByKey(key);
        cacheEntry2.Should().NotBeNull();
        cacheEntry2?.ExpiresAt.Should().BeCloseTo(FakeTimeProvider.GetUtcNow().AddMinutes(5), TimeSpan.FromSeconds(1));
        cacheEntry2?.SlidingExpiration.Should().Be(cacheEntry.SlidingExpiration);
        cacheEntry2?.AbsoluteExpiration.Should().BeNull();

        _operationCountCollector.GetMeasurementSnapshot().Should().HaveCount(1);
        _operationDurationCollector.GetMeasurementSnapshot().Should().HaveCount(1);
        _operationIOCollector.GetMeasurementSnapshot().Should().HaveCount(1);
    }

    [Theory]
    [CombinatorialData]
    public async Task Get_WhenItemDoesNotExist_ShouldReturnNull(bool async, bool pgBouncer, bool usePreparedStatements)
    {
        if (pgBouncer)
        {
            PostgresCacheOptions.ConnectionString = PostgresFixture.ConnectionStringPgBouncer;
        }
        PostgresCacheOptions.UsePreparedStatements = usePreparedStatements;

        string key = Guid.NewGuid().ToString();

        byte[]? resultValue;
        if (async)
        {
            resultValue = await PostgresCache.GetAsync(key, default);
        }
        else
        {
            resultValue = PostgresCache.Get(key);
        }

        resultValue.Should().BeNull();

        _operationCountCollector.GetMeasurementSnapshot().Should().HaveCount(1);
        _operationDurationCollector.GetMeasurementSnapshot().Should().HaveCount(1);
        _operationIOCollector.GetMeasurementSnapshot().Should().HaveCount(1);
    }

    public enum ExpirationScenarios
    {
        Absolute,
        AbsoluteRelativeToNow,
        Sliding,
        SlidingAndAbsolute,
        NoOption,
    }

    [Theory]
    [CombinatorialData]
    public async Task Set_WhenItemDoesNotExist_ShouldAddItem(bool async, bool pgBouncer, bool usePreparedStatements, ExpirationScenarios scenario)
    {
        if (pgBouncer)
        {
            PostgresCacheOptions.ConnectionString = PostgresFixture.ConnectionStringPgBouncer;
        }
        PostgresCacheOptions.UsePreparedStatements = usePreparedStatements;

        string key = Guid.NewGuid().ToString();
        byte[] value = RandomNumberGenerator.GetBytes(ValueSize);

        TimeSpan sliding = TimeSpan.FromMinutes(1);
        TimeSpan absolute = TimeSpan.FromMinutes(5);

        DistributedCacheEntryOptions options = new();

        if (scenario is ExpirationScenarios.Absolute or ExpirationScenarios.SlidingAndAbsolute)
        {
            options.AbsoluteExpiration = FakeTimeProvider.GetUtcNow() + absolute;
        }

        if (scenario is ExpirationScenarios.AbsoluteRelativeToNow)
        {
            options.AbsoluteExpirationRelativeToNow = absolute;
        }

        if (scenario is ExpirationScenarios.Sliding or ExpirationScenarios.SlidingAndAbsolute)
        {
            options.SlidingExpiration = sliding;
        }

        Output.WriteLine("Options {0}", options);

        if (async)
        {
            await PostgresCache.SetAsync(key, value, options, default);
        }
        else
        {
            PostgresCache.Set(key, value, options);
        }

        CacheEntry? cacheEntry = await PostgresFixture.SelectByKey(key);
        cacheEntry.Should().NotBeNull();

        cacheEntry?.Key.Should().Be(key);
        cacheEntry?.Value.Should().BeEquivalentTo(value);

        TimeSpan tolerance = TimeSpan.FromMilliseconds(10);
        if (scenario is ExpirationScenarios.Absolute or ExpirationScenarios.AbsoluteRelativeToNow)
        {
            cacheEntry?.ExpiresAt.Should().BeCloseTo(FakeTimeProvider.GetUtcNow() + absolute, tolerance);
            cacheEntry?.SlidingExpiration.Should().BeNull();
            cacheEntry?.AbsoluteExpiration.Should().BeCloseTo(FakeTimeProvider.GetUtcNow() + absolute, tolerance);
        }
        else if (scenario is ExpirationScenarios.Sliding)
        {
            cacheEntry?.ExpiresAt.Should().BeCloseTo(FakeTimeProvider.GetUtcNow() + sliding, tolerance);
            cacheEntry?.SlidingExpiration.Should().Be(sliding);
            cacheEntry?.AbsoluteExpiration.Should().BeNull();
        }
        else if (scenario is ExpirationScenarios.SlidingAndAbsolute)
        {
            cacheEntry?.ExpiresAt.Should().BeCloseTo(FakeTimeProvider.GetUtcNow() + sliding, tolerance);
            cacheEntry?.SlidingExpiration.Should().Be(sliding);
            cacheEntry?.AbsoluteExpiration.Should().BeCloseTo(FakeTimeProvider.GetUtcNow() + absolute, tolerance);
        }
        else if (scenario is ExpirationScenarios.NoOption)
        {
            cacheEntry?.ExpiresAt.Should().BeCloseTo(FakeTimeProvider.GetUtcNow() + TimeSpan.FromSeconds(PostgresCacheConstants.DefaultSlidingExpirationInSeconds), tolerance);
            cacheEntry?.SlidingExpiration.Should().NotBeNull();
            cacheEntry?.AbsoluteExpiration.Should().BeNull();
        }
        else
        {
            throw FailException.ForFailure($"Unknown scenario {scenario}");
        }

        _operationCountCollector.GetMeasurementSnapshot().Should().HaveCount(1);
        _operationDurationCollector.GetMeasurementSnapshot().Should().HaveCount(1);
        _operationIOCollector.GetMeasurementSnapshot().Should().HaveCount(1);
    }

    [Theory]
    [CombinatorialData]
    public async Task Refresh_ShouldUpdateExpiredAt(bool async, bool pgBouncer, bool usePreparedStatements)
    {
        if (pgBouncer)
        {
            PostgresCacheOptions.ConnectionString = PostgresFixture.ConnectionStringPgBouncer;
        }
        PostgresCacheOptions.UsePreparedStatements = usePreparedStatements;

        string key = Guid.NewGuid().ToString();
        byte[] value = RandomNumberGenerator.GetBytes(ValueSize);
        CacheEntry cacheEntry = new(key,
            value,
            ExpiresAt: FakeTimeProvider.GetUtcNow().AddMinutes(1),
            SlidingExpiration: TimeSpan.FromMinutes(5),
            AbsoluteExpiration: null);
        await PostgresFixture.Insert(cacheEntry);

        if (async)
        {
            await PostgresCache.RefreshAsync(key, default);
        }
        else
        {
            PostgresCache.Refresh(key);
        }

        CacheEntry? cacheEntry2 = await PostgresFixture.SelectByKey(key);
        cacheEntry2.Should().NotBeNull();
        cacheEntry2?.ExpiresAt.Should().BeCloseTo(FakeTimeProvider.GetUtcNow() + TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(1));
        cacheEntry2?.SlidingExpiration.Should().Be(cacheEntry.SlidingExpiration);
        cacheEntry2?.AbsoluteExpiration.Should().BeNull();

        _operationCountCollector.GetMeasurementSnapshot().Should().HaveCount(1);
        _operationDurationCollector.GetMeasurementSnapshot().Should().HaveCount(1);
        _operationIOCollector.GetMeasurementSnapshot().Should().HaveCount(0);
    }

    [Theory]
    [CombinatorialData]
    public async Task Remove_WhenItemExists_ShouldRemoveItem(bool async, bool pgBouncer, bool usePreparedStatements)
    {
        if (pgBouncer)
        {
            PostgresCacheOptions.ConnectionString = PostgresFixture.ConnectionStringPgBouncer;
        }
        PostgresCacheOptions.UsePreparedStatements = usePreparedStatements;

        string key = Guid.NewGuid().ToString();
        byte[] value = RandomNumberGenerator.GetBytes(ValueSize);
        CacheEntry cacheEntry = new(key,
            value,
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(1),
            SlidingExpiration: null,
            AbsoluteExpiration: null);
        await PostgresFixture.Insert(cacheEntry);

        if (async)
        {
            await PostgresCache.RemoveAsync(key, default);
        }
        else
        {
            PostgresCache.Remove(key);
        }

        CacheEntry? cacheItem2 = await PostgresFixture.SelectByKey(key);
        cacheItem2.Should().BeNull();

        _operationCountCollector.GetMeasurementSnapshot().Should().HaveCount(1);
        _operationDurationCollector.GetMeasurementSnapshot().Should().HaveCount(1);
        _operationIOCollector.GetMeasurementSnapshot().Should().HaveCount(0);
    }

    [Theory]
    [CombinatorialData]
    public async Task WhenCacheEntriesAreExpired_ThenGarbageCollectorShouldRemoveThem(bool pgBouncer, bool usePreparedStatements)
    {
        if (pgBouncer)
        {
            PostgresCacheOptions.ConnectionString = PostgresFixture.ConnectionStringPgBouncer;
        }
        PostgresCacheOptions.UsePreparedStatements = usePreparedStatements;

        string key1 = Guid.NewGuid().ToString();
        byte[] value1 = RandomNumberGenerator.GetBytes(ValueSize);
        CacheEntry? cacheEntry1 = new(key1,
            value1,
            ExpiresAt: FakeTimeProvider.GetUtcNow().AddMinutes(1),
            SlidingExpiration: null,
            AbsoluteExpiration: null);
        await PostgresFixture.Insert(cacheEntry1);

        string key2 = Guid.NewGuid().ToString();
        byte[] value2 = RandomNumberGenerator.GetBytes(ValueSize);
        CacheEntry? cacheEntry2 = new(key2,
            value2,
            ExpiresAt: FakeTimeProvider.GetUtcNow().AddMinutes(2),
            SlidingExpiration: null,
            AbsoluteExpiration: null);
        await PostgresFixture.Insert(cacheEntry2);

        await PostgresCache.RunGarbageCollection(CancellationToken.None);

        cacheEntry1 = await PostgresFixture.SelectByKey(key1);
        cacheEntry1.Should().NotBeNull("it dit not expire");
        cacheEntry2 = await PostgresFixture.SelectByKey(key2);
        cacheEntry2.Should().NotBeNull("it did not expire");

        _gcCountCollector.GetMeasurementSnapshot().Should().HaveCount(1);
        _gcDurationCollector.GetMeasurementSnapshot().Should().HaveCount(1);
        _gcRemovedEntriesCountCollector.GetMeasurementSnapshot().Should().HaveCount(1)
            .And.Subject.Last().Value.Should().Be(0);

        FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));

        cacheEntry1 = await PostgresFixture.SelectByKey(key1);
        cacheEntry1.Should().NotBeNull("garbage collection did not run");
        cacheEntry2 = await PostgresFixture.SelectByKey(key2);
        cacheEntry2.Should().NotBeNull("it did not expire");

        await PostgresCache.RunGarbageCollection(CancellationToken.None);

        cacheEntry1 = await PostgresFixture.SelectByKey(key1);
        cacheEntry1.Should().BeNull("garbage collection removed it");
        cacheEntry2 = await PostgresFixture.SelectByKey(key2);
        cacheEntry2.Should().NotBeNull("it did not expire");

        _gcCountCollector.GetMeasurementSnapshot().Should().HaveCount(2);
        _gcDurationCollector.GetMeasurementSnapshot().Should().HaveCount(2);
        _gcRemovedEntriesCountCollector.GetMeasurementSnapshot().Should().HaveCount(2)
            .And.Subject.Last().Value.Should().Be(1, "garbage collection removed one entry");

        FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));

        cacheEntry2 = await PostgresFixture.SelectByKey(key2);
        cacheEntry2.Should().NotBeNull("garbage collection did not run");

        await PostgresCache.RunGarbageCollection(CancellationToken.None);

        cacheEntry2 = await PostgresFixture.SelectByKey(key2);
        cacheEntry2.Should().BeNull("garbage collection removed it");

        _gcCountCollector.GetMeasurementSnapshot().Should().HaveCount(3);
        _gcDurationCollector.GetMeasurementSnapshot().Should().HaveCount(3);
        _gcRemovedEntriesCountCollector.GetMeasurementSnapshot().Should().HaveCount(3)
            .And.Subject.Last().Value.Should().Be(1, "garbage collection removed one entry");
    }
}