using Meziantou.Extensions.Logging.Xunit;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace RafaelKallis.Extensions.Caching.Postgres.Tests.Common;

public abstract class IntegrationTest : IAsyncLifetime
{
    protected ITestOutputHelper Output { get; }
    protected PostgresFixture PostgresFixture { get; }

    private WebApplication? _webApplication;

    protected IntegrationTest(ITestOutputHelper output, PostgresFixture postgresFixture)
    {
        Output = output;
        PostgresFixture = postgresFixture;
    }

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
        _webApplication = webApplicationBuilder.Build();
        ConfigureApplication(_webApplication);
        await _webApplication.StartAsync();
        Client = _webApplication.GetTestClient();
        PostgresCache = _webApplication.Services.GetRequiredService<PostgresCache>();
        PostgresCacheOptions = _webApplication.Services.GetRequiredService<IOptions<PostgresCacheOptions>>().Value;
    }

    public virtual async Task DisposeAsync()
    {
        Client.Dispose();

        if (_webApplication is not null)
        {
            await _webApplication.StopAsync();
            await _webApplication.DisposeAsync();
        }
        _webApplication = null;
    }

    protected virtual void ConfigureLogging(ILoggingBuilder logging)
    {
        XUnitLoggerProvider loggerProvider = new(Output, new XUnitLoggerOptions
        {
            IncludeLogLevel = true,
            IncludeCategory = true,
        });
        logging.AddProvider(loggerProvider);
    }

    protected virtual void ConfigureOptions(PostgresCacheOptions options)
    {
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