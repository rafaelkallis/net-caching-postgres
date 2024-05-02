using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RafaelKallis.Extensions.Caching.Postgres;

internal class PostgresCacheMigratorHostedService(
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
        await postgresCache.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}