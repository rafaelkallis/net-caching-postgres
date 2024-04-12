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
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenItemExists_ShouldGet(bool async)
    {
        string key = Guid.NewGuid().ToString();
        byte[] value = new byte[1024];
        Random.Shared.NextBytes(value);
        CacheItem cacheItem = new(key,
            value,
            ExpiresAtTime: DateTimeOffset.UtcNow.AddMinutes(1),
            SlidingExpirationInSeconds: null,
            AbsoluteExpiration: null);
        await _postgresFixture.Insert(cacheItem);

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
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenItemDoesNotExist_ShouldReturnNull(bool async)
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
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenItemExists_ShouldRemoveItem(bool async)
    {
        string key = Guid.NewGuid().ToString();
        byte[] value = new byte[1024];
        Random.Shared.NextBytes(value);
        CacheItem cacheItem = new(key,
            value,
            ExpiresAtTime: DateTimeOffset.UtcNow.AddMinutes(1),
            SlidingExpirationInSeconds: null,
            AbsoluteExpiration: null);
        await _postgresFixture.Insert(cacheItem);

        if (async)
        {
            await Cache.RemoveAsync(key);
        }
        else
        {
            Cache.Remove(key);
        }

        CacheItem? cacheItem2 = await _postgresFixture.SelectOne(key);
        cacheItem2.Should().BeNull();
    }
}