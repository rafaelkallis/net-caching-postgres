using JetBrains.Annotations;

namespace RafaelKallis.Library.Abstractions;

/// <summary>
/// Service.
/// </summary>
[PublicAPI]
public interface IService
{
    /// <summary>
    /// Does something.
    /// </summary>
    public int AddOne(int value);
}