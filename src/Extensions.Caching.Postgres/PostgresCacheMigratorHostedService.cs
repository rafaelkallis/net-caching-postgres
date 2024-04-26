using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RafaelKallis.Extensions.Caching.Postgres;

public class PostgresCacheMigratorHostedService(
    ILogger<PostgresCacheMigratorHostedService> logger,
    IOptions<PostgresCacheOptions> options,
    PostgresCache postgresCache)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.MigrateOnStart)
        {
            logger.LogInformation("Skipping migration");
            return;
        }
        await postgresCache.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}