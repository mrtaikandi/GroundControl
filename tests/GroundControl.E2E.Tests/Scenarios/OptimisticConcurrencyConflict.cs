using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.E2E.Tests.Infrastructure;
using Shouldly;

namespace GroundControl.E2E.Tests.Scenarios;

/// <summary>
/// End-to-end workflow verifying optimistic concurrency control: updates with a
/// stale version are rejected with 409 Conflict.
/// </summary>
public sealed class OptimisticConcurrencyConflict : EndToEndTestBase
{
    private const string ProjectIdKey = "ProjectId";
    private const string InitialVersionKey = "InitialVersion";
    private const string UpdatedVersionKey = "UpdatedVersion";

    public OptimisticConcurrencyConflict(AspireFixture fixture)
        : base(fixture) { }

    [Fact, Step(1)]
    public Task Step01_CreateProject() => RunStep(1, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "project", "create",
            "--name", "Concurrency Test Project");

        // Assert
        result.ShouldSucceed();
        var project = result.ParseOutput<ProjectResponse>();
        project.Id.ShouldNotBe(Guid.Empty);
        project.Name.ShouldBe("Concurrency Test Project");

        Set(ProjectIdKey, project.Id);
    });

    [Fact, Step(2)]
    public Task Step02_VerifyInitialVersion() => RunStep(2, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);

        // Act
        var project = await ApiClient.GetProjectHandlerAsync(projectId, TestCancellationToken);

        // Assert
        project.Version.ShouldBe(1);
        project.Name.ShouldBe("Concurrency Test Project");

        Set(InitialVersionKey, project.Version);
    });

    [Fact, Step(3)]
    public Task Step03_UpdateWithCorrectVersion() => RunStep(3, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);
        var initialVersion = Get<long>(InitialVersionKey);

        var request = new UpdateProjectRequest
        {
            Name = "Concurrency Test Project - Updated",
            Description = "Updated in step 3",
        };

        // Act
        GroundControlClient.SetIfMatch(initialVersion);
        var updated = await ApiClient.UpdateProjectHandlerAsync(projectId, request, TestCancellationToken);

        // Assert
        updated.Name.ShouldBe("Concurrency Test Project - Updated");
        updated.Version.ShouldBe(initialVersion + 1);

        Set(UpdatedVersionKey, updated.Version);
    });

    [Fact, Step(4)]
    public Task Step04_UpdateWithStaleVersionFails() => RunStep(4, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);
        var staleVersion = Get<long>(InitialVersionKey);

        var request = new UpdateProjectRequest
        {
            Name = "Should Not Be Applied",
            Description = "This update uses a stale version",
        };

        // Act
        GroundControlClient.SetIfMatch(staleVersion);
        var ex = await Should.ThrowAsync<GroundControlApiClientException>(async () =>
            await ApiClient.UpdateProjectHandlerAsync(projectId, request, TestCancellationToken));

        // Assert
        ex.StatusCode.ShouldBe(409);
    });

    [Fact, Step(5)]
    public Task Step05_VerifySuccessfulUpdatePersisted() => RunStep(5, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);
        var expectedVersion = Get<long>(UpdatedVersionKey);

        // Act
        var project = await ApiClient.GetProjectHandlerAsync(projectId, TestCancellationToken);

        // Assert
        project.Name.ShouldBe("Concurrency Test Project - Updated");
        project.Description.ShouldBe("Updated in step 3");
        project.Version.ShouldBe(expectedVersion);
    });
}