using BenchmarkDotNet.Attributes;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RafaelKallis.Extensions.Caching.Postgres.Benchmark;

public class SetBenchmarks
{
    private const string DefaultHost = "127.0.0.1";
    private const string DefaultDatabase = "postgres";
    private const string DefaultUsername = "postgres";
    private const string DefaultPassword = "pAssw0rd";

    private IHost _host = null!;
    private PostgresCache _postgresCache = null!;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        IHostBuilder builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(TimeProvider.System);
            services.AddDistributedPostgresCache(options =>
            {
                options.ConnectionString = CreateConnectionString();
                options.EnableGarbageCollection = false;
            });
        });
        _host = builder.Build();

        await _host.StartAsync();

        _postgresCache = _host.Services.GetRequiredService<PostgresCache>();
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    private static string CreateConnectionString(string host = DefaultHost, string database = DefaultDatabase, string username = DefaultUsername, string password = DefaultPassword) =>
        $"Host={host};Database={database};Username={username};Password={password};Include Error Detail=true;Maximum Pool Size=20";

    [Benchmark]
    public async Task SetAsync()
    {
        string key = Guid.NewGuid().ToString();
        byte[] value = new byte[1024];
        Random.Shared.NextBytes(value);

        await _postgresCache.SetAsync(key, value);
    }

}