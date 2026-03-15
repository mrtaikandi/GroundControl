using System.Security.Claims;
using GroundControl.Api.Shared.Security.Auth;
using GroundControl.Api.Shared.Security.Authorization;
using GroundControl.Persistence.Contracts;
using GroundControl.Persistence.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Shared.Security.Authorization;

public sealed class GroundControlClaimsTransformationTests
{
    private readonly IUserStore _userStore = Substitute.For<IUserStore>();
    private readonly GroundControlClaimsTransformation _transformation;

    public GroundControlClaimsTransformationTests()
    {
        _transformation = new GroundControlClaimsTransformation(NullLogger<GroundControlClaimsTransformation>.Instance, _userStore);
    }

    [Fact]
    public async Task ApiKeyScheme_SkipsValidation()
    {
        // Arrange
        var clientId = Guid.CreateVersion7();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, clientId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, ApiKeyAuthenticationHandler.SchemeName));

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldBeSameAs(principal);
        await _userStore.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoSubClaim_ReturnsPrincipalUnchanged()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldBeSameAs(principal);
    }

    [Fact]
    public async Task InvalidSubClaim_ReturnsPrincipalUnchanged()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-guid") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldBeSameAs(principal);
    }

    [Fact]
    public async Task NoAuth_GuidEmpty_SkipsDbLookup()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "NoAuth"));

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldBeSameAs(principal);
        await _userStore.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActiveUser_ReturnsPrincipalUnchanged()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var user = new User
        {
            Id = userId,
            Username = "active",
            Email = "active@test.com",
            IsActive = true
        };

        _userStore.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldBeSameAs(principal);
    }

    [Fact]
    public async Task InactiveUser_ReturnsEmptyPrincipal()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var user = new User
        {
            Id = userId,
            Username = "inactive",
            Email = "inactive@test.com",
            IsActive = false
        };

        _userStore.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldNotBeSameAs(principal);
        result.Identity.ShouldBeNull();
    }

    [Fact]
    public async Task UnknownUser_ReturnsEmptyPrincipal()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        _userStore.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldNotBeSameAs(principal);
        result.Identity.ShouldBeNull();
    }
}