using System.Diagnostics;

namespace RafaelKallis.Extensions.Caching.Postgres;

internal static class PostgresCacheActivitySource
{
    private static readonly ActivitySource ActivitySource = new("RafaelKallis.Extensions.Caching.Postgres");

    internal static Activity? StartGetActivity(string key) =>
        StartActivity("Get", key);

    internal static Activity? StartSetActivity(string key) =>
        StartActivity("Set", key);

    internal static Activity? StartRefreshActivity(string key) =>
        StartActivity("Refresh", key);

    internal static Activity? StartRemoveActivity(string key) =>
        StartActivity("Remove", key);

    internal static Activity? StartGarbageCollectionActivity() =>
        StartActivity("Garbage Collection", key: null);

    internal static Activity? StartMigrationActivity() =>
        StartActivity("Migration", key: null);

    private static Activity? StartActivity(string activityType, string? key)
    {
        Activity? activity = ActivitySource.StartActivity($"PostgresCache {activityType}", ActivityKind.Client);
        if (activity is not { IsAllDataRequested: true })
        {
            return null;
        }

        if (key != null)
        {
            activity.SetTag("com.rafaelkallis.cache.key", key);
        }

        return activity;
    }
}