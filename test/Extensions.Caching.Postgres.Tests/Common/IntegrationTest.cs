using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace RafaelKallis.Extensions.Caching.Postgres.Tests.Common;

public abstract class IntegrationTest(ITestOutputHelper output, PostgresFixture postgresFixture) : IAsyncLifetime, IClassFixture<PostgresFixture>
{
    protected ITestOutputHelper Output { get; } = output;
    protected PostgresFixture PostgresFixture { get; } = postgresFixture;

    protected WebApplication WebApplication { get; private set; } = null!;
    protected FakeTimeProvider FakeTimeProvider { get; private set; } = new();
    protected HttpClient Client { get; private set; } = null!;
    protected PostgresCache PostgresCache { get; private set; } = null!;
    protected PostgresCacheOptions PostgresCacheOptions { get; private set; } = null!;

    public virtual async Task InitializeAsync()
    {
        WebApplicationBuilder webApplicationBuilder = WebApplication.CreateBuilder();
        webApplicationBuilder.WebHost.UseTestServer();
        ConfigureLogging(webApplicationBuilder.Logging);
        ConfigureServices(webApplicationBuilder.Services);
        WebApplication = webApplicationBuilder.Build();
        ConfigureApplication(WebApplication);
        await WebApplication.StartAsync();
        Client = WebApplication.GetTestClient();
        PostgresCache = WebApplication.Services.GetRequiredService<PostgresCache>();
        PostgresCacheOptions = WebApplication.Services.GetRequiredService<IOptions<PostgresCacheOptions>>().Value;
    }

    public virtual async Task DisposeAsync()
    {
        await PostgresFixture.Truncate();

        Client.Dispose();

        await WebApplication.StopAsync();
        await WebApplication.DisposeAsync();
    }

    protected virtual void ConfigureLogging(ILoggingBuilder logging)
    {
        logging.AddFilter("RafaelKallis", LogLevel.Debug);
    }

    protected virtual void ConfigureOptions(PostgresCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.ConnectionString = PostgresFixture.ConnectionString;
        options.EnableGarbageCollection = false;
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(FakeTimeProvider);
        services.AddDistributedPostgresCache(configureOptions: ConfigureOptions);
        services.AddControllers();
    }

    protected virtual void ConfigureApplication(WebApplication app)
    {
        app.UseRouting();
    }
}