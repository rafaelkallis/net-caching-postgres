namespace RafaelKallis.Extensions.Caching.Postgres;

public class PostgresCacheConstants
{
    public class Columns
    {
        public const string Id = "Id";
        public const string Value = "Value";
        public const string ExpiresAtTime = "ExpiresAtTime";
        public const string SlidingExpirationInSeconds = "SlidingExpirationInSeconds";
        public const string AbsoluteExpiration = "AbsoluteExpiration";
    }

    public const string DefaultSchema = "public";
    public const string DefaultTableName = "__CacheItems";
    public const string DefaultOwner = "CURRENT_USER";
}