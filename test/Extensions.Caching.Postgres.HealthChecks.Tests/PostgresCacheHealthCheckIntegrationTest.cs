using System.Net;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using NSubstitute;

namespace RafaelKallis.Extensions.Caching.Postgres.HealthChecks.Tests;

public sealed class PostgresCacheHealthCheckIntegrationTest(ITestOutputHelper output, PostgresFixture postgresFixture) : IntegrationTest(output, postgresFixture)
{
    private IDistributedCache CacheMock { get; } = Substitute.For<IDistributedCache>();

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddSingleton(CacheMock);

        services.AddHealthChecks()
            .AddDistributedPostgresCacheHealthCheck(options =>
            {
                options.DegradedTimeout = TimeSpan.FromSeconds(10);
                options.UnhealthyTimeout = TimeSpan.FromSeconds(20);
            });
    }

    protected override void ConfigureApplication(WebApplication app)
    {
        base.ConfigureApplication(app);
        app.MapHealthChecks("/healthz");
    }

    [Theory]
    [CombinatorialData]
    public async Task WhenHealthCheckIsCalled_ThenItShouldReturnHealthy(HealthStatus healthStatus)
    {
        TimeSpan delay = healthStatus switch
        {
            HealthStatus.Healthy => TimeSpan.Zero,
            HealthStatus.Degraded => TimeSpan.FromSeconds(10),
            HealthStatus.Unhealthy => TimeSpan.FromSeconds(20),
            _ => throw new ArgumentOutOfRangeException(nameof(healthStatus)),
        };
        byte[]? valueUsed = null;
        CacheMock.SetAsync(Arg.Any<string>(),
            Arg.Do<byte[]>(value => valueUsed = value),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
        CacheMock.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                FakeTimeProvider.Advance(delay);
                return valueUsed;
            });

        Uri endpoint = new(Client.BaseAddress!, "healthz");
        HttpResponseMessage response = await Client.GetAsync(endpoint);

        HttpStatusCode expectedStatusCode = healthStatus switch
        {
            HealthStatus.Healthy => HttpStatusCode.OK,
            HealthStatus.Degraded => HttpStatusCode.OK,
            HealthStatus.Unhealthy => HttpStatusCode.ServiceUnavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(healthStatus)),
        };
        response.StatusCode.Should().Be(expectedStatusCode);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Be(healthStatus.ToString());
    }
}