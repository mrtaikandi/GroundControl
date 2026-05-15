using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.MongoDb.Conventions;
using GroundControl.Persistence.MongoDb.Stores;
using GroundControl.Persistence.MongoDb.Tests.Infrastructure;
using Shouldly;
using Xunit;

namespace GroundControl.Persistence.MongoDb.Tests.ConfigEntries;

[Collection("MongoDB")]
public sealed class ConfigEntryStoreTests
{
    private readonly MongoFixture _mongoFixture;

    public ConfigEntryStoreTests(MongoFixture mongoFixture)
    {
        _mongoFixture = mongoFixture;
    }

    [Fact]
    public async Task UpdateAsync_RenamingKeyToSiblingKeyInSameOwner_ThrowsDuplicateKeyException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(cancellationToken);
        var ownerId = Guid.CreateVersion7();

        await store.CreateAsync(CreateEntry(ownerId, "ExistingKey"), cancellationToken);
        var renaming = CreateEntry(ownerId, "OriginalKey");
        await store.CreateAsync(renaming, cancellationToken);

        renaming.Key = "ExistingKey";

        // Act / Assert
        await Should.ThrowAsync<DuplicateKeyException>(
            () => store.UpdateAsync(renaming, renaming.Version, cancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_RenamingKeyToUnusedKey_PersistsNewKeyAndIncrementsVersion()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(cancellationToken);
        var entry = CreateEntry(Guid.CreateVersion7(), "OriginalKey");
        await store.CreateAsync(entry, cancellationToken);

        entry.Key = "RenamedKey";

        // Act
        var updated = await store.UpdateAsync(entry, entry.Version, cancellationToken);
        var refetched = await store.GetByIdAsync(entry.Id, cancellationToken);

        // Assert
        updated.ShouldBeTrue();
        refetched.ShouldNotBeNull();
        refetched.Key.ShouldBe("RenamedKey");
        refetched.Version.ShouldBe(2);
    }

    private async Task<ConfigEntryStore> CreateStoreAsync(CancellationToken cancellationToken)
    {
        var database = _mongoFixture.CreateDatabase();
        var context = _mongoFixture.CreateContext(database);
        var configuration = new ConfigEntryConfiguration(context);

        await configuration.ConfigureAsync(cancellationToken).ConfigureAwait(false);

        return new ConfigEntryStore(context);
    }

    private static ConfigEntry CreateEntry(Guid ownerId, string key)
    {
        var timestamp = DateTimeOffset.UtcNow;

        return new ConfigEntry
        {
            Id = Guid.CreateVersion7(),
            Key = key,
            OwnerId = ownerId,
            OwnerType = ConfigEntryOwnerType.Project,
            ValueType = "String",
            Values = [new ScopedValue { Value = "value" }],
            IsSensitive = false,
            Description = null,
            Version = 1,
            CreatedAt = timestamp,
            CreatedBy = Guid.Empty,
            UpdatedAt = timestamp,
            UpdatedBy = Guid.Empty
        };
    }
}