namespace RafaelKallis.Extensions.Caching.Postgres;

public class PostgresCacheConstants
{
    public const string DefaultSchemaName = "public";
    public const string DefaultTableName = "__CacheEntries";
    public const string DefaultOwner = "CURRENT_USER";
    public const int DefaultKeyMaxLength = 1024;
    public const int DefaultSlidingExpirationInSeconds = 60 * 20; // 20 minutes
    public const int DefaultGarbageCollectionIntervalInSeconds = 60 * 30; // 30 minutes
}