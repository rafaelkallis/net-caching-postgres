using BenchmarkDotNet.Running;

using RafaelKallis.Extensions.Caching.Postgres.Benchmark;

var summary = BenchmarkRunner.Run<SetBenchmarks>();