using Meziantou.Extensions.Logging.Xunit;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;

namespace RafaelKallis.Library.Implementation.Tests.Common;

public abstract class IntegrationTest : IAsyncLifetime
{
    protected ITestOutputHelper Output { get; }
    private WebApplication? _webApplication;
    protected HttpClient Client { get; private set; } = null!;

    protected IntegrationTest(ITestOutputHelper output)
    {
        Output = output;
    }

    public LibraryOptions LibraryOptions =>
        _webApplication?.Services.GetRequiredService<IOptions<LibraryOptions>>().Value
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
        XUnitLoggerProvider loggerProvider = new(Output, new XUnitLoggerOptions
        {
            IncludeLogLevel = true,
            IncludeCategory = true,
        });
        logging.AddProvider(loggerProvider);
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddLibrary(configureOptions: ConfigureOptions);
        services.AddControllers();
    }

    protected virtual void ConfigureWebApplication(WebApplication app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }

    protected virtual void ConfigureOptions(LibraryOptions options)
    { }
}