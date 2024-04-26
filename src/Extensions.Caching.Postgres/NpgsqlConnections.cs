using JetBrains.Annotations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace RafaelKallis.Extensions.Caching.Postgres;

/// <summary>
/// Manages the creation of <see cref="NpgsqlConnection"/>s.
/// </summary>
public sealed partial class NpgsqlConnections
{
    private readonly ILogger<NpgsqlConnections> _logger;
    private readonly IOptions<PostgresCacheOptions> _options;

    /// <inheritdoc cref="NpgsqlConnections"/>
    public NpgsqlConnections(ILogger<NpgsqlConnections> logger, IOptions<PostgresCacheOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    /// <summary>
    /// Opens a new connection to the database.
    /// </summary>
    [MustDisposeResource]
    public NpgsqlConnection OpenConnection()
    {
        LogCreatingConnection();
        NpgsqlConnection connection = new(_options.Value.ConnectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Opens a new connection to the database.
    /// </summary>
    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        LogCreatingConnection();
        NpgsqlConnection connection = new(_options.Value.ConnectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Creating a new connection to the database")]
    partial void LogCreatingConnection();
}