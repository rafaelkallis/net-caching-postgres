using System.Data;

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
        SqlQueries sqlQueries = asyncServiceScope.ServiceProvider.GetRequiredService<SqlQueries>();
        await using NpgsqlConnection connection = await npgsqlConnections.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await using NpgsqlCommand command = new(sqlQueries.Migration(), connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Database migrated");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}