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

    public string SchemaName => _options.Value.SchemaName;
    public string TableName => _options.Value.TableName;
    public string Owner => _options.Value.Owner;
    public int KeyMaxLength => _options.Value.KeyMaxLength;

    public string Migration() => $@"
        CREATE SCHEMA IF NOT EXISTS ""{SchemaName}"" AUTHORIZATION {Owner};

        CREATE TABLE IF NOT EXISTS ""{SchemaName}"".""{TableName}"" (
            ""Key"" VARCHAR({KeyMaxLength}) PRIMARY KEY,
            ""Value"" BYTEA NOT NULL,
            ""ExpiresAt"" BIGINT NOT NULL,
            ""SlidingExpiration"" BIGINT,
            ""AbsoluteExpiration"" BIGINT
        );

        ALTER TABLE ""{SchemaName}"".""{TableName}"" OWNER TO {Owner};
        ALTER TABLE ""{SchemaName}"".""{TableName}"" ALTER COLUMN ""Key"" TYPE VARCHAR({KeyMaxLength});

        CREATE INDEX IF NOT EXISTS ""IX_{TableName}_ExpiresAt"" ON ""{SchemaName}"".""{TableName}"" (""ExpiresAt"");";

    public string GetCacheEntry() => $@"
        UPDATE ""{SchemaName}"".""{TableName}"" 
        SET ""ExpiresAt"" = LEAST(""AbsoluteExpiration"", $2 + ""SlidingExpiration"")
        WHERE ""Key"" = $1 AND $2 < ""ExpiresAt""
        RETURNING ""Value"";";

    public string RefreshCacheEntry() => $@"
        UPDATE ""{SchemaName}"".""{TableName}"" 
        SET ""ExpiresAt"" = LEAST(""AbsoluteExpiration"", $2 + ""SlidingExpiration"")
        WHERE ""Key"" = $1 AND $2 < ""ExpiresAt"";";

    public string SetCacheEntry() => $@"
        INSERT INTO ""{SchemaName}"".""{TableName}"" (""Key"", ""Value"", ""ExpiresAt"", ""SlidingExpiration"", ""AbsoluteExpiration"")
            VALUES ($1, $2, $3, $4, $5)
        ON CONFLICT(""Key"") DO
        UPDATE SET
            ""Value"" = EXCLUDED.""Value"",
            ""ExpiresAt"" = EXCLUDED.""ExpiresAt"",
            ""SlidingExpiration"" = EXCLUDED.""SlidingExpiration"",
            ""AbsoluteExpiration"" = EXCLUDED.""AbsoluteExpiration"";";

    public string DeleteCacheEntry() => $@"
        DELETE FROM ""{SchemaName}"".""{TableName}"" WHERE ""Key"" = $1";

    public string DeleteExpiredCacheEntries() => $@"
        DELETE FROM ""{SchemaName}"".""{TableName}"" WHERE $1 >= ""ExpiresAt"";";

    public string DeleteExpiredCacheEntriesWithLock() => $@"
        LOCK TABLE ""{SchemaName}"".""{TableName}"" IN ROW EXCLUSIVE MODE;
        DELETE FROM ""{SchemaName}"".""{TableName}"" WHERE $1 >= ""ExpiresAt"";";
}