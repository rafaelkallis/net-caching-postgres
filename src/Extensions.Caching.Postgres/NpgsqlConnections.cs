using JetBrains.Annotations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace RafaelKallis.Extensions.Caching.Postgres;

/// <summary>
/// Manages the creation of <see cref="NpgsqlConnection"/>s.
/// </summary>
public sealed partial class NpgsqlConnections : IDisposable, IAsyncDisposable
{
    private readonly ILogger<NpgsqlConnections> _logger;
    private readonly IOptions<PostgresCacheOptions> _options;
#if NET7_0_OR_GREATER
    private readonly NpgsqlDataSource _dataSource;
#endif

    internal NpgsqlConnections(ILogger<NpgsqlConnections> logger, IOptions<PostgresCacheOptions> options)
    {
        _logger = logger;
        _options = options;
#if NET7_0_OR_GREATER
        NpgsqlDataSourceBuilder dataSourceBuilder = new(_options.Value.ConnectionString);
        _dataSource = dataSourceBuilder.Build();
#endif
    }

    /// <summary>
    /// Opens a new connection to the database.
    /// </summary>
    [MustDisposeResource]
    public NpgsqlConnection OpenConnection()
    {
        LogCreatingConnection();
#if NET7_0_OR_GREATER
        NpgsqlConnection connection = _dataSource.CreateConnection();
#else
        NpgsqlConnection connection = new(_options.Value.ConnectionString);
#endif
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Opens a new connection to the database.
    /// </summary>
    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        LogCreatingConnection();
#if NET7_0_OR_GREATER
        NpgsqlConnection connection = _dataSource.CreateConnection();
#else
        NpgsqlConnection connection = new(_options.Value.ConnectionString);
#endif
        await connection.OpenAsync(ct);
        return connection;
    }

    /// <inheritdoc />
    public void Dispose()
    {
#if NET7_0_OR_GREATER
        _dataSource.Dispose();
#endif
    }

    /// <inheritdoc />

    public async ValueTask DisposeAsync()
    {
#if NET7_0_OR_GREATER
        await _dataSource.DisposeAsync();
#else
        await Task.CompletedTask;
#endif
    }

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Creating a new connection to the database")]
    partial void LogCreatingConnection();
}