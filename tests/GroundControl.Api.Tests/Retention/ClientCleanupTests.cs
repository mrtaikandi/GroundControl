using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Retention;

public sealed class ClientCleanupTests : ApiHandlerTestBase
{
    public ClientCleanupTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task GetExpiredAndDeactivatedAsync_ReturnsInactiveClientsPastGracePeriod()
    {
        // Arrange
        await using var factory = CreateFactory();
        var clientCollection = factory.Database.GetCollection<Client>("clients");
        var clientStore = factory.Services.GetRequiredService<IClientStore>();
        var projectId = Guid.CreateVersion7();

        var expiredClient = CreateClientEntity(projectId, isActive: false, updatedAt: DateTimeOffset.UtcNow.AddDays(-60));
        await clientCollection.InsertOneAsync(expiredClient, cancellationToken: TestCancellationToken);

        // Act
        var result = await clientStore.GetExpiredAndDeactivatedAsync(30, TestCancellationToken);

        // Assert
        result.ShouldContain(c => c.Id == expiredClient.Id);
    }

    [Fact]
    public async Task GetExpiredAndDeactivatedAsync_DoesNotReturnActiveClients()
    {
        // Arrange
        await using var factory = CreateFactory();
        var clientCollection = factory.Database.GetCollection<Client>("clients");
        var clientStore = factory.Services.GetRequiredService<IClientStore>();
        var projectId = Guid.CreateVersion7();

        var activeClient = CreateClientEntity(projectId, isActive: true, updatedAt: DateTimeOffset.UtcNow.AddDays(-60));
        await clientCollection.InsertOneAsync(activeClient, cancellationToken: TestCancellationToken);

        // Act
        var result = await clientStore.GetExpiredAndDeactivatedAsync(30, TestCancellationToken);

        // Assert
        result.ShouldNotContain(c => c.Id == activeClient.Id);
    }

    [Fact]
    public async Task GetExpiredAndDeactivatedAsync_DoesNotReturnInactiveClientsWithinGracePeriod()
    {
        // Arrange
        await using var factory = CreateFactory();
        var clientCollection = factory.Database.GetCollection<Client>("clients");
        var clientStore = factory.Services.GetRequiredService<IClientStore>();
        var projectId = Guid.CreateVersion7();

        var recentlyDeactivated = CreateClientEntity(projectId, isActive: false, updatedAt: DateTimeOffset.UtcNow.AddDays(-10));
        await clientCollection.InsertOneAsync(recentlyDeactivated, cancellationToken: TestCancellationToken);

        // Act
        var result = await clientStore.GetExpiredAndDeactivatedAsync(30, TestCancellationToken);

        // Assert
        result.ShouldNotContain(c => c.Id == recentlyDeactivated.Id);
    }

    [Fact]
    public async Task HardDeleteAsync_PermanentlyRemovesClient()
    {
        // Arrange
        await using var factory = CreateFactory();
        var clientCollection = factory.Database.GetCollection<Client>("clients");
        var clientStore = factory.Services.GetRequiredService<IClientStore>();
        var projectId = Guid.CreateVersion7();

        var client = CreateClientEntity(projectId, isActive: false, updatedAt: DateTimeOffset.UtcNow.AddDays(-60));
        await clientCollection.InsertOneAsync(client, cancellationToken: TestCancellationToken);

        // Act
        await clientStore.HardDeleteAsync(client.Id, TestCancellationToken);

        // Assert
        var remaining = await clientCollection.Find(c => c.Id == client.Id).FirstOrDefaultAsync(TestCancellationToken);
        remaining.ShouldBeNull();
    }

    [Fact]
    public async Task CleanupFlow_OnlyDeletesExpiredInactiveClients()
    {
        // Arrange
        await using var factory = CreateFactory();
        var clientCollection = factory.Database.GetCollection<Client>("clients");
        var clientStore = factory.Services.GetRequiredService<IClientStore>();
        var projectId = Guid.CreateVersion7();
        var gracePeriodDays = 30;

        var expiredInactive = CreateClientEntity(projectId, isActive: false, updatedAt: DateTimeOffset.UtcNow.AddDays(-60));
        var recentInactive = CreateClientEntity(projectId, isActive: false, updatedAt: DateTimeOffset.UtcNow.AddDays(-10));
        var activeOld = CreateClientEntity(projectId, isActive: true, updatedAt: DateTimeOffset.UtcNow.AddDays(-60));
        var activeRecent = CreateClientEntity(projectId, isActive: true, updatedAt: DateTimeOffset.UtcNow.AddDays(-5));

        await clientCollection.InsertManyAsync(
            [expiredInactive, recentInactive, activeOld, activeRecent],
            cancellationToken: TestCancellationToken);

        // Act — simulate cleanup flow: get expired, then hard-delete each
        var expiredClients = await clientStore.GetExpiredAndDeactivatedAsync(gracePeriodDays, TestCancellationToken);
        foreach (var client in expiredClients)
        {
            await clientStore.HardDeleteAsync(client.Id, TestCancellationToken);
        }

        // Assert
        var remaining = await clientCollection
            .Find(c => c.ProjectId == projectId)
            .ToListAsync(TestCancellationToken);

        remaining.Count.ShouldBe(3);
        remaining.ShouldNotContain(c => c.Id == expiredInactive.Id);
        remaining.ShouldContain(c => c.Id == recentInactive.Id);
        remaining.ShouldContain(c => c.Id == activeOld.Id);
        remaining.ShouldContain(c => c.Id == activeRecent.Id);
    }

    #region Test Helpers

    private static Client CreateClientEntity(Guid projectId, bool isActive, DateTimeOffset updatedAt)
    {
        var now = DateTimeOffset.UtcNow;
        return new Client
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            Name = $"Client-{Guid.CreateVersion7():N}",
            Secret = "test-secret",
            IsActive = isActive,
            Version = 1,
            CreatedAt = now.AddDays(-90),
            CreatedBy = Guid.CreateVersion7(),
            UpdatedAt = updatedAt,
            UpdatedBy = Guid.CreateVersion7(),
        };
    }

    #endregion
}