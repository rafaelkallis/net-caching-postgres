using System.Diagnostics;

namespace RafaelKallis.Extensions.Caching.Postgres;

internal static class PostgresCacheActivitySource
{
    private const string ActivitySourceName = "Caching.Postgres";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    internal static Activity? StartGetActivity(string key) =>
        StartActivity(activityType: "Get", key: key, absoluteExpirationRelativeToNow: null, slidingExpiration: null);

    internal static Activity? StartSetActivity(string key, TimeSpan? absoluteExpirationRelativeToNow, TimeSpan? slidingExpiration) =>
        StartActivity(activityType: "Set",
            key: key,
            absoluteExpirationRelativeToNow: absoluteExpirationRelativeToNow,
            slidingExpiration: slidingExpiration);

    internal static Activity? StartRefreshActivity(string key) =>
        StartActivity(activityType: "Refresh", key: key, absoluteExpirationRelativeToNow: null, slidingExpiration: null);

    internal static Activity? StartRemoveActivity(string key) =>
        StartActivity(activityType: "Remove", key: key, absoluteExpirationRelativeToNow: null, slidingExpiration: null);

    internal static Activity? StartGarbageCollectionActivity() =>
        StartActivity(activityType: "Garbage Collection", key: null, absoluteExpirationRelativeToNow: null, slidingExpiration: null);

    internal static Activity? StartMigrationActivity() =>
        StartActivity(activityType: "Migration", key: null, absoluteExpirationRelativeToNow: null, slidingExpiration: null);

    private static Activity? StartActivity(string activityType, string? key = null, TimeSpan? absoluteExpirationRelativeToNow = null, TimeSpan? slidingExpiration = null)
    {
        Activity? activity = ActivitySource.StartActivity($"PostgresCache {activityType}", ActivityKind.Internal);

        if (activity is not { IsAllDataRequested: true })
        {
            return null;
        }

        activity.SetTag("otel.status_code", "ERROR");

        if (absoluteExpirationRelativeToNow != null)
        {
            activity.SetTag("rafaelkallis.absoluteExpirationDuration", absoluteExpirationRelativeToNow.ToString());
        }

        if (slidingExpiration != null)
        {
            activity.SetTag("rafaelkallis.slidingExpirationDuration", slidingExpiration.ToString());
        }

        return activity;
    }
}