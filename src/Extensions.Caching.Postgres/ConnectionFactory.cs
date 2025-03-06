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
    private readonly bool _isDataSourceOwner;

    /// <inheritdoc cref="ConnectionFactory"/>
    public ConnectionFactory(ILogger<ConnectionFactory> logger, IOptions<PostgresCacheOptions> options)
    {
        _logger = logger;
        _options = options;
        if (_options.Value.DataSource is not null)
        {
            _dataSource = _options.Value.DataSource;
            _isDataSourceOwner = false;
        }
        else
        {
            NpgsqlDataSourceBuilder builder = new(_options.Value.ConnectionString);
            _dataSource = builder.Build();
            _isDataSourceOwner = true;
        }
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
        await connection.OpenAsync(ct).ConfigureAwait(false);
        return connection;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDataSourceOwner){
            _dataSource.Dispose();
        }
    }

    /// <inheritdoc />

    public async ValueTask DisposeAsync()
    {
        if (_isDataSourceOwner)
        {
            await _dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Creating a new connection to the database")]
    partial void LogCreatingConnection();
}