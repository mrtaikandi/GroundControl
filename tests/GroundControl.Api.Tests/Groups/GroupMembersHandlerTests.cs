using System.Net;
using System.Net.Http.Json;
using GroundControl.Api.Features.Groups.Contracts;
using GroundControl.Api.Features.Users.Contracts;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Groups;

[Collection("MongoDB")]
public sealed class GroupMembersHandlerTests : ApiHandlerTestBase
{
    public GroupMembersHandlerTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    {
    }

    [Fact]
    public async Task SetGroupMember_WithValidGroupAndUser_ReturnsNoContent()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "Engineering", TestCancellationToken);
        var user = await CreateUserAsync(apiClient, "member1", "member1@example.com", TestCancellationToken);
        var roleId = Guid.CreateVersion7();

        // Act
        var response = await apiClient.PutAsJsonAsync(
            $"/api/groups/{group.Id}/members/{user.Id}",
            new SetGroupMemberRequest { RoleId = roleId },
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SetGroupMember_Idempotent_ReturnsNoContent()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "Platform", TestCancellationToken);
        var user = await CreateUserAsync(apiClient, "member2", "member2@example.com", TestCancellationToken);
        var roleId = Guid.CreateVersion7();
        var requestBody = new SetGroupMemberRequest { RoleId = roleId };

        await apiClient.PutAsJsonAsync(
            $"/api/groups/{group.Id}/members/{user.Id}",
            requestBody,
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Act
        var response = await apiClient.PutAsJsonAsync(
            $"/api/groups/{group.Id}/members/{user.Id}",
            requestBody,
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ListGroupMembers_AfterAddingMember_ReturnsMember()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "DevOps", TestCancellationToken);
        var user = await CreateUserAsync(apiClient, "member3", "member3@example.com", TestCancellationToken);
        var roleId = Guid.CreateVersion7();

        await apiClient.PutAsJsonAsync(
            $"/api/groups/{group.Id}/members/{user.Id}",
            new SetGroupMemberRequest { RoleId = roleId },
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync($"/api/groups/{group.Id}/members", TestCancellationToken);
        var members = await ReadRequiredJsonAsync<List<UserResponse>>(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        members.ShouldHaveSingleItem();
        members[0].Id.ShouldBe(user.Id);
    }

    [Fact]
    public async Task RemoveGroupMember_AfterAddingMember_ReturnsNoContent()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "Security", TestCancellationToken);
        var user = await CreateUserAsync(apiClient, "member4", "member4@example.com", TestCancellationToken);
        var roleId = Guid.CreateVersion7();

        await apiClient.PutAsJsonAsync(
            $"/api/groups/{group.Id}/members/{user.Id}",
            new SetGroupMemberRequest { RoleId = roleId },
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Act
        var response = await apiClient.DeleteAsync($"/api/groups/{group.Id}/members/{user.Id}", TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ListGroupMembers_AfterRemoval_ReturnsEmptyList()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "QA", TestCancellationToken);
        var user = await CreateUserAsync(apiClient, "member5", "member5@example.com", TestCancellationToken);
        var roleId = Guid.CreateVersion7();

        await apiClient.PutAsJsonAsync(
            $"/api/groups/{group.Id}/members/{user.Id}",
            new SetGroupMemberRequest { RoleId = roleId },
            WebJsonSerializerOptions,
            TestCancellationToken);

        await apiClient.DeleteAsync($"/api/groups/{group.Id}/members/{user.Id}", TestCancellationToken);

        // Act
        var response = await apiClient.GetAsync($"/api/groups/{group.Id}/members", TestCancellationToken);
        var members = await ReadRequiredJsonAsync<List<UserResponse>>(response, TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        members.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetGroupMember_WithUnknownGroup_ReturnsNotFound()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var user = await CreateUserAsync(apiClient, "member6", "member6@example.com", TestCancellationToken);

        // Act
        var response = await apiClient.PutAsJsonAsync(
            $"/api/groups/{Guid.CreateVersion7()}/members/{user.Id}",
            new SetGroupMemberRequest { RoleId = Guid.CreateVersion7() },
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetGroupMember_WithUnknownUser_ReturnsNotFound()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var apiClient = factory.CreateClient();
        var group = await CreateGroupAsync(apiClient, "TestGroup", TestCancellationToken);

        // Act
        var response = await apiClient.PutAsJsonAsync(
            $"/api/groups/{group.Id}/members/{Guid.CreateVersion7()}",
            new SetGroupMemberRequest { RoleId = Guid.CreateVersion7() },
            WebJsonSerializerOptions,
            TestCancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<GroupResponse> CreateGroupAsync(HttpClient apiClient, string name, CancellationToken cancellationToken)
    {
        var response = await apiClient.PostAsJsonAsync(
            "/api/groups",
            new CreateGroupRequest { Name = name, Description = $"{name} group" },
            WebJsonSerializerOptions,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredJsonAsync<GroupResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<UserResponse> CreateUserAsync(HttpClient apiClient, string username, string email, CancellationToken cancellationToken)
    {
        var response = await apiClient.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest { Username = username, Email = email },
            WebJsonSerializerOptions,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredJsonAsync<UserResponse>(response, cancellationToken).ConfigureAwait(false);
    }
}