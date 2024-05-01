using System.Runtime.CompilerServices;

using JetBrains.Annotations;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using NpgsqlTypes;

namespace RafaelKallis.Extensions.Caching.Postgres;

/// <summary>
/// Extensions for the <see cref="IDistributedCache"/> to use a postgres database.
/// </summary>
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
        services.AddSingleton<ConnectionFactory>();
        services.AddSingleton<PostgresCache>();
        services.AddSingleton<IDistributedCache>(sp => sp.GetRequiredService<PostgresCache>());

        services.AddHostedService<PostgresCacheMigratorHostedService>();
        services.AddHostedService<PostgresCacheGarbageCollectorBackgroundService>();

        return services;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static DateTime AsUnixTimeMillisecondsDateTime(this long unixTimeMilliseconds) =>
        DateTime.UnixEpoch.AddMilliseconds(unixTimeMilliseconds);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long ToUnixTimeMilliseconds(this DateTime dateTime) =>
        dateTime.Subtract(DateTime.UnixEpoch).ToMilliseconds();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TimeSpan AsMillisecondsTimeSpan(this long unixTimeMilliseconds) =>
        TimeSpan.FromMilliseconds(Convert.ToDouble(unixTimeMilliseconds));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long ToMilliseconds(this TimeSpan timeSpan) =>
        Convert.ToInt64(timeSpan.TotalMilliseconds);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static NpgsqlParameter AddWithValue<T>(this NpgsqlParameterCollection parameters, NpgsqlDbType dbType, T? value) =>
        parameters.Add(new() { NpgsqlDbType = dbType, Value = value ?? DBNull.Value as object });
}