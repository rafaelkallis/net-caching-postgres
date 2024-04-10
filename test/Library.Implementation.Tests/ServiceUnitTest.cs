
using RafaelKallis.Library.Implementation.Tests.Common;

namespace RafaelKallis.Library.Implementation.Tests;

public class ServiceUnitTest : UnitTest
{
    public ServiceUnitTest(ITestOutputHelper output) : base(output)
    { }

    private Service CreateService() =>
        new(logger: CreateLogger<Service>());

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    public void ShouldAddOne(int parameter, int expectedResult)
    {
        using Service service = CreateService();
        service.AddOne(parameter).Should().Be(expectedResult);
    }
}