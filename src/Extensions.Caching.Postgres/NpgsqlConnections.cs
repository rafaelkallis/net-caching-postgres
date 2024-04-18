using JetBrains.Annotations;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace RafaelKallis.Extensions.Caching.Postgres;

public sealed partial class NpgsqlConnections : IDisposable, IAsyncDisposable
{
    private readonly ILogger<NpgsqlConnections> _logger;
    private readonly IOptions<PostgresCacheOptions> _options;
#if NET7_0_OR_GREATER
    private readonly NpgsqlDataSource _dataSource; 
#endif

    public NpgsqlConnections(ILogger<NpgsqlConnections> logger, IOptions<PostgresCacheOptions> options)
    {
        _logger = logger;
        _options = options;
#if NET7_0_OR_GREATER
        NpgsqlDataSourceBuilder dataSourceBuilder = new(_options.Value.ConnectionString);
        _dataSource = dataSourceBuilder.Build();
#endif
    }

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

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        LogCreatingConnection();
#if NET8_0_OR_GREATER
        NpgsqlConnection connection = _dataSource.CreateConnection();
#else
        NpgsqlConnection connection = new(_options.Value.ConnectionString);
#endif
        await connection.OpenAsync(ct);
        return connection;
    }

    public void Dispose()
    {
#if NET7_0_OR_GREATER
        _dataSource.Dispose();
#endif
    }

    public async ValueTask DisposeAsync()
    {
#if NET7_0_OR_GREATER
        await _dataSource.DisposeAsync();
#else
        await Task.CompletedTask;
#endif
    }

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Creating a new connection to the database.")]
    public partial void LogCreatingConnection();
}