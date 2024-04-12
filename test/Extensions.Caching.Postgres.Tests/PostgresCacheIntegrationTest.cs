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
    public async Task WhenValueExists_ShouldGet(bool async)
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
}