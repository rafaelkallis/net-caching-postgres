using JetBrains.Annotations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace RafaelKallis.Extensions.Caching.Postgres;

/// <summary>
/// Manages the creation of <see cref="NpgsqlConnection"/>s.
/// </summary>
public sealed partial class ConnectionFactory : IDisposable, IAsyncDisposable
{
    private readonly ILogger<ConnectionFactory> _logger;
    private readonly IOptions<PostgresCacheOptions> _options;
    private readonly NpgsqlDataSource _dataSource;

    /// <inheritdoc cref="ConnectionFactory"/>
    public ConnectionFactory(ILogger<ConnectionFactory> logger, IOptions<PostgresCacheOptions> options)
    {
        _logger = logger;
        _options = options;
        NpgsqlDataSourceBuilder dataSourceBuilder = new(_options.Value.ConnectionString);
        _dataSource = dataSourceBuilder.Build();
    }

    /// <summary>
    /// Opens a new connection to the database.
    /// </summary>
    [MustDisposeResource]
    public NpgsqlConnection OpenConnection()
    {
        LogCreatingConnection();
        NpgsqlConnection connection = _dataSource.CreateConnection();
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Opens a new connection to the database.
    /// </summary>
    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        LogCreatingConnection();
        NpgsqlConnection connection = _dataSource.CreateConnection();
        await connection.OpenAsync(ct);
        return connection;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _dataSource.Dispose();
    }

    /// <inheritdoc />

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();
    }

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Creating a new connection to the database")]
    partial void LogCreatingConnection();
}