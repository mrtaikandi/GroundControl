using System.Security.Claims;
using GroundControl.Api.Core.Authentication;
using GroundControl.Api.Shared.Security;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.Authentication;

public sealed class PermissionHandlerTests
{
    private static readonly Guid ViewerRoleId = Guid.CreateVersion7();
    private static readonly Guid EditorRoleId = Guid.CreateVersion7();
    private static readonly Guid PublisherRoleId = Guid.CreateVersion7();
    private static readonly Guid AdminRoleId = Guid.CreateVersion7();

    private static readonly Role ViewerRole = new()
    {
        Id = ViewerRoleId,
        Name = "Viewer",
        Permissions =
        [
            Permissions.ScopesRead, Permissions.GroupsRead, Permissions.ProjectsRead,
            Permissions.TemplatesRead, Permissions.VariablesRead, Permissions.ConfigEntriesRead,
            Permissions.SnapshotsRead, Permissions.AuditRead
        ]
    };

    private static readonly Role EditorRole = new()
    {
        Id = EditorRoleId,
        Name = "Editor",
        Permissions =
        [
            Permissions.ScopesRead, Permissions.GroupsRead, Permissions.ProjectsRead, Permissions.ProjectsWrite,
            Permissions.TemplatesRead, Permissions.TemplatesWrite, Permissions.VariablesRead, Permissions.VariablesWrite,
            Permissions.ConfigEntriesRead, Permissions.ConfigEntriesWrite, Permissions.SnapshotsRead,
            Permissions.ClientsRead, Permissions.ClientsWrite, Permissions.AuditRead
        ]
    };

    private static readonly Role PublisherRole = new()
    {
        Id = PublisherRoleId,
        Name = "Publisher",
        Permissions =
        [
            Permissions.ScopesRead, Permissions.GroupsRead, Permissions.ProjectsRead, Permissions.ProjectsWrite,
            Permissions.TemplatesRead, Permissions.TemplatesWrite, Permissions.VariablesRead, Permissions.VariablesWrite,
            Permissions.ConfigEntriesRead, Permissions.ConfigEntriesWrite, Permissions.SnapshotsRead,
            Permissions.SnapshotsPublish, Permissions.ClientsRead, Permissions.ClientsWrite, Permissions.AuditRead
        ]
    };

    private static readonly Role AdminRole = new()
    {
        Id = AdminRoleId,
        Name = "Admin",
        Permissions = [.. Permissions.All]
    };

    private readonly IUserStore _userStore = Substitute.For<IUserStore>();
    private readonly IRoleStore _roleStore = Substitute.For<IRoleStore>();
    private readonly PermissionHandler _handler;

    public PermissionHandlerTests()
    {
        _roleStore.GetByIdAsync(ViewerRoleId, Arg.Any<CancellationToken>()).Returns(ViewerRole);
        _roleStore.GetByIdAsync(EditorRoleId, Arg.Any<CancellationToken>()).Returns(EditorRole);
        _roleStore.GetByIdAsync(PublisherRoleId, Arg.Any<CancellationToken>()).Returns(PublisherRole);
        _roleStore.GetByIdAsync(AdminRoleId, Arg.Any<CancellationToken>()).Returns(AdminRole);
        _handler = new PermissionHandler(NullLogger<PermissionHandler>.Instance, _userStore, _roleStore);
    }

