using GroundControl.Api.Tests.Infrastructure;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Shared.Security;

[Collection("MongoDB")]
public sealed class KeyRingModeSelectionTests(MongoFixture mongoFixture) : ApiHandlerTestBase(mongoFixture)
{
    [Fact]
    public async Task FileSystemMode_IsDefault_StartsSuccessfully()
    {
        // Arrange
        await using var factory = CreateFactory();

        // Act
        var client = factory.CreateClient();
        var response = await client.GetAsync("/healthz/liveness", TestCancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Fact]
    public async Task FileSystemMode_Explicit_StartsSuccessfully()
    {
        // Arrange
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["DataProtection:Mode"] = "FileSystem"
        });

        // Act
        var client = factory.CreateClient();
        var response = await client.GetAsync("/healthz/liveness", TestCancellationToken);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
    }
}