using GroundControl.Api.Core.ChangeNotification;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.ChangeNotification;

public sealed class MongoChangeStreamNotifierTests
{
    private readonly MongoFixture _mongoFixture;

    public MongoChangeStreamNotifierTests(MongoFixture mongoFixture)
    {
        _mongoFixture = mongoFixture;
    }

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ChangeStream_SnapshotPublish_FiresNotification()
    {
        // Arrange
        var (notifier, collection) = CreateNotifier();
        await notifier.StartAsync(TestCancellationToken);

        try
        {
            var projectId = Guid.CreateVersion7();
            var snapshotId = Guid.CreateVersion7();

            var project = CreateProject(projectId);
            await collection.InsertOneAsync(project, cancellationToken: TestCancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var received = new List<(Guid ProjectId, Guid SnapshotId)>();
            var subscriberTask = Task.Run(async () =>
            {
                await foreach (var item in notifier.SubscribeAsync(cts.Token))
                {
                    received.Add(item);
                    await cts.CancelAsync();
                }
            }, TestCancellationToken);

            // Wait for the change stream to connect before triggering the update
            await TestWaiter.WaitUntilAsync(() => notifier.IsConnected, cancellationToken: TestCancellationToken);

            // Act
            var update = Builders<Project>.Update.Set(p => p.ActiveSnapshotId, snapshotId);
            await collection.UpdateOneAsync(p => p.Id == projectId, update, cancellationToken: TestCancellationToken);

            await IgnoreOperationCanceledException(subscriberTask);

            // Assert
            received.ShouldHaveSingleItem();
            received[0].ProjectId.ShouldBe(projectId);
            received[0].SnapshotId.ShouldBe(snapshotId);
        }
        finally
        {
            await notifier.StopAsync(TestCancellationToken);
            await notifier.DisposeAsync();
        }
    }

    [Fact]
    public async Task ChangeStream_NonSnapshotUpdate_DoesNotFireNotification()
    {
        // Arrange
        var (notifier, collection) = CreateNotifier();
        await notifier.StartAsync(TestCancellationToken);

        try
        {
            var projectId = Guid.CreateVersion7();
            var project = CreateProject(projectId);
            await collection.InsertOneAsync(project, cancellationToken: TestCancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var received = new List<(Guid ProjectId, Guid SnapshotId)>();
            var subscriberTask = Task.Run(async () =>
            {
                await foreach (var item in notifier.SubscribeAsync(cts.Token))
                {
                    received.Add(item);
                }
            }, TestCancellationToken);

            await TestWaiter.WaitUntilAsync(() => notifier.IsConnected, cancellationToken: TestCancellationToken);

            // Act — update description, not activeSnapshotId
            var update = Builders<Project>.Update.Set(p => p.Description, "Updated description");
            await collection.UpdateOneAsync(p => p.Id == projectId, update, cancellationToken: TestCancellationToken);

            await IgnoreOperationCanceledException(subscriberTask);

            // Assert
            received.ShouldBeEmpty();
        }
        finally
        {
            await notifier.StopAsync(TestCancellationToken);
            await notifier.DisposeAsync();
        }
    }

    [Fact]
    public async Task IsConnected_WhenStarted_ReturnsTrue()
    {
        // Arrange
        var (notifier, _) = CreateNotifier();

        try
        {
            // Act
            await notifier.StartAsync(TestCancellationToken);

            // Assert
            await TestWaiter.WaitUntilAsync(() => notifier.IsConnected, cancellationToken: TestCancellationToken);
            notifier.IsConnected.ShouldBeTrue();
        }
        finally
        {
            await notifier.StopAsync(TestCancellationToken);
            await notifier.DisposeAsync();
        }
    }

    [Fact]
    public async Task IsConnected_WhenStopped_ReturnsFalse()
    {
        // Arrange
        var (notifier, _) = CreateNotifier();
        await notifier.StartAsync(TestCancellationToken);
        await TestWaiter.WaitUntilAsync(() => notifier.IsConnected, cancellationToken: TestCancellationToken);

        // Act
        await notifier.StopAsync(TestCancellationToken);

        // Assert
        notifier.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task NotifyAsync_DirectNotification_ReachesSubscribers()
    {
        // Arrange
        var (notifier, _) = CreateNotifier();
        var projectId = Guid.CreateVersion7();
        var snapshotId = Guid.CreateVersion7();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        var received = new List<(Guid ProjectId, Guid SnapshotId)>();

        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var item in notifier.SubscribeAsync(cts.Token))
            {
                received.Add(item);
                await cts.CancelAsync();
            }
        }, TestCancellationToken);

        await Task.Delay(50, TestCancellationToken);

        // Act
        await notifier.NotifyAsync(projectId, snapshotId, TestCancellationToken);

        await IgnoreOperationCanceledException(subscriberTask);

        // Assert
        received.ShouldHaveSingleItem();
        received[0].ProjectId.ShouldBe(projectId);
        received[0].SnapshotId.ShouldBe(snapshotId);

        await notifier.DisposeAsync();
    }

