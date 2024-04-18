using Meziantou.Extensions.Logging.Xunit;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace RafaelKallis.Extensions.Caching.Postgres.Tests.Common;

public abstract class IntegrationTest : IAsyncLifetime
{
    protected ITestOutputHelper Output { get; }
    private WebApplication? _webApplication;
    protected FakeTimeProvider FakeTimeProvider { get; private set; }
    protected HttpClient Client { get; private set; } = null!;
    protected PostgresCache PostgresCache { get; private set; } = null!;
    protected PostgresCacheOptions PostgresCacheOptions { get; private set; } = null!;

    protected IntegrationTest(ITestOutputHelper output)
    {
        Output = output;
        FakeTimeProvider = new();
    }

    public virtual async Task InitializeAsync()
    {
        WebApplicationBuilder webApplicationBuilder = WebApplication.CreateBuilder();
        webApplicationBuilder.WebHost.UseTestServer();
        ConfigureLogging(webApplicationBuilder.Logging);
        ConfigureServices(webApplicationBuilder.Services);
        _webApplication = webApplicationBuilder.Build();
        ConfigureWebApplication(_webApplication);
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
            await _webApplication.DisposeAsync();
        }
        _webApplication = null;
    }

    protected virtual void ConfigureLogging(ILoggingBuilder logging)
    {
#if NET7_0_OR_GREATER  
        XUnitLoggerProvider loggerProvider = new(Output, new XUnitLoggerOptions
        {
            IncludeLogLevel = true,
            IncludeCategory = true,
        });
#else 
        XUnitLoggerProvider loggerProvider = new(Output);
#endif
        logging.AddProvider(loggerProvider);
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(FakeTimeProvider);
        services.AddDistributedPostgresCache(configureOptions: ConfigureOptions);
        services.AddControllers();
    }

    protected virtual void ConfigureWebApplication(WebApplication app)
    {
        app.UseRouting();
    }

    protected virtual void ConfigureOptions(PostgresCacheOptions options)
    {
        options.EnableGarbageCollection = false;
        options.UncorrelateGarbageCollection = false;
    }
}