using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Npgsql;

namespace RafaelKallis.Extensions.Caching.Postgres.Benchmarks;

public abstract class Benchmark
{
    private const string DefaultHost = "127.0.0.1";
    private const string DefaultDatabase = "postgres";
    private const string DefaultUsername = "postgres";
    private const string DefaultPassword = "pAssw0rd";

    private string _databaseName = string.Empty;
    private string _connectionString = string.Empty;

    private IHost _host = null!;
    protected PostgresCache PostgresCache { get; private set; } = null!;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _connectionString = CreateConnectionString();

        _databaseName = $"benchmark_{DateTime.UtcNow:o}";
        await ExecuteSqlAsync($"CREATE DATABASE \"{_databaseName}\"");

        _connectionString = CreateConnectionString(database: _databaseName);

        IHostBuilder builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(TimeProvider.System);
            services.AddDistributedPostgresCache(options =>
            {
                options.ConnectionString = _connectionString;
                options.EnableGarbageCollection = false;
                options.UseUnloggedTable = true;
            });
        });
        _host = builder.Build();

        await _host.StartAsync();

        PostgresCache = _host.Services.GetRequiredService<PostgresCache>();

        await DoGlobalSetup();
    }

    protected virtual Task DoGlobalSetup() => Task.CompletedTask;

    [IterationSetup]
    public void IterationSetup() => DoIterationSetup();

    protected virtual void DoIterationSetup() { }

    [IterationCleanup]
    public virtual void IterationCleanup() => DoIterationCleanup();

    protected virtual void DoIterationCleanup() { }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        await DoGlobalCleanup();
        await _host.StopAsync();
        _host.Dispose();

        _connectionString = CreateConnectionString();
        await ExecuteSqlAsync($"DROP DATABASE \"{_databaseName}\" (FORCE)");
    }

    protected virtual Task DoGlobalCleanup() => Task.CompletedTask;

    protected async Task TruncateTableAsync() =>
        await ExecuteSqlAsync($"TRUNCATE TABLE \"{PostgresCacheConstants.DefaultSchemaName}\".\"{PostgresCacheConstants.DefaultTableName}\"");

    private async Task ExecuteSqlAsync(string sql)
    {
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string CreateConnectionString(string host = DefaultHost, string database = DefaultDatabase, string username = DefaultUsername, string password = DefaultPassword) =>
        $"Host={host};Database={database};Username={username};Password={password};Include Error Detail=true";
}