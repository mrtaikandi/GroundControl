using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Client.Tests.Infrastructure;
using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Features.Users.Contracts;
using CreateGroupRequest = GroundControl.Api.Client.Contracts.CreateGroupRequest;
using CreateUserRequest = GroundControl.Api.Features.Users.Contracts.CreateUserRequest;
using SetGroupMemberRequest = GroundControl.Api.Client.Contracts.SetGroupMemberRequest;
using UpdateGroupRequest = GroundControl.Api.Client.Contracts.UpdateGroupRequest;

namespace GroundControl.Api.Client.Tests.Groups;

public sealed class GroupsClientTests : ApiHandlerTestBase
{
    public GroupsClientTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task CreateGroup_ValidRequest_ReturnsCreatedGroup()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = ApiClientFactory.Create(factory);
        var request = new CreateGroupRequest
        {
            Name = "Engineering",
            Description = "Engineering team"
        };

        // Act
        await client.CreateGroupHandlerAsync(request, TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastStatusCode.ShouldBe(HttpStatusCode.Created);

        var group = handler.DeserializeCapturedResponse<GroupResponse>();
        group.Name.ShouldBe("Engineering");
        group.Description.ShouldBe("Engineering team");
        group.Version.ShouldBe(1);
    }

    [Fact]
    public async Task GetGroup_ExistingId_ReturnsGroup()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = ApiClientFactory.Create(factory);
        var created = await CreateGroupAsync(client, handler, "Platform");

        // Act
        await client.GetGroupHandlerAsync(created.Id, TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastStatusCode.ShouldBe(HttpStatusCode.OK);

        var group = handler.DeserializeCapturedResponse<GroupResponse>();
        group.Id.ShouldBe(created.Id);
        group.Name.ShouldBe("Platform");
    }

    [Fact]
    public async Task UpdateGroup_WithIfMatch_ReturnsUpdatedGroup()
    {
        // Arrange
        await using var factory = CreateFactory();
        var (client, handler) = ApiClientFactory.Create(factory);
        var created = await CreateGroupAsync(client, handler, "DevOps");

        using var httpClient = factory.CreateClient();
        var updateRequest = new UpdateGroupRequest
        {
            Name = "DevOps Updated",
            Description = "Updated description"
        };

        // Act
        httpClient.DefaultRequestHeaders.Add("If-Match", $"\"{created.Version}\"");
        var response = await httpClient.PutAsJsonAsync(
            $"/api/groups/{created.Id}", updateRequest, WebJsonSerializerOptions, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await ReadRequiredJsonAsync<GroupResponse>(response, TestCancellationToken);
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
        var (client, handler) = ApiClientFactory.Create(factory);

        var group = await CreateGroupAsync(client, handler, "Security");
        var user = await CreateUserViaHttpAsync(httpClient, "member1", "member1@test.com");
        var roleId = Guid.CreateVersion7();

        // Act
        await client.SetGroupMemberHandlerAsync(
            group.Id,
            user.Id,
            new SetGroupMemberRequest { RoleId = roleId },
            TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastStatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ListGroupMembers_ReturnsMembers()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var (client, handler) = ApiClientFactory.Create(factory);

        var group = await CreateGroupAsync(client, handler, "Backend");
        var user = await CreateUserViaHttpAsync(httpClient, "member2", "member2@test.com");
        var roleId = Guid.CreateVersion7();

        await client.SetGroupMemberHandlerAsync(
            group.Id, user.Id,
            new SetGroupMemberRequest { RoleId = roleId },
            TestCancellationToken);

        // Act
        await client.ListGroupMembersHandlerAsync(group.Id, TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastStatusCode.ShouldBe(HttpStatusCode.OK);

        var members = handler.DeserializeCapturedResponse<List<UserResponse>>();
        members.ShouldHaveSingleItem();
        members[0].Id.ShouldBe(user.Id);
    }

    [Fact]
    public async Task RemoveGroupMember_DeletesMembership()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var httpClient = factory.CreateClient();
        var (client, handler) = ApiClientFactory.Create(factory);

        var group = await CreateGroupAsync(client, handler, "Frontend");
        var user = await CreateUserViaHttpAsync(httpClient, "member3", "member3@test.com");
        var roleId = Guid.CreateVersion7();

        await client.SetGroupMemberHandlerAsync(
            group.Id, user.Id,
            new SetGroupMemberRequest { RoleId = roleId },
            TestCancellationToken);

        // Act
        await client.RemoveGroupMemberHandlerAsync(group.Id, user.Id, TestCancellationToken);

        // Assert
        handler.LastResponse.ShouldNotBeNull();
        handler.LastStatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task<GroupResponse> CreateGroupAsync(
        GroundControlClient client,
        ResponseCapturingHandler handler,
        string name)
    {
        var request = new CreateGroupRequest
        {
            Name = name,
            Description = $"{name} group"
        };

        await client.CreateGroupHandlerAsync(request, TestCancellationToken);
        return handler.DeserializeCapturedResponse<GroupResponse>();
    }

    private static async Task<UserResponse> CreateUserViaHttpAsync(HttpClient httpClient, string username, string email)
    {
        var response = await httpClient.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest { Username = username, Email = email },
            WebJsonSerializerOptions,
            TestCancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<UserResponse>(WebJsonSerializerOptions, TestCancellationToken).ConfigureAwait(false);
        user.ShouldNotBeNull();

        return user;
    }
}