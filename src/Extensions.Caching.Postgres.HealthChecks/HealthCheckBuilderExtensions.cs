using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RafaelKallis.Extensions.Caching.Postgres.HealthChecks;

/// <summary>
/// Extension methods for adding <see cref="PostgresCacheHealthCheck"/> to the <see cref="IHealthChecksBuilder"/>.
/// </summary>
public static class HealthChecksBuilderExtensions
{
    /// <summary>
    /// Add a health check for Postgres Cache.
    /// </summary>
    public static IHealthChecksBuilder AddDistributedPostgresCacheHealthCheck(this IHealthChecksBuilder builder, Action<PostgresCacheHealthCheckOptions>? configureOptions = null, HealthStatus? failureStatus = null, IEnumerable<string>? tags = null, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        configureOptions ??= _ => { };

        builder.Services.AddOptions<PostgresCacheHealthCheckOptions>()
            .Configure(configureOptions);

        return builder.AddCheck<PostgresCacheHealthCheck>("Postgres Cache",
            failureStatus,
            tags,
            timeout);
    }
}