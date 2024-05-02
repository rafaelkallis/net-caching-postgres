using BenchmarkDotNet.Running;

namespace RafaelKallis.Extensions.Caching.Postgres.Benchmarks;

public sealed class Program
{
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args: args);
}