    [Fact]
    public async Task NoAuth_Succeeds_OnAnyPermission()
    {
        // Arrange
        var requirement = new PermissionRequirement(Permissions.ScopesWrite);
        var context = CreateContext(Guid.Empty, requirement);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Viewer_CanRead_Scopes()
    {
        // Arrange
        var userId = SetupUser(ViewerRoleId);
        var requirement = new PermissionRequirement(Permissions.ScopesRead);
        var context = CreateContext(userId, requirement);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Viewer_CannotWrite_Scopes()
    {
        // Arrange
        var userId = SetupUser(ViewerRoleId);
        var requirement = new PermissionRequirement(Permissions.ScopesWrite);
        var context = CreateContext(userId, requirement);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Viewer_CannotPublish_Snapshots()
    {
        // Arrange
        var userId = SetupUser(ViewerRoleId);
        var requirement = new PermissionRequirement(Permissions.SnapshotsPublish);
        var context = CreateContext(userId, requirement);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Editor_CanWrite_Projects()
    {
        // Arrange
        var userId = SetupUser(EditorRoleId);
        var requirement = new PermissionRequirement(Permissions.ProjectsWrite);
        var context = CreateContext(userId, requirement);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Editor_CannotPublish_Snapshots()
    {
        // Arrange
        var userId = SetupUser(EditorRoleId);
        var requirement = new PermissionRequirement(Permissions.SnapshotsPublish);
        var context = CreateContext(userId, requirement);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Publisher_CanPublish_Snapshots()
    {
        // Arrange
        var userId = SetupUser(PublisherRoleId);
        var requirement = new PermissionRequirement(Permissions.SnapshotsPublish);
        var context = CreateContext(userId, requirement);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(AllPermissions))]
    public async Task Admin_Succeeds_OnAllPermissions(string permission)
    {
        // Arrange
        var userId = SetupUser(AdminRoleId);
        var requirement = new PermissionRequirement(permission);
        var context = CreateContext(userId, requirement);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task InactiveUser_IsDenied()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var user = new User
        {
            Id = userId,
            Username = "inactive",
            Email = "inactive@test.com",
            IsActive = false,
            Grants = [new Grant { RoleId = AdminRoleId }]
        };

        _userStore.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var requirement = new PermissionRequirement(Permissions.ScopesRead);
        var context = CreateContext(userId, requirement);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
        context.HasFailed.ShouldBeTrue();
    }

    [Fact]
    public async Task UnknownUser_IsNotSucceeded()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        _userStore.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var requirement = new PermissionRequirement(Permissions.ScopesRead);
        var context = CreateContext(userId, requirement);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task NoPrincipal_IsNotSucceeded()
    {
        // Arrange
        var requirement = new PermissionRequirement(Permissions.ScopesRead);
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task MultipleGrants_UnionPermissions()
    {
        // Arrange — user has Viewer system-wide and Editor on a group
        var userId = Guid.CreateVersion7();
        var groupId = Guid.CreateVersion7();
        var user = new User
        {
            Id = userId,
            Username = "multi",
            Email = "multi@test.com",
            IsActive = true,
            Grants =
            [
                new Grant { RoleId = ViewerRoleId },
                new Grant { Resource = groupId, RoleId = EditorRoleId }
            ]
        };

        _userStore.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        // Editor has ProjectsWrite, Viewer doesn't — union should include it
        var requirement = new PermissionRequirement(Permissions.ProjectsWrite);
        var context = CreateContext(userId, requirement);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task PatScoping_IntersectsPermissions()
    {
        // Arrange — admin user but PAT only allows ScopesRead
        var userId = SetupUser(AdminRoleId);
        var requirement = new PermissionRequirement(Permissions.ScopesWrite);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("pat_permissions", Permissions.ScopesRead)
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert — ScopesWrite not in PAT scope, so denied
        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task PatScoping_AllowsPermittedAction()
    {
        // Arrange — admin user with PAT that allows ScopesRead
        var userId = SetupUser(AdminRoleId);
        var requirement = new PermissionRequirement(Permissions.ScopesRead);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("pat_permissions", Permissions.ScopesRead)
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task GrantWithUnknownRole_IsSkipped()
    {
        // Arrange — user has a grant referencing a deleted role
        var userId = Guid.CreateVersion7();
        var deletedRoleId = Guid.CreateVersion7();
        var user = new User
        {
            Id = userId,
            Username = "orphan",
            Email = "orphan@test.com",
            IsActive = true,
            Grants = [new Grant { RoleId = deletedRoleId }]
        };

        _userStore.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _roleStore.GetByIdAsync(deletedRoleId, Arg.Any<CancellationToken>()).Returns((Role?)null);

        var requirement = new PermissionRequirement(Permissions.ScopesRead);
        var context = CreateContext(userId, requirement);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task UserWithNoGrants_IsDenied()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var user = new User
        {
            Id = userId,
            Username = "empty",
            Email = "empty@test.com",
            IsActive = true,
            Grants = []
        };

        _userStore.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var requirement = new PermissionRequirement(Permissions.ScopesRead);
        var context = CreateContext(userId, requirement);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
    }

    public static TheoryData<string> AllPermissions()
    {
        var data = new TheoryData<string>();
        foreach (var permission in Permissions.All)
        {
            data.Add(permission);
        }

        return data;
    }

    private Guid SetupUser(Guid roleId, bool isActive = true, Guid? resource = null)
    {
        var userId = Guid.CreateVersion7();
        var user = new User
        {
            Id = userId,
            Username = $"user-{userId:N}",
            Email = $"{userId:N}@test.com",
            IsActive = isActive,
            Grants = [new Grant { Resource = resource, RoleId = roleId }]
        };

        _userStore.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        return userId;
    }

    private static AuthorizationHandlerContext CreateContext(Guid userId, PermissionRequirement requirement)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        return new AuthorizationHandlerContext([requirement], principal, null);
    }
}