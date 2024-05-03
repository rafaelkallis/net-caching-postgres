# .NET Postgres Cache

![nuget-stable](https://img.shields.io/nuget/v/RafaelKallis.Extensions.Caching.Postgres.svg?label=stable)
![nuget-preview](https://img.shields.io/nuget/vpre/RafaelKallis.Extensions.Caching.Postgres.svg?label=preview)
![net-caching-postgres-build](https://github.com/rafaelkallis/net-caching-postgres/actions/workflows/build.yml/badge.svg)

Distributed cache implementation of [IDistributedCache](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache) using PostgreSQL.

A distributed cache is a cache shared by multiple app servers, typically maintained as an external service to the app servers that access it. A distributed cache can improve the performance and scalability of an ASP.NET Core app, especially when the app is hosted by a cloud service or a server farm.

You can read more about distributed caching [here](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/distributed).

## Getting started

### Step 1: Install Package

Add a reference to the [RafaelKallis.Extensions.Caching.Postgres](https://www.nuget.org/packages/RafaelKallis.Extensions.Caching.Postgres) package.

```sh
dotnet add package RafaelKallis.Extensions.Caching.Postgres
```

### Step 2: Enable Postgres Cache

The following code enables the Postgres Cache in your application:

```csharp
builder.Services.AddDistributedPostgresCache(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("Database");
    options.SchemaName = "public";
    options.TableName = "__CacheEntries";
});
```

### Configuration

The following options are available:
- `ConnectionString`: The connection string to the PostgreSQL database.
- `SchemaName` (`"public"`): The schema name where the cache table is located.
- `TableName` (`"__CacheEntries"`): The name of the cache table.
- `Owner` (`"CURRENT_USER"`): The owner of the cache table.
- `KeyMaxLength` (`1024`): The maximum length of the cache key.
- `MigrateOnStart` (`true`): Whether to automatically migrate the database on application start.
- `UseUnloggedTable` (`false`): Whether to create the cache table as an [unlogged table](https://pganalyze.com/blog/5mins-postgres-unlogged-tables).
- `DefaultSlidingExpiration` (`20 minutes`): The default sliding expiration for cache entries.
- `GarbageCollectionInterval` (`30 minutes`): The interval at which the garbage collection runs.

## OpenTelemetry

![nuget-stable](https://img.shields.io/nuget/v/RafaelKallis.Extensions.Caching.Postgres.OpenTelemetry.svg?label=stable)
![nuget-preview](https://img.shields.io/nuget/vpre/RafaelKallis.Extensions.Caching.Postgres.OpenTelemetry.svg?label=preview)

[OpenTelemetry](https://opentelemetry.io) is a widely-adopted framework for distributed observability across many languages and components. Its tracing standards allow applications and libraries to emit information on activities and events, which can be exported by the application, stored and analyzed. Activities typically have start and end times, and can encompass other activities recursivelyr. This allows you to analyze e.g. exactly how much time was spent in the database when handling a certain HTTP call.

### Step 1: Install Package

Add a reference to the [RafaelKallis.Extensions.Caching.Postgres.OpenTelemetry](https://www.nuget.org/packages/RafaelKallis.Extensions.Caching.Postgres.OpenTelemetry) package.

```sh
dotnet add package RafaelKallis.Extensions.Caching.Postgres.OpenTelemetry
```

### Step 2: Enable Instrumentation

The following code enables the OpenTelemetry instrumentation for the Postgres Caching in your application:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddDistributedPostgresCacheInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddDistributedPostgresCacheInstrumentation()
        .AddConsoleExporter());
```

### Available Metrics

| Name | Description | Units | Instrument Type | Value Type | Attributes |
|---|---|---|---|---|---|
| `cache.operation.count` | The number of cache operations | {operation} | Counter | Int64 | `cache.operation.type` (`get`, `set`, `refresh`, `remove`); `cache.operation.key` |
| `cache.operation.duration` | The duration of cache operations | ms | Histogram | Int64 | `cache.operation.type` (`get`, `set`, `refresh`, `remove`), `cache.operation.key` |
| `cache.operation.io` | The amount of bytes read and written during cache operations | By | Histogram | Int64 | `cache.operation.type` (`get`, `set`, `refresh`, `remove`), `cache.operation.key` |
| `cache.hit_ratio` | The hit ratio of cache | ObservableGauge | Double |
| `cache.gc.count` | The number of garbage collections | {run} | Counter | Int64 |
| `cache.gc.duration` | The duration of garbage collections | ms | Histogram | Int64 |
| `cache.gc.removed_entries` | The number of entries that were removed during garbage collection, due to expiration | {entry} | Histogram | Int64
