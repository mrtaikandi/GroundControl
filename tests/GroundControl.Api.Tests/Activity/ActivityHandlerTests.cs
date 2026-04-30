using System.Net;
using GroundControl.Api.Features.Activity.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Activity;

public sealed class ActivityHandlerTests : ApiHandlerTestBase
{
    public ActivityHandlerTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task GetActivitySummary_ReturnsCurrentActivitySnapshot()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();

        // Act
        var response = await apiClient.GetAsync("/activity/summary", TestCancellationToken);
        var summary = await ReadRequiredJsonAsync<ActivitySummaryResponse>(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        summary.Clients.ShouldBe(0);
        summary.Rate.ShouldBe(0);
    }
}