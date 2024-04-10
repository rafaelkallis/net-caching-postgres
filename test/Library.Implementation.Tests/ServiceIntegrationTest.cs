using System.Globalization;

using Microsoft.AspNetCore.Mvc;

namespace RafaelKallis.Library.Implementation.Tests;

public class ServiceIntegrationTest : IntegrationTest
{
    public ServiceIntegrationTest(ITestOutputHelper output) : base(output)
    { }

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<Service>();
    }

    protected override void ConfigureWebApplication(WebApplication app)
    {
        app.MapPost("/add-one/{value:int}",
            ([FromRoute] int value, [FromServices] Service service) => service.AddOne(value));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    public async Task ShouldAddOne(int parameter, int expectedResult)
    {
        using HttpResponseMessage response = await Client.PostAsync($"/add-one/{parameter}", new StringContent(""));
        response.Should().BeSuccessful();
        string responseContent = await response.Content.ReadAsStringAsync();
        int.TryParse(responseContent, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result).Should().BeTrue();
        result.Should().Be(expectedResult);
    }
}