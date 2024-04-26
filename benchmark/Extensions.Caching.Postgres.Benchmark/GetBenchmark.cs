using Microsoft.Extensions.Caching.Distributed;

namespace RafaelKallis.Extensions.Caching.Postgres.Benchmark;

public class GetBenchmark : Benchmark
{
    private const int Rows = 10_000;
    private const int OpsPerIteration = 300;
    private const int ValueSize = 1024;

    private readonly static DistributedCacheEntryOptions Options =
        new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(10));

    private readonly string[] _keys = new string[OpsPerIteration];
    private readonly byte[][] _values = new byte[OpsPerIteration][];

    protected override async Task DoGlobalSetup()
    {
        for (int i = 0; i < Rows; i++)
        {
            string key = Guid.NewGuid().ToString();
            byte[] value = new byte[ValueSize];
            Random.Shared.NextBytes(value);
            await PostgresCache.SetAsync(key, value, Options, CancellationToken.None);
        }
    }

    protected override void DoIterationSetup()
    {
        for (int i = 0; i < OpsPerIteration; i++)
        {
            _keys[i] = Guid.NewGuid().ToString();
            _values[i] = new byte[ValueSize];
            Random.Shared.NextBytes(_values[i]);
            PostgresCache.Set(_keys[i], _values[i], Options);
        }
    }

    public override void IterationCleanup()
    {
        for (int i = 0; i < OpsPerIteration; i++)
        {
            PostgresCache.Remove(_keys[i]);
        }
    }

    protected override async Task DoGlobalCleanup()
    {
        await TruncateTableAsync();
    }

    [Benchmark]
    public async Task GetAsync()
    {
        for (int i = 0; i < OpsPerIteration; i++)
        {
            byte[]? _ = await PostgresCache.GetAsync(_keys[i], CancellationToken.None);
        }
    }
}