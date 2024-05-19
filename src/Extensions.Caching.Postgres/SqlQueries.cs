using Microsoft.Extensions.Options;

namespace RafaelKallis.Extensions.Caching.Postgres;

/// <summary>
/// SQL queries used by <see cref="PostgresCache"/>.
/// </summary>
public class SqlQueries
{
    internal readonly string Migration;
    internal readonly string GetCacheEntry;
    internal readonly string SetCacheEntry;
    internal readonly string RefreshCacheEntry;
    internal readonly string RemoveCacheEntry;
    internal readonly string DeleteExpiredCacheEntries;
    internal readonly string DeleteExpiredCacheEntriesWithLock;
    internal readonly string TruncateCacheEntries;

    /// <inheritdoc cref="SqlQueries" /> 
    public SqlQueries(IOptions<PostgresCacheOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        string schemaName = options.Value.SchemaName;
        string tableName = options.Value.TableName;
        string migrationsTableName = options.Value.MigrationHistoryTableName;
        string owner = options.Value.Owner;
        int keyMaxLength = options.Value.KeyMaxLength;
        string unlogged = options.Value.UseUnloggedTable ? "UNLOGGED" : string.Empty;

        Migration = $@"
            CREATE SCHEMA IF NOT EXISTS ""{schemaName}"" AUTHORIZATION {owner};
            
            CREATE TABLE IF NOT EXISTS ""{schemaName}"".""{migrationsTableName}"" (
                ""Version"" INT NOT NULL PRIMARY KEY,
                ""AppliedAt"" TIMESTAMP WITH TIME ZONE NOT NULL
            );

            INSERT INTO ""{schemaName}"".""{migrationsTableName}"" (""Version"", ""AppliedAt"")
                VALUES (1, CURRENT_TIMESTAMP)
            ON CONFLICT DO NOTHING;

            CREATE {unlogged} TABLE IF NOT EXISTS ""{schemaName}"".""{tableName}"" (
                ""Key"" VARCHAR({keyMaxLength}) PRIMARY KEY,
                ""Value"" BYTEA NOT NULL,
                ""ExpiresAt"" BIGINT NOT NULL,
                ""SlidingExpiration"" BIGINT,
                ""AbsoluteExpiration"" BIGINT
            );

            ALTER TABLE ""{schemaName}"".""{tableName}"" OWNER TO {owner};
            ALTER TABLE ""{schemaName}"".""{tableName}"" ALTER COLUMN ""Key"" TYPE VARCHAR({keyMaxLength});

            CREATE INDEX IF NOT EXISTS ""IX_{tableName}_ExpiresAt"" ON ""{schemaName}"".""{tableName}"" (""ExpiresAt"");";

        GetCacheEntry = $@"
            UPDATE ""{schemaName}"".""{tableName}"" 
            SET ""ExpiresAt"" = LEAST(""AbsoluteExpiration"", $2 + ""SlidingExpiration"")
            WHERE ""Key"" = $1 AND $2 < ""ExpiresAt""
            RETURNING ""Value"";";

        SetCacheEntry = $@"
            INSERT INTO ""{schemaName}"".""{tableName}"" (""Key"", ""Value"", ""ExpiresAt"", ""SlidingExpiration"", ""AbsoluteExpiration"")
                VALUES ($1, $2, $3, $4, $5)
            ON CONFLICT(""Key"") DO
            UPDATE SET
                ""Value"" = EXCLUDED.""Value"",
                ""ExpiresAt"" = EXCLUDED.""ExpiresAt"",
                ""SlidingExpiration"" = EXCLUDED.""SlidingExpiration"",
                ""AbsoluteExpiration"" = EXCLUDED.""AbsoluteExpiration"";";

        RefreshCacheEntry = $@"
            UPDATE ""{schemaName}"".""{tableName}"" 
            SET ""ExpiresAt"" = LEAST(""AbsoluteExpiration"", $2 + ""SlidingExpiration"")
            WHERE ""Key"" = $1 AND $2 < ""ExpiresAt"";";

        RemoveCacheEntry = $@"
            DELETE FROM ""{schemaName}"".""{tableName}"" WHERE ""Key"" = $1";

        DeleteExpiredCacheEntries = $@"
            DELETE FROM ""{schemaName}"".""{tableName}"" WHERE $1 >= ""ExpiresAt"";";

        DeleteExpiredCacheEntriesWithLock = $@"
            LOCK TABLE ""{schemaName}"".""{tableName}"" IN ROW EXCLUSIVE MODE;
            DELETE FROM ""{schemaName}"".""{tableName}"" WHERE $1 >= ""ExpiresAt"";";

        TruncateCacheEntries = $@"
            TRUNCATE TABLE ""{schemaName}"".""{tableName}"";";
    }


}