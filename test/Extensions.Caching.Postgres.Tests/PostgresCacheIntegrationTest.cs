using Xunit.Sdk;

using DistributedCacheEntryOptions = Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions;

namespace RafaelKallis.Extensions.Caching.Postgres.Tests;

[Collection(PostgresFixture.CollectionName)]
public class PostgresCacheIntegrationTest : IntegrationTest
{
    private readonly PostgresFixture _postgresFixture;

    public PostgresCacheIntegrationTest(ITestOutputHelper output, PostgresFixture postgresFixture) : base(output)
    {
        _postgresFixture = postgresFixture;
    }

    protected override void ConfigureOptions(PostgresCacheOptions options)
    {
        options.ConnectionString = _postgresFixture.ConnectionString;
    }

    [Theory]
    [CombinatorialData]
    public async Task Get_WhenItemExists_ShouldGet(bool async)
    {
        DateTime now = DateTime.UtcNow;
        string key = Guid.NewGuid().ToString();
        byte[] value = new byte[1024];
        Random.Shared.NextBytes(value);
        CacheEntry cacheEntry = new(key,
            value,
            ExpiresAt: now.AddMinutes(1),
            SlidingExpiration: TimeSpan.FromMinutes(5),
            AbsoluteExpiration: null);
        await _postgresFixture.Insert(cacheEntry);

        byte[]? resultValue;
        if (async)
        {
            resultValue = await Cache.GetAsync(key);
        }
        else
        {
            resultValue = Cache.Get(key);
        }

        resultValue.Should().NotBeNull();
        resultValue.Should().BeEquivalentTo(value);

        CacheEntry? cacheEntry2 = await _postgresFixture.SelectOne(key);
        cacheEntry2.Should().NotBeNull();
        cacheEntry2?.ExpiresAt.Should().BeCloseTo(now + TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(1));
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
            resultValue = await Cache.GetAsync(key);
        }
        else
        {
            resultValue = Cache.Get(key);
        }

        resultValue.Should().BeNull();
    }

    [Theory]
    [CombinatorialData]
    public async Task Remove_WhenItemExists_ShouldRemoveItem(bool async)
    {
        string key = Guid.NewGuid().ToString();
        byte[] value = new byte[1024];
        Random.Shared.NextBytes(value);
        CacheEntry cacheEntry = new(key,
            value,
            ExpiresAt: DateTime.UtcNow.AddMinutes(1),
            SlidingExpiration: null,
            AbsoluteExpiration: null);
        await _postgresFixture.Insert(cacheEntry);

        if (async)
        {
            await Cache.RemoveAsync(key);
        }
        else
        {
            Cache.Remove(key);
        }

        CacheEntry? cacheItem2 = await _postgresFixture.SelectOne(key);
        cacheItem2.Should().BeNull();
    }

    public enum ExpirationScenarios
    {
        NoOptions,
        Absolute,
        AbsoluteRelativeToNow,
        Sliding,
        SlidingAndAbsolute,
    }

    [Theory]
    [CombinatorialData]
    public async Task Set_WhenItemDoesNotExist_ShouldAddItem(bool async, ExpirationScenarios scenario)
    {
        DateTime now = DateTime.UtcNow;
        string key = Guid.NewGuid().ToString();
        byte[] value = new byte[1024];
        Random.Shared.NextBytes(value);

        TimeSpan sliding = TimeSpan.FromMinutes(1);
        TimeSpan absolute = TimeSpan.FromMinutes(5);

        DistributedCacheEntryOptions options = new();

        if (scenario is ExpirationScenarios.Absolute or ExpirationScenarios.SlidingAndAbsolute)
        {
            options.AbsoluteExpiration = now + absolute;
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
            await Cache.SetAsync(key, value, options);
        }
        else
        {
            Cache.Set(key, value, options);
        }

        CacheEntry? cacheEntry = await _postgresFixture.SelectOne(key);
        cacheEntry.Should().NotBeNull();

        cacheEntry?.Key.Should().Be(key);
        cacheEntry?.Value.Should().BeEquivalentTo(value);

        TimeSpan tolerance = TimeSpan.FromMilliseconds(10);
        if (scenario is ExpirationScenarios.NoOptions)
        {
            cacheEntry?.ExpiresAt.Should().BeCloseTo(now.Add(PostgresCacheOptions.DefaultSlidingExpiration), TimeSpan.FromSeconds(1));
            cacheEntry?.SlidingExpiration.Should().BeNull();
            cacheEntry?.AbsoluteExpiration.Should().BeNull();
        }
        else if (scenario is ExpirationScenarios.Absolute or ExpirationScenarios.AbsoluteRelativeToNow)
        {
            cacheEntry?.ExpiresAt.Should().BeCloseTo(now + absolute, tolerance);
            cacheEntry?.SlidingExpiration.Should().BeNull();
            cacheEntry?.AbsoluteExpiration.Should().BeCloseTo(now + absolute, tolerance);
        }
        else if (scenario is ExpirationScenarios.Sliding)
        {
            cacheEntry?.ExpiresAt.Should().BeCloseTo(now + sliding, tolerance);
            cacheEntry?.SlidingExpiration.Should().Be(sliding);
            cacheEntry?.AbsoluteExpiration.Should().BeNull();
        }
        else if (scenario is ExpirationScenarios.SlidingAndAbsolute)
        {
            cacheEntry?.ExpiresAt.Should().BeCloseTo(now + sliding, tolerance);
            cacheEntry?.SlidingExpiration.Should().Be(sliding);
            cacheEntry?.AbsoluteExpiration.Should().BeCloseTo(now + absolute, tolerance);
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
        DateTime now = DateTime.UtcNow;
        string key = Guid.NewGuid().ToString();
        byte[] value = new byte[1024];
        Random.Shared.NextBytes(value);
        CacheEntry cacheEntry = new(key,
            value,
            ExpiresAt: now.AddMinutes(1),
            SlidingExpiration: TimeSpan.FromMinutes(5),
            AbsoluteExpiration: null);
        await _postgresFixture.Insert(cacheEntry);

        if (async)
        {
            await Cache.RefreshAsync(key);
        }
        else
        {
            Cache.Refresh(key);
        }

        CacheEntry? cacheEntry2 = await _postgresFixture.SelectOne(key);
        cacheEntry2.Should().NotBeNull();
        cacheEntry2?.ExpiresAt.Should().BeCloseTo(now + TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(1));
        cacheEntry2?.SlidingExpiration.Should().Be(cacheEntry.SlidingExpiration);
        cacheEntry2?.AbsoluteExpiration.Should().BeNull();
    }
}