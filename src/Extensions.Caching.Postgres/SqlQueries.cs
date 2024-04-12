using Microsoft.Extensions.Options;

namespace RafaelKallis.Extensions.Caching.Postgres;

public class SqlQueries
{
    private readonly IOptions<PostgresCacheOptions> _options;

    public SqlQueries(IOptions<PostgresCacheOptions> options)
    {
        _options = options;
    }

    public string Schema => _options.Value.SchemaName;
    public string TableName => _options.Value.TableName;

    public string TableInfo() => $@"
        SELECT TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE
        FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_SCHEMA = '{Schema}'
        AND TABLE_NAME = '{TableName}';";

    public string UpdateCacheItem() => $@"
        UPDATE ""{Schema}"".""{TableName}""
        SET ""ExpiresAt"" =
            (CASE
            WHEN DATEDIFF(SECOND, $2, ""AbsoluteExpiration"") <= ""SlidingExpiration""
            THEN ""AbsoluteExpiration""
            ELSE
            DATEADD(SECOND, ""SlidingExpiration"", $2)
            END)
        WHERE ""Key"" = $1
        AND $2 <= ""ExpiresAt""
        AND ""SlidingExpiration"" IS NOT NULL
        AND (""AbsoluteExpiration"" IS NULL OR ""AbsoluteExpiration"" <> ""ExpiresAt"") ;";

    public string GetCacheItem() => $@"
        SELECT ""Value""
        FROM ""{Schema}"".""{TableName}"" 
        WHERE ""Key"" = $1 AND $2 <= ""ExpiresAt"";";

    public string SetCacheItem() => $@"
        INSERT INTO ""{Schema}"".""{TableName}"" (""Key"", ""Value"", ""ExpiresAt"", ""SlidingExpiration"", ""AbsoluteExpiration"")
            VALUES ($1, $2, $3, $4, $5)
        ON CONFLICT(""Key"") DO
        UPDATE SET
            ""Value"" = EXCLUDED.""Value"",
            ""ExpiresAt"" = EXCLUDED.""ExpiresAt"",
            ""SlidingExpiration"" = EXCLUDED.""SlidingExpiration"",
            ""AbsoluteExpiration"" = EXCLUDED.""AbsoluteExpiration"";";

    public string DeleteCacheItem() => $@"
        DELETE FROM ""{Schema}"".""{TableName}"" WHERE ""Key"" = $1";

    public string DeleteExpiredCacheItems() => $@"
        DELETE FROM ""{Schema}"".""{TableName}"" WHERE $1 > ""ExpiresAt"";";
}