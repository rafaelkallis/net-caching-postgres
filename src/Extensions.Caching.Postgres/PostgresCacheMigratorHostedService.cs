using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RafaelKallis.Extensions.Caching.Postgres;

internal class PostgresCacheMigratorHostedService : IHostedService
{
    private readonly ILogger<PostgresCacheMigratorHostedService> _logger;
    private readonly IOptions<PostgresCacheOptions> _options;
    private readonly PostgresCache _postgresCache;

    public PostgresCacheMigratorHostedService(ILogger<PostgresCacheMigratorHostedService> logger,
        IOptions<PostgresCacheOptions> options,
        PostgresCache postgresCache)
    {
        _logger = logger;
        _options = options;
        _postgresCache = postgresCache;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Value.MigrateOnStart)
        {
            _logger.LogInformation("Skipping migration");
            return;
        }
        await _postgresCache.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}