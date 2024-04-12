namespace RafaelKallis.Extensions.Caching.Postgres.Tests.Common;

public record CacheItem(
    string Key,
    byte[] Value,
    DateTimeOffset ExpiresAtTime,
    long? SlidingExpirationInSeconds,
    DateTimeOffset? AbsoluteExpiration);