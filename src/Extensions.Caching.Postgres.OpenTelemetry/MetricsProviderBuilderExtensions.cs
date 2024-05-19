using JetBrains.Annotations;

using OpenTelemetry.Metrics;

namespace RafaelKallis.Extensions.Caching.Postgres.OpenTelemetry;

/// <summary>
/// Extensions for <see cref="MeterProviderBuilder" />
/// </summary>
[PublicAPI]
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Subscribes to the postgres cache activity source to enable OpenTelemetry metrics.
    /// </summary>
    public static MeterProviderBuilder AddDistributedPostgresCacheInstrumentation(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddMeter("Caching.Postgres");
    }
}