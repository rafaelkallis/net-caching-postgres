using Xunit.Sdk;

using DistributedCacheEntryOptions = Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions;

namespace RafaelKallis.Extensions.Caching.Postgres.Tests;

[Collection(PostgresFixture.CollectionName)]
public sealed class PostgresCacheIntegrationTest(ITestOutputHelper output, PostgresFixture postgresFixture) : IntegrationTest(output, postgresFixture)
{
    private const int ValueSize = 1024;

    [Theory]
    [CombinatorialData]
    public async Task Get_WhenItemExists_ShouldGet(bool async)
    {
        string key = Guid.NewGuid().ToString();
        byte[] value = new byte[ValueSize];
        Random.Shared.NextBytes(value);
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
    }

    [Theory]
    [CombinatorialData]
    public async Task Get_WhenItemDoesNotExist_ShouldReturnNull(bool async)
    {
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
    }

    [Theory]
    [CombinatorialData]
    public async Task Remove_WhenItemExists_ShouldRemoveItem(bool async)
    {
        string key = Guid.NewGuid().ToString();
        byte[] value = new byte[ValueSize];
        Random.Shared.NextBytes(value);
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
    public async Task Set_WhenItemDoesNotExist_ShouldAddItem(bool async, ExpirationScenarios scenario)
    {
        string key = Guid.NewGuid().ToString();
        byte[] value = new byte[ValueSize];
        Random.Shared.NextBytes(value);

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
    }

    [Theory]
    [CombinatorialData]
    public async Task Refresh_ShouldUpdateExpiredAt(bool async)
    {
        string key = Guid.NewGuid().ToString();
        byte[] value = new byte[1024];
        Random.Shared.NextBytes(value);
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
    }

    [Fact]
    public async Task WhenCacheEntriesAreExpired_ThenGarbageCollectorShouldRemoveThem()
    {

        string key1 = Guid.NewGuid().ToString();
        byte[] value1 = new byte[1024];
        Random.Shared.NextBytes(value1);
        CacheEntry? cacheEntry1 = new(key1,
            value1,
            ExpiresAt: FakeTimeProvider.GetUtcNow().AddMinutes(1),
            SlidingExpiration: null,
            AbsoluteExpiration: null);
        await PostgresFixture.Insert(cacheEntry1);

        string key2 = Guid.NewGuid().ToString();
        byte[] value2 = new byte[1024];
        Random.Shared.NextBytes(value2);
        CacheEntry? cacheEntry2 = new(key2,
            value1,
            ExpiresAt: FakeTimeProvider.GetUtcNow().AddMinutes(2),
            SlidingExpiration: null,
            AbsoluteExpiration: null);
        await PostgresFixture.Insert(cacheEntry2);

        await PostgresCache.RunGarbageCollection(CancellationToken.None);

        cacheEntry1 = await PostgresFixture.SelectByKey(key1);
        cacheEntry1.Should().NotBeNull("it dit not expire");
        cacheEntry2 = await PostgresFixture.SelectByKey(key2);
        cacheEntry2.Should().NotBeNull("it did not expire");

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

        FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));

        cacheEntry2 = await PostgresFixture.SelectByKey(key2);
        cacheEntry2.Should().NotBeNull("garbage collection did not run");

        await PostgresCache.RunGarbageCollection(CancellationToken.None);

        cacheEntry2 = await PostgresFixture.SelectByKey(key2);
        cacheEntry2.Should().BeNull("garbage collection removed it");
    }
}