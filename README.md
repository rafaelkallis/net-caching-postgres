# Postgres Distributed Caching

![nuget-stable](https://img.shields.io/nuget/v/RafaelKallis.Extensions.Caching.Postgres.svg?label=stable)
![nuget-preview](https://img.shields.io/nuget/vpre/RafaelKallis.Extensions.Caching.Postgres.svg?label=preview)
![net-caching-postgres-build](https://github.com/rafaelkallis/net-caching-postgres/actions/workflows/build.yml/badge.svg)

Distributed cache implementation of [Microsoft.Extensions.Caching.Distributed.IDistributedCache](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache) using PostgreSQL.

You can read more about distributed caching [here](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/distributed).

## Getting started

The following code enables the Postgres Caching in your application:

```csharp
builder.Services.AddDistributedPostgresCache(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("Database");
    options.SchemaName = "public";
    options.TableName = "__CacheEntries";
});
```

## Configuration

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
