using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.ConfigEntries.Contracts;
using GroundControl.Api.Features.Projects.Contracts;
using GroundControl.Api.Features.Snapshots;
using GroundControl.Persistence.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Shouldly;
using Xunit;
using ScopedValueRequest = GroundControl.Api.Features.ConfigEntries.Contracts.ScopedValueRequest;

namespace GroundControl.Api.Tests.Retention;

public sealed class SnapshotRetentionTests : ApiHandlerTestBase
{
    public SnapshotRetentionTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task Publish_WhenSnapshotsExceedRetentionCount_DeletesOldestSnapshots()
    {
        // Arrange
        var retentionCount = 5;
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Snapshots:RetentionCount"] = retentionCount.ToString()
        });

        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "key1", project.Id, value: "val1");

        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();

        // Act — publish more snapshots than the retention count
        var totalPublishes = retentionCount + 3;
        for (var i = 0; i < totalPublishes; i++)
        {
            var result = await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);
            result.Result.ShouldBeOfType<Created<Snapshot>>();
        }

        // Assert — only retentionCount snapshots remain
        var snapshotCollection = factory.Database.GetCollection<Snapshot>("snapshots");
        var remaining = await snapshotCollection
            .Find(s => s.ProjectId == project.Id)
            .ToListAsync(TestCancellationToken);

        remaining.Count.ShouldBe(retentionCount);
    }

    [Fact]
    public async Task Publish_ActiveSnapshotIsNeverDeleted()
    {
        // Arrange
        var retentionCount = 3;
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Snapshots:RetentionCount"] = retentionCount.ToString()
        });

        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "key1", project.Id, value: "val1");

        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();

        // Act — publish more than retention count
        Guid lastSnapshotId = default;
        for (var i = 0; i < retentionCount + 5; i++)
        {
            var result = await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);
            var created = result.Result.ShouldBeOfType<Created<Snapshot>>();
            lastSnapshotId = created.Value!.Id;
        }

        // Assert — the active (last published) snapshot always exists
        var snapshotCollection = factory.Database.GetCollection<Snapshot>("snapshots");
        var activeSnapshot = await snapshotCollection
            .Find(s => s.Id == lastSnapshotId)
            .FirstOrDefaultAsync(TestCancellationToken);

        activeSnapshot.ShouldNotBeNull();
    }

    [Fact]
    public async Task Publish_WhenSnapshotsAtRetentionCount_NothingDeleted()
    {
        // Arrange
        var retentionCount = 5;
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Snapshots:RetentionCount"] = retentionCount.ToString()
        });

        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "key1", project.Id, value: "val1");

        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();

        // Act — publish exactly the retention count
        for (var i = 0; i < retentionCount; i++)
        {
            var result = await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);
            result.Result.ShouldBeOfType<Created<Snapshot>>();
        }

        // Assert — all snapshots remain
        var snapshotCollection = factory.Database.GetCollection<Snapshot>("snapshots");
        var remaining = await snapshotCollection
            .Find(s => s.ProjectId == project.Id)
            .ToListAsync(TestCancellationToken);

        remaining.Count.ShouldBe(retentionCount);
    }

    [Fact]
    public async Task Publish_WhenRetentionCountIsZero_NoSnapshotsDeleted()
    {
        // Arrange
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Snapshots:RetentionCount"] = "0"
        });

        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "key1", project.Id, value: "val1");

        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();

        // Act — publish several snapshots
        var totalPublishes = 10;
        for (var i = 0; i < totalPublishes; i++)
        {
            var result = await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);
            result.Result.ShouldBeOfType<Created<Snapshot>>();
        }

        // Assert — all snapshots remain (retention disabled)
        var snapshotCollection = factory.Database.GetCollection<Snapshot>("snapshots");
        var remaining = await snapshotCollection
            .Find(s => s.ProjectId == project.Id)
            .ToListAsync(TestCancellationToken);

        remaining.Count.ShouldBe(totalPublishes);
    }

    [Fact]
    public async Task Publish_RetentionOnlyAffectsTargetProject()
    {
        // Arrange
        var retentionCount = 3;
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Snapshots:RetentionCount"] = retentionCount.ToString()
        });

        using var apiClient = factory.CreateClient();
        var projectA = await CreateProjectAsync(apiClient);
        var projectB = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "key1", projectA.Id, value: "val1");
        await CreateConfigEntryAsync(apiClient, "key1", projectB.Id, value: "val1");

        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();

        // Publish exactly retentionCount snapshots to project B (all retained)
        for (var i = 0; i < retentionCount; i++)
        {
            await publisher.PublishAsync(projectB.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);
        }

        // Act — publish more than retentionCount to project A (triggers retention for A only)
        for (var i = 0; i < retentionCount + 3; i++)
        {
            await publisher.PublishAsync(projectA.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);
        }

        // Assert — project A trimmed to retention count, project B still has all its snapshots
        var snapshotCollection = factory.Database.GetCollection<Snapshot>("snapshots");

        var remainingA = await snapshotCollection
            .Find(s => s.ProjectId == projectA.Id)
            .ToListAsync(TestCancellationToken);

        var remainingB = await snapshotCollection
            .Find(s => s.ProjectId == projectB.Id)
            .ToListAsync(TestCancellationToken);

        remainingA.Count.ShouldBe(retentionCount);
        remainingB.Count.ShouldBe(retentionCount);
    }

    [Fact]
    public async Task Publish_DefaultRetentionCountIs50()
    {
        // Arrange — no Snapshots:RetentionCount configured, defaults to 50
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var project = await CreateProjectAsync(apiClient);
        await CreateConfigEntryAsync(apiClient, "key1", project.Id, value: "val1");

        var publisher = factory.Services.GetRequiredService<SnapshotPublisher>();

        // Act — publish 52 snapshots
        for (var i = 0; i < 52; i++)
        {
            await publisher.PublishAsync(project.Id, Guid.CreateVersion7(), cancellationToken: TestCancellationToken);
        }

        // Assert — only 50 remain (default retention)
        var snapshotCollection = factory.Database.GetCollection<Snapshot>("snapshots");
        var remaining = await snapshotCollection
            .Find(s => s.ProjectId == project.Id)
            .ToListAsync(TestCancellationToken);

        remaining.Count.ShouldBe(50);
    }

    #region Test Helpers

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient apiClient)
    {
        var request = new CreateProjectRequest
        {
            Name = $"Project-{Guid.CreateVersion7():N}",
            Description = "Test project",
        };

        var response = await apiClient.PostAsJsonAsync("/api/projects", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(WebJsonSerializerOptions, TestCancellationToken);
        project.ShouldNotBeNull();

        return project;
    }

    private static async Task CreateConfigEntryAsync(
        HttpClient apiClient,
        string key,
        Guid ownerId,
        string value = "default")
    {
        var request = new CreateConfigEntryRequest
        {
            Key = key,
            OwnerId = ownerId,
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = [new ScopedValueRequest { Value = value }],
        };

        var response = await apiClient.PostAsJsonAsync("/api/config-entries", request, WebJsonSerializerOptions, TestCancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    #endregion
}