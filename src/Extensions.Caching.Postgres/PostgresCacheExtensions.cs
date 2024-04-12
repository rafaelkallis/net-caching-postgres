using JetBrains.Annotations;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace RafaelKallis.Extensions.Caching.Postgres;

[PublicAPI]
public static class PostgresCacheExtensions
{
    /// <summary>
    /// Adds a postgres based <see cref="IDistributedCache"/>.
    /// </summary>
    public static IServiceCollection AddDistributedPostgresCache(this IServiceCollection services,
        Action<PostgresCacheOptions>? configureOptions = null)
    {
        configureOptions ??= _ => { };

        services.AddOptions<PostgresCacheOptions>()
            .Configure(configureOptions)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<SqlQueries>();
        services.AddSingleton<NpgsqlConnections>();
        services.AddSingleton<IDistributedCache, PostgresCache>();

        services.AddHostedService<PostgresCacheDatabaseMigrator>();

        return services;
    }
}