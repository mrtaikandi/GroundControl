using GroundControl.Api.Features.ClientApi;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.ClientApi;

public sealed class SnapshotCacheTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private readonly ISnapshotStore _snapshotStore = Substitute.For<ISnapshotStore>();
    private readonly SnapshotCache _sut;

    public SnapshotCacheTests()
    {
        _sut = new SnapshotCache(_snapshotStore);
    }

    [Fact]
    public async Task GetOrLoadAsync_FirstAccess_LoadsFromStore()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var snapshot = CreateSnapshot(projectId);

        _snapshotStore.GetActiveForProjectAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(snapshot);

        // Act
        var result = await _sut.GetOrLoadAsync(projectId, TestCancellationToken);

        // Assert
        result.ShouldBeSameAs(snapshot);
        await _snapshotStore.Received(1).GetActiveForProjectAsync(projectId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrLoadAsync_SecondAccess_ReturnsCachedWithoutStoreCall()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var snapshot = CreateSnapshot(projectId);

        _snapshotStore.GetActiveForProjectAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(snapshot);

        await _sut.GetOrLoadAsync(projectId, TestCancellationToken);
        _snapshotStore.ClearReceivedCalls();

        // Act
        var result = await _sut.GetOrLoadAsync(projectId, TestCancellationToken);

        // Assert
        result.ShouldBeSameAs(snapshot);
        await _snapshotStore.DidNotReceive().GetActiveForProjectAsync(projectId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrLoadAsync_ProjectWithNoActiveSnapshot_CachesNull()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();

        _snapshotStore.GetActiveForProjectAsync(projectId, Arg.Any<CancellationToken>())
            .Returns((Snapshot?)null);

        await _sut.GetOrLoadAsync(projectId, TestCancellationToken);
        _snapshotStore.ClearReceivedCalls();

        // Act
        var result = await _sut.GetOrLoadAsync(projectId, TestCancellationToken);

        // Assert
        result.ShouldBeNull();
        await _snapshotStore.DidNotReceive().GetActiveForProjectAsync(projectId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_ReloadsFromStore()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var original = CreateSnapshot(projectId);
        var updated = CreateSnapshot(projectId);

        _snapshotStore.GetActiveForProjectAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(original, updated);

        await _sut.GetOrLoadAsync(projectId, TestCancellationToken);

        // Act
        await _sut.InvalidateAsync(projectId, TestCancellationToken);

        // Assert
        var result = await _sut.GetOrLoadAsync(projectId, TestCancellationToken);
        result.ShouldBeSameAs(updated);

        // Store called twice: initial load + invalidation reload (not the final GetOrLoadAsync which is a cache hit)
        await _snapshotStore.Received(2).GetActiveForProjectAsync(projectId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrLoadAsync_DifferentProjects_CachedIndependently()
    {
        // Arrange
        var projectId1 = Guid.CreateVersion7();
        var projectId2 = Guid.CreateVersion7();
        var snapshot1 = CreateSnapshot(projectId1);
        var snapshot2 = CreateSnapshot(projectId2);

        _snapshotStore.GetActiveForProjectAsync(projectId1, Arg.Any<CancellationToken>())
            .Returns(snapshot1);
        _snapshotStore.GetActiveForProjectAsync(projectId2, Arg.Any<CancellationToken>())
            .Returns(snapshot2);

        // Act
        var result1 = await _sut.GetOrLoadAsync(projectId1, TestCancellationToken);
        var result2 = await _sut.GetOrLoadAsync(projectId2, TestCancellationToken);

        // Assert
        result1.ShouldBeSameAs(snapshot1);
        result2.ShouldBeSameAs(snapshot2);
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