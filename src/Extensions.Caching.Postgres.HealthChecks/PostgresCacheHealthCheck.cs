using System.Security.Cryptography;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RafaelKallis.Extensions.Caching.Postgres.HealthChecks;

/// <summary>
/// Health check for Postgres Cache.
/// </summary>
public class PostgresCacheHealthCheck(ILogger<PostgresCacheHealthCheck> logger, IOptions<PostgresCacheHealthCheckOptions> options, IDistributedCache cache, TimeProvider timeProvider) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        DateTimeOffset start = timeProvider.GetUtcNow();
        string key = $"HealthCheck:{start:O}";
        byte[] value = RandomNumberGenerator.GetBytes(16);

        logger.LogDebug("Checking health of postgres cache: Key {Key}, Value {Value}", key, Convert.ToHexString(value));

        DistributedCacheEntryOptions entryOptions = new()
        {
            AbsoluteExpirationRelativeToNow = options.Value.UnhealthyTimeout,
        };
        // using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // cancellationToken = timeoutCts.Token;
        // timeoutCts.CancelAfter(options.Value.UnhealthyTimeout);
        try
        {
            Task timeoutTask = Task.Delay(options.Value.UnhealthyTimeout, cancellationToken);
            Task setTask = cache.SetAsync(key, value, entryOptions, cancellationToken);
            await Task.WhenAny([setTask, timeoutTask]).ConfigureAwait(false);

            TimeSpan elapsed = timeProvider.GetUtcNow() - start;
            if (elapsed >= options.Value.UnhealthyTimeout)
            {
                return HealthCheckResult.Unhealthy("The cache response time is unhealthy.");
            }

            Task<byte[]?> getTask = cache.GetAsync(key, cancellationToken);
            await Task.WhenAny([getTask, timeoutTask]).ConfigureAwait(false);
            byte[]? result = await getTask.ConfigureAwait(false);

            if (result == null || !value.SequenceEqual(result))
            {
                return new(context.Registration.FailureStatus);
            }

            elapsed = timeProvider.GetUtcNow() - start;
            logger.LogDebug("Postgres cache health check took {Elapsed}", elapsed);
            if (elapsed >= options.Value.UnhealthyTimeout)
            {
                return HealthCheckResult.Unhealthy("The cache response time is unhealthy.");
            }

            if (elapsed >= options.Value.DegradedTimeout)
            {
                return HealthCheckResult.Degraded("The cache response time is degraded.");
            }

            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException ex)
        {
            return HealthCheckResult.Unhealthy("The cache response time is unhealthy.", ex);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }

    }
}