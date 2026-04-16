using GroundControl.Api.Features.ClientApi;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.ClientApi;

public sealed class SnapshotCacheWarmupServiceTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private readonly ISnapshotStore _snapshotStore = Substitute.For<ISnapshotStore>();
    private readonly IProjectStore _projectStore = Substitute.For<IProjectStore>();

    [Fact]
    public async Task StartAsync_WarmsProjectsWithActiveSnapshots()
    {
        // Arrange
        var project1 = CreateProject(activeSnapshotId: Guid.CreateVersion7());
        var project2 = CreateProject(activeSnapshotId: null);
        var project3 = CreateProject(activeSnapshotId: Guid.CreateVersion7());

        _projectStore.ListAsync(Arg.Any<ProjectListQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Project>
            {
                Items = [project1, project2, project3],
                TotalCount = 3,
                NextCursor = null,
            });

        var snapshot1 = CreateSnapshot(project1.Id);
        var snapshot3 = CreateSnapshot(project3.Id);

        _snapshotStore.GetActiveForProjectAsync(project1.Id, Arg.Any<CancellationToken>())
            .Returns(snapshot1);
        _snapshotStore.GetActiveForProjectAsync(project3.Id, Arg.Any<CancellationToken>())
            .Returns(snapshot3);

        var cache = new SnapshotCache(_snapshotStore, _projectStore);
        var sut = new SnapshotCacheWarmupService(cache, _projectStore, NullLogger<SnapshotCacheWarmupService>.Instance);

        // Act
        await sut.StartAsync(TestCancellationToken);

        // Assert — only projects with active snapshots were loaded
        await _snapshotStore.Received(1).GetActiveForProjectAsync(project1.Id, Arg.Any<CancellationToken>());
        await _snapshotStore.DidNotReceive().GetActiveForProjectAsync(project2.Id, Arg.Any<CancellationToken>());
        await _snapshotStore.Received(1).GetActiveForProjectAsync(project3.Id, Arg.Any<CancellationToken>());

        // Verify the cache is populated (second call should not hit the store)
        _snapshotStore.ClearReceivedCalls();
        var result = await cache.GetOrLoadAsync(project1.Id, TestCancellationToken);
        result.ShouldBeSameAs(snapshot1);
        await _snapshotStore.DidNotReceive().GetActiveForProjectAsync(project1.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_PaginatesThroughAllProjects()
    {
        // Arrange
        var project1 = CreateProject(activeSnapshotId: Guid.CreateVersion7());
        var project2 = CreateProject(activeSnapshotId: Guid.CreateVersion7());

        _projectStore.ListAsync(Arg.Is<ProjectListQuery>(q => q.After == null), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Project>
            {
                Items = [project1],
                TotalCount = 2,
                NextCursor = "cursor1",
            });

        _projectStore.ListAsync(Arg.Is<ProjectListQuery>(q => q.After == "cursor1"), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Project>
            {
                Items = [project2],
                TotalCount = 2,
                NextCursor = null,
            });

        _snapshotStore.GetActiveForProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => CreateSnapshot((Guid)ci[0]));

        var cache = new SnapshotCache(_snapshotStore, _projectStore);
        var sut = new SnapshotCacheWarmupService(cache, _projectStore, NullLogger<SnapshotCacheWarmupService>.Instance);

        // Act
        await sut.StartAsync(TestCancellationToken);

        // Assert — both pages were fetched
        await _projectStore.Received(2).ListAsync(Arg.Any<ProjectListQuery>(), Arg.Any<CancellationToken>());
        await _snapshotStore.Received(1).GetActiveForProjectAsync(project1.Id, Arg.Any<CancellationToken>());
        await _snapshotStore.Received(1).GetActiveForProjectAsync(project2.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_NoProjects_CompletesWithoutError()
    {
        // Arrange
        _projectStore.ListAsync(Arg.Any<ProjectListQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Project>
            {
                Items = [],
                TotalCount = 0,
                NextCursor = null,
            });

        var cache = new SnapshotCache(_snapshotStore, _projectStore);
        var sut = new SnapshotCacheWarmupService(cache, _projectStore, NullLogger<SnapshotCacheWarmupService>.Instance);

        // Act & Assert — should not throw
        await sut.StartAsync(TestCancellationToken);

        await _snapshotStore.DidNotReceive().GetActiveForProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static Project CreateProject(Guid? activeSnapshotId)
    {
        var now = DateTimeOffset.UtcNow;

        return new Project
        {
            Id = Guid.CreateVersion7(),
            Name = $"Project-{Guid.CreateVersion7():N}",
            ActiveSnapshotId = activeSnapshotId,
            Version = 1,
            CreatedAt = now,
            CreatedBy = Guid.Empty,
            UpdatedAt = now,
            UpdatedBy = Guid.Empty,
        };
    }

    private static Snapshot CreateSnapshot(Guid projectId) => new()
    {
        Id = Guid.CreateVersion7(),
        ProjectId = projectId,
        SnapshotVersion = 1,
        Entries = [],
        PublishedAt = DateTimeOffset.UtcNow,
        PublishedBy = Guid.CreateVersion7(),
    };
}