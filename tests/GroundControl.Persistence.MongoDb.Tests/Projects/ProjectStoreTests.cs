using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Conventions;
using GroundControl.Persistence.MongoDb.Stores;
using GroundControl.Persistence.MongoDb.Tests.Infrastructure;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace GroundControl.Persistence.MongoDb.Tests.Projects;

[Collection("MongoDB")]
public sealed class ProjectStoreTests
{
    private readonly MongoFixture _mongoFixture;

    public ProjectStoreTests(MongoFixture mongoFixture)
    {
        _mongoFixture = mongoFixture;
    }

    [Fact]
    public async Task ListAsync_WithForwardAndBackwardPagination_ReturnsExpectedPages()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        await store.CreateAsync(CreateProject("Gamma"), cancellationToken);
        await store.CreateAsync(CreateProject("Alpha"), cancellationToken);
        await store.CreateAsync(CreateProject("Beta"), cancellationToken);

        // Act
        var firstPage = await store.ListAsync(new ProjectListQuery
        {
            Limit = 2,
            SortField = "name",
            SortOrder = "asc"
        }, cancellationToken);

        var secondPage = await store.ListAsync(new ProjectListQuery
        {
            Limit = 2,
            After = firstPage.NextCursor,
            SortField = "name",
            SortOrder = "asc"
        }, cancellationToken);

        var previousPage = await store.ListAsync(new ProjectListQuery
        {
            Limit = 2,
            Before = secondPage.PreviousCursor,
            SortField = "name",
            SortOrder = "asc"
        }, cancellationToken);

        // Assert
        firstPage.Items.Select(project => project.Name).ShouldBe(["Alpha", "Beta"]);
        firstPage.NextCursor.ShouldNotBeNull();
        firstPage.PreviousCursor.ShouldBeNull();
        firstPage.TotalCount.ShouldBe(3);

        secondPage.Items.Select(project => project.Name).ShouldBe(["Gamma"]);
        secondPage.NextCursor.ShouldBeNull();
        secondPage.PreviousCursor.ShouldNotBeNull();
        secondPage.TotalCount.ShouldBe(3);

        previousPage.Items.Select(project => project.Name).ShouldBe(["Alpha", "Beta"]);
        previousPage.NextCursor.ShouldNotBeNull();
        previousPage.PreviousCursor.ShouldBeNull();
        previousPage.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task ListAsync_WithGroupIdAndSearch_ReturnsOnlyMatchingProjectsInGroup()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, _) = await CreateStoreAsync(cancellationToken);
        var targetGroupId = Guid.CreateVersion7();
        var otherGroupId = Guid.CreateVersion7();

        await store.CreateAsync(CreateProject("Billing API", targetGroupId), cancellationToken);
        await store.CreateAsync(CreateProject("Checkout", targetGroupId, "Handles billing workflows"), cancellationToken);
        await store.CreateAsync(CreateProject("Billing Portal", otherGroupId), cancellationToken);
        await store.CreateAsync(CreateProject("Inventory", targetGroupId, "Warehouse operations"), cancellationToken);

        // Act
        var result = await store.ListAsync(new ProjectListQuery
        {
            GroupId = targetGroupId,
            Search = "billing",
            SortField = "name",
            SortOrder = "asc"
        }, cancellationToken);

        // Assert
        result.Items.Select(project => project.Name).ShouldBe(["Billing API", "Checkout"]);
        result.TotalCount.ShouldBe(2);
    }

    private async Task<(ProjectStore Store, IMongoDatabase Database)> CreateStoreAsync(CancellationToken cancellationToken)
    {
        var database = _mongoFixture.CreateDatabase();
        var context = _mongoFixture.CreateContext(database);
        var configuration = new ProjectConfiguration(context);

        await configuration.ConfigureAsync(cancellationToken).ConfigureAwait(false);

        return (new ProjectStore(context), database);
    }

    private static Project CreateProject(string name, Guid? groupId = null, string? description = null)
    {
        var timestamp = DateTimeOffset.UtcNow;

        return new Project
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Description = description ?? $"{name} project",
            GroupId = groupId,
            TemplateIds = [],
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty
        };
    }
}