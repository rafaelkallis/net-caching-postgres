using Microsoft.Extensions.Options;

using Npgsql;

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

    public string GetCacheItem() => $@"
        UPDATE ""{Schema}"".""{TableName}"" 
        SET ""ExpiresAt"" = LEAST(""AbsoluteExpiration"", $2 + ""SlidingExpiration"")
        WHERE ""Key"" = $1 AND $2 <= ""ExpiresAt""
        RETURNING ""Value"";";

    public string RefreshCacheItem() => $@"
        UPDATE ""{Schema}"".""{TableName}"" 
        SET ""ExpiresAt"" = LEAST(""AbsoluteExpiration"", $2 + ""SlidingExpiration"")
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