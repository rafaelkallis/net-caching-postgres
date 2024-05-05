namespace RafaelKallis.Extensions.Caching.Postgres.HealthChecks;

/// <summary>
/// Options for <see cref="PostgresCacheHealthCheck"/>.
/// </summary>
public class PostgresCacheHealthCheckOptions
{
    internal static readonly TimeSpan DefaultDegradedTimeout = TimeSpan.FromMilliseconds(500);
    internal static readonly TimeSpan DefaultUnhealthyTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The time after which the health check is considered degraded.
    /// </summary>
    public TimeSpan DegradedTimeout { get; set; } = DefaultDegradedTimeout;

    /// <summary>
    /// The time after which the health check is considered unhealthy.
    /// </summary>
    public TimeSpan UnhealthyTimeout { get; set; } = DefaultUnhealthyTimeout;
}