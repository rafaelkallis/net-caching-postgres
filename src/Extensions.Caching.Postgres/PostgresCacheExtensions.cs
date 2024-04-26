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
        services.AddSingleton<PostgresCache>();
        services.AddSingleton<IDistributedCache>(sp => sp.GetRequiredService<PostgresCache>());

        services.AddHostedService<PostgresCacheMigratorHostedService>();
        services.AddHostedService<PostgresCacheGarbageCollectorBackgroundService>();

        return services;
    }

    internal static DateTime AsUnixTimeMillisecondsDateTime(this long unixTimeMilliseconds) =>
        DateTime.UnixEpoch.AddMilliseconds(unixTimeMilliseconds);

    internal static long ToUnixTimeMilliseconds(this DateTime dateTime) =>
        dateTime.Subtract(DateTime.UnixEpoch).ToMilliseconds();

    internal static TimeSpan AsMillisecondsTimeSpan(this long unixTimeMilliseconds) =>
        TimeSpan.FromMilliseconds(Convert.ToDouble(unixTimeMilliseconds));

    internal static long ToMilliseconds(this TimeSpan timeSpan) =>
        Convert.ToInt64(timeSpan.TotalMilliseconds);
}