    [Fact]
    public async Task ChangeStream_MultipleUpdates_AllNotificationsReceived()
    {
        // Arrange
        var (notifier, collection) = CreateNotifier();
        await notifier.StartAsync(TestCancellationToken);

        try
        {
            var projectId = Guid.CreateVersion7();
            var snapshotId1 = Guid.CreateVersion7();
            var snapshotId2 = Guid.CreateVersion7();
            var snapshotId3 = Guid.CreateVersion7();

            var project = CreateProject(projectId);
            await collection.InsertOneAsync(project, cancellationToken: TestCancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var received = new List<(Guid ProjectId, Guid SnapshotId)>();
            var subscriberTask = Task.Run(async () =>
            {
                await foreach (var item in notifier.SubscribeAsync(cts.Token))
                {
                    received.Add(item);
                    if (received.Count >= 3)
                    {
                        await cts.CancelAsync();
                    }
                }
            }, TestCancellationToken);

            await TestWaiter.WaitUntilAsync(() => notifier.IsConnected, cancellationToken: TestCancellationToken);

            // Act
            await UpdateActiveSnapshot(collection, projectId, snapshotId1);
            await UpdateActiveSnapshot(collection, projectId, snapshotId2);
            await UpdateActiveSnapshot(collection, projectId, snapshotId3);

            await IgnoreOperationCanceledException(subscriberTask);

            // Assert
            received.Count.ShouldBe(3);
            received[0].SnapshotId.ShouldBe(snapshotId1);
            received[1].SnapshotId.ShouldBe(snapshotId2);
            received[2].SnapshotId.ShouldBe(snapshotId3);
        }
        finally
        {
            await notifier.StopAsync(TestCancellationToken);
            await notifier.DisposeAsync();
        }
    }

    private (MongoChangeStreamNotifier Notifier, IMongoCollection<Project> Collection) CreateNotifier()
    {
        var database = _mongoFixture.CreateDatabase();
        var context = CreateContext(database);
        var notifier = new MongoChangeStreamNotifier(context, NullLogger<MongoChangeStreamNotifier>.Instance);
        var collection = database.GetCollection<Project>("projects");

        return (notifier, collection);
    }

    private MongoDbContext CreateContext(IMongoDatabase database)
    {
        var options = Options.Create(new MongoDbOptions
        {
            ConnectionString = _mongoFixture.ConnectionString,
            DatabaseName = database.DatabaseNamespace.DatabaseName
        });

        return new MongoDbContext(database.Client, options);
    }

    private static Project CreateProject(Guid projectId)
    {
        var timestamp = DateTimeOffset.UtcNow;

        return new Project
        {
            Id = projectId,
            Name = $"Test Project {projectId:N}",
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty
        };
    }

    private static async Task UpdateActiveSnapshot(IMongoCollection<Project> collection, Guid projectId, Guid snapshotId)
    {
        var update = Builders<Project>.Update.Set(p => p.ActiveSnapshotId, snapshotId);
        await collection.UpdateOneAsync(p => p.Id == projectId, update);
    }

    private static async Task IgnoreOperationCanceledException(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }
}