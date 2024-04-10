using Microsoft.Extensions.Logging;

namespace RafaelKallis.Library.Implementation;

public sealed class Service : IService, IDisposable
{
    private readonly ILogger<Service> _logger;

    public Service(ILogger<Service> logger)
    {
        _logger = logger;
    }

    public int AddOne(int value)
    {
        _logger.LogInformation("Adding 1 to {Value}", value);
        return value + 1;
    }

    public void Dispose()
    {
        _logger.LogDebug("Disposing");
    }
}