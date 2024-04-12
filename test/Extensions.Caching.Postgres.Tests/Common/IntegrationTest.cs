using Meziantou.Extensions.Logging.Xunit;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace RafaelKallis.Extensions.Caching.Postgres.Tests.Common;

public abstract class IntegrationTest : IAsyncLifetime
{
    protected ITestOutputHelper Output { get; }
    private WebApplication? _webApplication;
    protected HttpClient Client { get; private set; } = null!;
    protected IDistributedCache Cache { get; private set; } = null!;

    protected IntegrationTest(ITestOutputHelper output)
    {
        Output = output;
    }

    public PostgresCacheOptions PostgresCacheOptions =>
        _webApplication?.Services.GetRequiredService<IOptions<PostgresCacheOptions>>().Value
        ?? throw new InvalidOperationException("The web application is not initialized.");

    public async Task InitializeAsync()
    {
        WebApplicationBuilder webApplicationBuilder = WebApplication.CreateBuilder();
        webApplicationBuilder.WebHost.UseTestServer();
        ConfigureLogging(webApplicationBuilder.Logging);
        ConfigureServices(webApplicationBuilder.Services);
        _webApplication = webApplicationBuilder.Build();
        ConfigureWebApplication(_webApplication);
        await _webApplication.StartAsync();

        Client = _webApplication.GetTestClient();
        Cache = _webApplication.Services.GetRequiredService<IDistributedCache>();
    }

    public async Task DisposeAsync()
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
#if NET8_0_OR_GREATER  
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
        services.AddDistributedPostgresCache(configureOptions: ConfigureOptions);
        services.AddControllers();
    }

    protected virtual void ConfigureWebApplication(WebApplication app)
    {
        app.UseRouting();
    }

    protected virtual void ConfigureOptions(PostgresCacheOptions options)
    { }
}