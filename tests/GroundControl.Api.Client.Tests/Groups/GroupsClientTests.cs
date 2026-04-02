using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GroundControl.Api.Client.Tests.Infrastructure;
using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Features.Users.Contracts;
using Microsoft.Kiota.Abstractions;
using CreateGroupRequest = GroundControl.Api.Client.Models.CreateGroupRequest;
using CreateUserRequest = GroundControl.Api.Features.Users.Contracts.CreateUserRequest;
using SetGroupMemberRequest = GroundControl.Api.Client.Models.SetGroupMemberRequest;
using UpdateGroupRequest = GroundControl.Api.Client.Models.UpdateGroupRequest;

namespace GroundControl.Api.Client.Tests.Groups;

public sealed class GroupsClientTests : ApiHandlerTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public GroupsClientTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task CreateGroup_ValidRequest_ReturnsCreatedGroup()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = KiotaClientFactory.Create(factory);
        var request = new CreateGroupRequest
        {
            Name = "Engineering",
            Description = "Engineering team"
        };

        // Act
        using var stream = await client.Api.Groups.PostAsync(request, cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var group = await DeserializeAsync<GroupResponse>(stream);
        group.Name.ShouldBe("Engineering");
        group.Description.ShouldBe("Engineering team");
        group.Version.ShouldBe(1);
    }

    [Fact]
    public async Task GetGroup_ExistingId_ReturnsGroup()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = KiotaClientFactory.Create(factory);
        var created = await CreateGroupAsync(client, "Platform");

        // Act
        using var stream = await client.Api.Groups[created.Id].GetAsync(cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var group = await DeserializeAsync<GroupResponse>(stream);
        group.Id.ShouldBe(created.Id);
        group.Name.ShouldBe("Platform");
    }

    [Fact]
    public async Task UpdateGroup_WithIfMatch_ReturnsUpdatedGroup()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = KiotaClientFactory.Create(factory);
        var created = await CreateGroupAsync(client, "DevOps");
        var etag = $"\"{created.Version}\"";

        var updateRequest = new UpdateGroupRequest
        {
            Name = "DevOps Updated",
            Description = "Updated description"
        };

        // Act
        using var stream = await client.Api.Groups[created.Id].PutAsync(updateRequest, config =>
        {
            config.Headers.Add("If-Match", etag);
        }, cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await DeserializeAsync<GroupResponse>(stream);
        updated.Name.ShouldBe("DevOps Updated");
        updated.Description.ShouldBe("Updated description");
        updated.Version.ShouldBe(2);
    }

    [Fact]
    public async Task SetGroupMember_AddsUserToGroup()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var (client, handler) = KiotaClientFactory.Create(factory);

        var group = await CreateGroupAsync(client, "Security");
        var user = await CreateUserViaHttpAsync(httpClient, "member1", "member1@test.com");
        var roleId = Guid.CreateVersion7();

        // Act
        using var stream = await client.Api.Groups[group.Id].Members[user.Id].PutAsync(
            new SetGroupMemberRequest { RoleId = roleId },
            cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ListGroupMembers_ReturnsMembers()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var (client, handler) = KiotaClientFactory.Create(factory);

        var group = await CreateGroupAsync(client, "Backend");
        var user = await CreateUserViaHttpAsync(httpClient, "member2", "member2@test.com");
        var roleId = Guid.CreateVersion7();

        using var _ = await client.Api.Groups[group.Id].Members[user.Id].PutAsync(
            new SetGroupMemberRequest { RoleId = roleId },
            cancellationToken: TestCancellationToken);

        // Act
        using var stream = await client.Api.Groups[group.Id].Members.GetAsync(cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var members = await DeserializeAsync<List<UserResponse>>(stream);
        members.ShouldHaveSingleItem();
        members[0].Id.ShouldBe(user.Id);
    }

    [Fact]
    public async Task RemoveGroupMember_DeletesMembership()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var (client, handler) = KiotaClientFactory.Create(factory);

        var group = await CreateGroupAsync(client, "Frontend");
        var user = await CreateUserViaHttpAsync(httpClient, "member3", "member3@test.com");
        var roleId = Guid.CreateVersion7();

        using var _ = await client.Api.Groups[group.Id].Members[user.Id].PutAsync(
            new SetGroupMemberRequest { RoleId = roleId },
            cancellationToken: TestCancellationToken);

        // Act
        using var stream = await client.Api.Groups[group.Id].Members[user.Id].DeleteAsync(cancellationToken: TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task<GroupResponse> CreateGroupAsync(GroundControlApiClient client, string name)
    {
        var request = new CreateGroupRequest
        {
            Name = name,
            Description = $"{name} group"
        };

        using var stream = await client.Api.Groups.PostAsync(request, cancellationToken: TestCancellationToken);
        return await DeserializeAsync<GroupResponse>(stream);
    }

    private static async Task<UserResponse> CreateUserViaHttpAsync(HttpClient httpClient, string username, string email)
    {
        var response = await httpClient.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest { Username = username, Email = email },
            JsonOptions,
            TestCancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions, TestCancellationToken).ConfigureAwait(false);
        user.ShouldNotBeNull();

        return user;
    }

    private static async Task<T> DeserializeAsync<T>(Stream? stream) where T : class
    {
        stream.ShouldNotBeNull();
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions).ConfigureAwait(false);
        result.ShouldNotBeNull();

        return result;
    }
}