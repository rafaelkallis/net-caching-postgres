namespace RafaelKallis.Extensions.Caching.Postgres;

internal class PostgresCacheConstants
{
    public const string DefaultSchemaName = "public";
    public const string DefaultTableName = "__CacheEntries";
    public const string DefaultOwner = "CURRENT_USER";
    public const int DefaultKeyMaxLength = 1024;
    public const bool DefaultCreateTableOnStart = true;
    public const bool DefaultUseUnloggedTable = false;
    public const int DefaultSlidingExpirationInSeconds = 20 * 60; // 20 minutes
    public const int DefaultGarbageCollectionIntervalInSeconds = 30 * 60; // 30 minutes
}