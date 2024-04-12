using Microsoft.Extensions.Options;

namespace RafaelKallis.Extensions.Caching.Postgres;

public class SqlQueries
{
    private readonly IOptions<PostgresCacheOptions> _options;

    public SqlQueries(IOptions<PostgresCacheOptions> options)
    {
        _options = options;
    }

    public string Schema => _options.Value.Schema;
    public string TableName => _options.Value.TableName;

    public string TableInfo() => $@"
        SELECT TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE
        FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_SCHEMA = '{Schema}'
        AND TABLE_NAME = '{TableName}';";

    public string UpdateCacheItem() => $@"
        UPDATE ""{Schema}"".""{TableName}""
        SET ""ExpiresAtTime"" =
            (CASE
            WHEN DATEDIFF(SECOND, $2, ""AbsoluteExpiration"") <= ""SlidingExpirationInSeconds""
            THEN ""AbsoluteExpiration""
            ELSE
            DATEADD(SECOND, ""SlidingExpirationInSeconds"", $2)
            END)
        WHERE ""Key"" = $1
        AND $2 <= ""ExpiresAtTime""
        AND ""SlidingExpirationInSeconds"" IS NOT NULL
        AND (""AbsoluteExpiration"" IS NULL OR ""AbsoluteExpiration"" <> ""ExpiresAtTime"") ;";

    public string GetCacheItem() => $@"
        SELECT ""Value""
        FROM ""{Schema}"".""{TableName}"" 
        WHERE ""Key"" = $1 AND $2 <= ""ExpiresAtTime"";";

    public string SetCacheItem() => $@"
        DECLARE @ExpiresAtTime DATETIMEOFFSET;
        SET @ExpiresAtTime = 
        (CASE
                WHEN (@SlidingExpirationInSeconds IS NUll)
                THEN @AbsoluteExpiration
                ELSE
                DATEADD(SECOND, Convert(bigint, @SlidingExpirationInSeconds), @UtcNow)
        END);
        UPDATE ""{Schema}"".""{TableName}"" SET Value = @Value, ExpiresAtTime = @ExpiresAtTime,
        SlidingExpirationInSeconds = @SlidingExpirationInSeconds, AbsoluteExpiration = @AbsoluteExpiration
        WHERE Key = @Key
        IF (@@ROWCOUNT = 0)
        BEGIN
            INSERT INTO ""{Schema}"".""{TableName}""
            (Key, Value, ExpiresAtTime, SlidingExpirationInSeconds, AbsoluteExpiration)
            VALUES (@Key, @Value, @ExpiresAtTime, @SlidingExpirationInSeconds, @AbsoluteExpiration);
        END";

    public static string DeleteCacheItem(string schema, string tableName) => $@"
        DELETE FROM ""{schema}"".""{tableName}"" WHERE Key = $1";

    public static string DeleteExpiredCacheItems(string schema, string tableName) => $@"
        DELETE FROM ""{schema}"".""{tableName}"" WHERE $1 > ""ExpiresAtTime"";";
}