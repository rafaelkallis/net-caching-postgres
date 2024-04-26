using Microsoft.Extensions.Options;

namespace RafaelKallis.Extensions.Caching.Postgres;

public class SqlQueries(IOptions<PostgresCacheOptions> options)
{
    private string SchemaName => options.Value.SchemaName;
    private string TableName => options.Value.TableName;
    private string Owner => options.Value.Owner;
    private int KeyMaxLength => options.Value.KeyMaxLength;
    private bool UnloggedTable => options.Value.UnloggedTable;

    private string Unlogged => UnloggedTable ? "UNLOGGED" : string.Empty;

    public string Migration() => $@"
        CREATE SCHEMA IF NOT EXISTS ""{SchemaName}"" AUTHORIZATION {Owner};

        CREATE {Unlogged} TABLE IF NOT EXISTS ""{SchemaName}"".""{TableName}"" (
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

    public string TruncateCacheEntries() => $@"
        TRUNCATE TABLE ""{SchemaName}"".""{TableName}"";";
}