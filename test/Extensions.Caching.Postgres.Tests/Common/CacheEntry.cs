namespace RafaelKallis.Extensions.Caching.Postgres.Tests.Common;

public record CacheEntry(
    string Key,
    byte[] Value,
    DateTimeOffset ExpiresAt,
    TimeSpan? SlidingExpiration,
    DateTimeOffset? AbsoluteExpiration);