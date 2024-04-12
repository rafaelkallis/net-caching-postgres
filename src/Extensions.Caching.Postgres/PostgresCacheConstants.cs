namespace RafaelKallis.Extensions.Caching.Postgres;

public class PostgresCacheConstants
{
    public const string DefaultSchema = "public";
    public const string DefaultTableName = "__CacheItems";
    public const string DefaultOwner = "CURRENT_USER";
    public const int DefaultKeyMaxLength = 1024;
}