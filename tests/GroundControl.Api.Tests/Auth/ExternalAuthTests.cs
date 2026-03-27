using System.Security.Claims;
using GroundControl.Api.Shared.Configuration;
using GroundControl.Api.Shared.Security;
using GroundControl.Api.Shared.Security.Auth;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Auth;

[Collection("MongoDB")]
public sealed class ExternalAuthTests : ApiHandlerTestBase
{
    private const string ProviderName = "test-oidc";

    public ExternalAuthTests(MongoFixture mongoFixture)
        : base(mongoFixture)
    { }

    [Fact]
    public async Task JitProvision_NewUserFirstLogin_CreatesUser()
    {
        // Arrange
        var userStore = Substitute.For<IUserStore>();
        userStore.GetByExternalIdAsync(ProviderName, "new-sub-123", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        userStore.GetByEmailAsync("newuser@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var service = CreateJitService(userStore);
        var principal = CreatePrincipal("new-sub-123", "newuser@example.com", "New User");

        // Act
        var result = await service.ProvisionAsync(principal, TestCancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.User.ShouldNotBeNull();
        result.User.ExternalId.ShouldBe("new-sub-123");
        result.User.ExternalProvider.ShouldBe(ProviderName);
        result.User.Email.ShouldBe("newuser@example.com");
        result.User.IsActive.ShouldBeTrue();
        result.User.Grants.ShouldBeEmpty();

        await userStore.Received(1).CreateAsync(Arg.Is<User>(u =>
            u.ExternalId == "new-sub-123" &&
            u.ExternalProvider == ProviderName &&
            u.Email == "newuser@example.com"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JitProvision_ExistingUserBySub_MatchesWithoutCreatingDuplicate()
    {
        // Arrange
        var existingUser = CreateUser("existing-sub", "existing@example.com");
        var userStore = Substitute.For<IUserStore>();
        userStore.GetByExternalIdAsync(ProviderName, "existing-sub", Arg.Any<CancellationToken>())
            .Returns(existingUser);

        var service = CreateJitService(userStore);
        var principal = CreatePrincipal("existing-sub", "existing@example.com", "Existing User");

        // Act
        var result = await service.ProvisionAsync(principal, TestCancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.User.ShouldNotBeNull();
        result.User.Id.ShouldBe(existingUser.Id);

        await userStore.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await userStore.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JitProvision_ExistingUserByEmail_LinksExternalId()
    {
        // Arrange
        var existingUser = CreateUser(externalId: null, email: "linkme@example.com");
        var userStore = Substitute.For<IUserStore>();
        userStore.GetByExternalIdAsync(ProviderName, "oidc-sub-456", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        userStore.GetByEmailAsync("linkme@example.com", Arg.Any<CancellationToken>())
            .Returns(existingUser);
        userStore.UpdateAsync(Arg.Any<User>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var service = CreateJitService(userStore);
        var principal = CreatePrincipal("oidc-sub-456", "linkme@example.com", "Link Me");

        // Act
        var result = await service.ProvisionAsync(principal, TestCancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.User.ShouldNotBeNull();
        result.User.Id.ShouldBe(existingUser.Id);

        await userStore.Received(1).UpdateAsync(
            Arg.Is<User>(u =>
                u.Id == existingUser.Id &&
                u.ExternalId == "oidc-sub-456" &&
                u.ExternalProvider == ProviderName),
            existingUser.Version,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JitProvision_EmailMatchWithDifferentExternalId_RejectsMatch()
    {
        // Arrange
        var existingUser = CreateUser(externalId: "other-sub", email: "conflict@example.com");
        var userStore = Substitute.For<IUserStore>();
        userStore.GetByExternalIdAsync(ProviderName, "new-sub-789", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        userStore.GetByEmailAsync("conflict@example.com", Arg.Any<CancellationToken>())
            .Returns(existingUser);

        var service = CreateJitService(userStore);
        var principal = CreatePrincipal("new-sub-789", "conflict@example.com", "Conflict User");

        // Act
        var result = await service.ProvisionAsync(principal, TestCancellationToken);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Error!.ShouldContain("already linked");
    }

    [Fact]
    public async Task JitProvision_AutoCreateDisabled_RejectsNewUser()
    {
        // Arrange
        var userStore = Substitute.For<IUserStore>();
        userStore.GetByExternalIdAsync(ProviderName, "unknown-sub", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        userStore.GetByEmailAsync("unknown@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var options = CreateExternalOptions();
        options.JitProvisioning.AutoCreate = false;
        var service = CreateJitService(userStore, options);
        var principal = CreatePrincipal("unknown-sub", "unknown@example.com", "Unknown");

        // Act
        var result = await service.ProvisionAsync(principal, TestCancellationToken);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Error!.ShouldContain("auto-creation is disabled");
    }

    [Fact]
    public async Task JitProvision_NewUser_HasEmptyGrants()
    {
        // Arrange
        var userStore = Substitute.For<IUserStore>();
        userStore.GetByExternalIdAsync(ProviderName, "grants-sub", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        userStore.GetByEmailAsync("grantless@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var service = CreateJitService(userStore);
        var principal = CreatePrincipal("grants-sub", "grantless@example.com", "No Grants");

        // Act
        var result = await service.ProvisionAsync(principal, TestCancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.User!.Grants.ShouldBeEmpty();
    }

    [Fact]
    public async Task JitProvision_MissingSubClaim_ReturnsFailure()
    {
        // Arrange
        var userStore = Substitute.For<IUserStore>();
        var service = CreateJitService(userStore);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Email, "nosub@example.com")], "test");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await service.ProvisionAsync(principal, TestCancellationToken);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Error!.ShouldContain("subject identifier");
    }

    [Fact]
    public void ExternalAuthConfigurator_MissingAuthority_ThrowsOnStartup()
    {
        // Arrange
        var options = new GroundControlOptions
        {
            Security = new SecurityOptions
            {
                AuthenticationMode = AuthenticationMode.External,
                External = new ExternalSecurityOptions
                {
                    Authority = string.Empty,
                    ClientId = "test-client"
                }
            }
        };

        var configurator = new ExternalAuthConfigurator(options);
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() =>
            configurator.ConfigureServices(services, new ConfigurationBuilder().Build()));

        ex.Message.ShouldContain("Authority");
    }

    [Fact]
    public void ExternalAuthConfigurator_MissingClientId_ThrowsOnStartup()
    {
        // Arrange
        var options = new GroundControlOptions
        {
            Security = new SecurityOptions
            {
                AuthenticationMode = AuthenticationMode.External,
                External = new ExternalSecurityOptions
                {
                    Authority = "https://idp.example.com",
                    ClientId = string.Empty
                }
            }
        };

        var configurator = new ExternalAuthConfigurator(options);
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() =>
            configurator.ConfigureServices(services, new ConfigurationBuilder().Build()));

        ex.Message.ShouldContain("ClientId");
    }

    private static JitProvisioningService CreateJitService(
        IUserStore userStore,
        ExternalSecurityOptions? options = null)
    {
        var externalOptions = options ?? CreateExternalOptions();
        var logger = NullLoggerFactory.Instance.CreateLogger<JitProvisioningService>();

        return new JitProvisioningService(userStore, externalOptions, logger);
    }

    private static ExternalSecurityOptions CreateExternalOptions() => new()
    {
        Authority = "https://idp.example.com",
        ClientId = "test-client",
        ProviderName = ProviderName,
        JitProvisioning = new JitProvisioningOptions
        {
            Enabled = true,
            MatchByEmail = true,
            AutoCreate = true
        }
    };

    private static ClaimsPrincipal CreatePrincipal(string sub, string email, string name)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, sub),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, name)
        ];

        var identity = new ClaimsIdentity(claims, "test");
        return new ClaimsPrincipal(identity);
    }

    private static User CreateUser(string? externalId, string email) => new()
    {
        Id = Guid.CreateVersion7(),
        Username = email,
        Email = email,
        ExternalId = externalId,
        ExternalProvider = externalId is not null ? ProviderName : null,
        IsActive = true,
        Grants = [],
        Version = 1,
        CreatedAt = DateTimeOffset.UtcNow,
        CreatedBy = Guid.Empty,
        UpdatedAt = DateTimeOffset.UtcNow,
        UpdatedBy = Guid.Empty
    };
}