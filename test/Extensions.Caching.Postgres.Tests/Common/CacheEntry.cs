namespace RafaelKallis.Extensions.Caching.Postgres.Tests.Common;

public record CacheEntry(
    string Key,
    byte[] Value,
    DateTime ExpiresAt,
    TimeSpan? SlidingExpiration,
    DateTime? AbsoluteExpiration);