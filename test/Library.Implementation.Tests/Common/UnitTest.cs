using Meziantou.Extensions.Logging.Xunit;

namespace RafaelKallis.Library.Implementation.Tests.Common;

public abstract class UnitTest
{
    protected ITestOutputHelper Output { get; }

    protected UnitTest(ITestOutputHelper output)
    {
        Output = output;
    }

    protected ILogger<T> CreateLogger<T>() => XUnitLogger.CreateLogger<T>(Output);
}