﻿using OpenTelemetry.Trace;

namespace Extensions.Caching.Postgres.OpenTelemetry;

/// <summary>
/// Extensions for <see cref="TracerProviderBuilder" />
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Subscribes to the postgres cache activity source to enable OpenTelemetry tracing.
    /// </summary>
    public static TracerProviderBuilder AddDistributedPostgresCacheInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource("Caching.Postgres");
    }
}