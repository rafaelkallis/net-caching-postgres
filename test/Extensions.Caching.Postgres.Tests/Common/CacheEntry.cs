namespace RafaelKallis.Extensions.Caching.Postgres.Tests.Common;

public sealed record CacheEntry(
    string Key,
    ICollection<byte> Value,
    DateTimeOffset ExpiresAt,
    TimeSpan? SlidingExpiration,
    DateTimeOffset? AbsoluteExpiration);