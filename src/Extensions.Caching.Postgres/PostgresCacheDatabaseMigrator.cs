using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace RafaelKallis.Extensions.Caching.Postgres;

public class PostgresCacheDatabaseMigrator : IHostedService
{
    private readonly ILogger<PostgresCacheDatabaseMigrator> _logger;
    private readonly IServiceProvider _serviceProvider;

    public PostgresCacheDatabaseMigrator(ILogger<PostgresCacheDatabaseMigrator> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Migrating database...");
        await using AsyncServiceScope asyncServiceScope = _serviceProvider.CreateAsyncScope();
        NpgsqlConnections npgsqlConnections = asyncServiceScope.ServiceProvider.GetRequiredService<NpgsqlConnections>();
        IOptions<PostgresCacheOptions> options = asyncServiceScope.ServiceProvider.GetRequiredService<IOptions<PostgresCacheOptions>>();
        await using NpgsqlConnection connection = await npgsqlConnections.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

        string schema = options.Value.Schema;
        string tableName = options.Value.TableName;
        string owner = options.Value.Owner;

        string sql = @$"
            CREATE SCHEMA IF NOT EXISTS ""{schema}"" AUTHORIZATION {owner};

            CREATE TABLE IF NOT EXISTS ""{schema}"".""{tableName}"" (
                ""Id"" TEXT PRIMARY KEY,
                ""Value"" BYTEA NOT NULL,
                ""ExpiresAtTime"" TIMESTAMPTZ NOT NULL,
                ""SlidingExpirationInSeconds"" BIGINT,
                ""AbsoluteExpiration"" TIMESTAMPTZ
            );

            ALTER TABLE ""{schema}"".""{tableName}"" OWNER TO {owner};

            CREATE INDEX IF NOT EXISTS ""IX_{tableName}_ExpiresAtTime"" ON ""{schema}"".""{tableName}"" (""ExpiresAtTime"");";
        await using NpgsqlCommand command = new(cmdText: sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Database migrated");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}