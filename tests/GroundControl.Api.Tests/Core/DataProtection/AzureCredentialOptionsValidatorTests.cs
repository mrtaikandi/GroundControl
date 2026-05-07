using GroundControl.Api.Core.DataProtection;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection;

/// <summary>
/// Verifies <see cref="AzureCredentialOptions.Validator"/> enforces the per-mode field requirements
/// for each <see cref="AzureCredentialType"/> and honours the supplied member-name prefix.
/// </summary>
public sealed class AzureCredentialOptionsValidatorTests
{
    [Fact]
    public void Validate_ReturnsSuccess_ForDefaultMode() => AssertSuccess(AzureCredentialType.Default);

    [Fact]
    public void Validate_ReturnsSuccess_ForAzureCliMode() => AssertSuccess(AzureCredentialType.AzureCli);

    [Fact]
    public void Validate_ReturnsSuccess_ForEnvironmentMode() => AssertSuccess(AzureCredentialType.Environment);

    private static void AssertSuccess(AzureCredentialType mode)
    {
        // Arrange
        var validator = new AzureCredentialOptions.Validator();
        var options = new AzureCredentialOptions { Mode = mode };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Succeeded.ShouldBeTrue(result.FailureMessage);
    }

    [Fact]
    public void Validate_ManagedIdentity_AcceptsNoClientId_ForSystemAssigned()
    {
        // Arrange
        var validator = new AzureCredentialOptions.Validator();
        var options = new AzureCredentialOptions { Mode = AzureCredentialType.ManagedIdentity };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Succeeded.ShouldBeTrue(result.FailureMessage);
    }

    [Fact]
    public void Validate_ManagedIdentity_AcceptsClientId_ForUserAssigned()
    {
        // Arrange
        var validator = new AzureCredentialOptions.Validator();
        var options = new AzureCredentialOptions
        {
            Mode = AzureCredentialType.ManagedIdentity,
            ClientId = "11111111-1111-1111-1111-111111111111"
        };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Succeeded.ShouldBeTrue(result.FailureMessage);
    }

    [Fact]
    public void Validate_WorkloadIdentity_RequiresTenantIdAndClientId()
    {
        // Arrange
        var validator = new AzureCredentialOptions.Validator();
        var options = new AzureCredentialOptions { Mode = AzureCredentialType.WorkloadIdentity };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(failure => failure.Contains(nameof(AzureCredentialOptions.TenantId)));
        result.Failures!.ShouldContain(failure => failure.Contains(nameof(AzureCredentialOptions.ClientId)));
    }

    [Fact]
    public void Validate_WorkloadIdentity_PassesWhenRequiredFieldsPresent()
    {
        // Arrange
        var validator = new AzureCredentialOptions.Validator();
        var options = new AzureCredentialOptions
        {
            Mode = AzureCredentialType.WorkloadIdentity,
            TenantId = "tenant",
            ClientId = "client"
        };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Succeeded.ShouldBeTrue(result.FailureMessage);
    }

    [Fact]
    public void Validate_ClientSecret_RequiresAllThreeFields()
    {
        // Arrange
        var validator = new AzureCredentialOptions.Validator();
        var options = new AzureCredentialOptions { Mode = AzureCredentialType.ClientSecret };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(f => f.Contains(nameof(AzureCredentialOptions.TenantId)));
        result.Failures!.ShouldContain(f => f.Contains(nameof(AzureCredentialOptions.ClientId)));
        result.Failures!.ShouldContain(f => f.Contains(nameof(AzureCredentialOptions.ClientSecret)));
    }

    [Fact]
    public void Validate_ClientSecret_PassesWhenAllRequiredFieldsPresent()
    {
        // Arrange
        var validator = new AzureCredentialOptions.Validator();
        var options = new AzureCredentialOptions
        {
            Mode = AzureCredentialType.ClientSecret,
            TenantId = "tenant",
            ClientId = "client",
            ClientSecret = "shhh"
        };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Succeeded.ShouldBeTrue(result.FailureMessage);
    }

    [Fact]
    public void Validate_HonoursSuppliedMemberNamePrefix()
    {
        // Arrange — When the parent options validator passes a name (e.g. "AzureCredential"),
        // failure messages should be prefixed with that name rather than the type name.
        var validator = new AzureCredentialOptions.Validator();
        var options = new AzureCredentialOptions { Mode = AzureCredentialType.ClientSecret };

        // Act
        var result = validator.Validate(name: "AzureCredential", options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldAllBe(f => f.StartsWith("AzureCredential:", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_DefaultsToTypeName_WhenNoNamePassed()
    {
        // Arrange
        var validator = new AzureCredentialOptions.Validator();
        var options = new AzureCredentialOptions { Mode = AzureCredentialType.ClientSecret };

        // Act
        var result = validator.Validate(name: null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldAllBe(f => f.StartsWith($"{nameof(AzureCredentialOptions)}:", StringComparison.Ordinal));
    }
}
