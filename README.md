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
- `MigrationsHistoryTableName` (`"__CacheMigrationsHistory"`): The name of the cache table.
- `Owner` (`"CURRENT_USER"`): The owner of the cache table.
- `KeyMaxLength` (`1024`): The maximum length of the cache key.
- `UsePreparedStatements` (`true`): Whether to use prepared statements for cache operations.
- `MigrateOnStart` (`true`): Whether to automatically migrate the database on application start.
- `UseUnloggedTable` (`false`): Whether to create the cache table as an [unlogged table](https://pganalyze.com/blog/5mins-postgres-unlogged-tables).
- `DefaultSlidingExpiration` (`20 minutes`): The default sliding expiration for cache entries.
- `GarbageCollectionInterval` (`30 minutes`): The interval at which the garbage collection runs.

### PgBouncer

If you are using [PgBouncer](https://www.pgbouncer.org) as a connection pooler with the transaction pooling mode, some additional configuration is required.

If you are using pgbouncer version 1.21 or later:
- Set the `max_prepared_transactions`to a value greater than 0 in your PgBouncer configuration.
- Add `No Reset On Close=true;` to your connection string.

For older PgBouncer versions:
- Set `UsePreparedStatements` to `false` in the Postgres Cache configuration.

Additional references for PgBouncer configuration:
- [PgBouncer `max prepared statements` Documentation](https://www.pgbouncer.org/config.html#max_prepared_statements)
- [Npgsql PgBouncer Documentation](https://www.npgsql.org/doc/compatibility.html#pgbouncer)
- [Postgres Release Announcement](https://www.postgresql.org/about/news/pgbouncer-1210-released-now-with-prepared-statements-2735/)
- [Google Results](https://www.google.com/search?q=pgbouncer+prepared+statements)

Make sure the connection string contains `No Reset On Close=true`.

```csharp

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
| `cache.operation.duration` | The duration of cache operations | s | Histogram | Int64 | `cache.operation.type` (`get`, `set`, `refresh`, `remove`), `cache.operation.key` |
| `cache.operation.io` | The amount of bytes read and written during cache operations | By | Histogram | Int64 | `cache.operation.type` (`get`, `set`, `refresh`, `remove`), `cache.operation.key` |
| `cache.hit_ratio` | The hit ratio of the cache | ObservableGauge | Double |
| `cache.gc.count` | The number of garbage collections | {run} | Counter | Int64 |
| `cache.gc.duration` | The duration of garbage collections | s | Histogram | Int64 |
| `cache.gc.removed_entries` | The number of entries that were removed during garbage collection, due to expiration | {entry} | Histogram | Int64

## Health Checks

![nuget-stable](https://img.shields.io/nuget/v/RafaelKallis.Extensions.Caching.Postgres.HealthChecks.svg?label=stable)
![nuget-preview](https://img.shields.io/nuget/vpre/RafaelKallis.Extensions.Caching.Postgres.HealthChecks.svg?label=preview)

[Health checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks) are typically used with an external monitoring service or container orchestrator to check the status of an app. Before adding health checks to an app, decide on which monitoring system to use. The monitoring system dictates what types of health checks to create and how to configure their endpoints.

### Step 1: Install Package

Add a reference to the [RafaelKallis.Extensions.Caching.Postgres.HealthChecks](https://www.nuget.org/packages/RafaelKallis.Extensions.Caching.Postgres.HealthChecks) package.

```sh
dotnet add package RafaelKallis.Extensions.Caching.Postgres.HealthChecks
```

### Step 2: Enable Health Checks

The following code enables the health checks for the Postgres Cache in your application:

```csharp
builder.Services.AddHealthChecks()
    .AddDistributedPostgresCacheHealthCheck(options => 
    {
        options.DegradedTimeout = TimeSpan.FromMilliseconds(500);
        options.UnhealthyTimeout = TimeSpan.FromSeconds(30);
    });
```