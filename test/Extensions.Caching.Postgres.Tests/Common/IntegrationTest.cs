using Meziantou.Extensions.Logging.Xunit;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace RafaelKallis.Extensions.Caching.Postgres.Tests.Common;

public abstract class IntegrationTest(ITestOutputHelper output, PostgresFixture postgresFixture) : IAsyncLifetime
{
    protected ITestOutputHelper Output { get; } = output;
    protected PostgresFixture PostgresFixture { get; } = postgresFixture;

    private WebApplication? _webApplication;
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
#pragma warning disable CA2000 // Dispose objects before losing scope
        XUnitLoggerProvider loggerProvider = new(Output, new XUnitLoggerOptions
        {
            IncludeLogLevel = true,
            IncludeCategory = true,
        });
#pragma warning restore CA2000 // Dispose objects before losing scope
        logging.AddProvider(loggerProvider);
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