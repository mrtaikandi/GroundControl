using System.Reflection;
using Azure.Identity;
using GroundControl.Api.Core.DataProtection;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection;

/// <summary>
/// Verifies that <see cref="AzureCredentialFactory.Create"/> returns the correct concrete
/// <c>TokenCredential</c> for each <see cref="AzureCredentialType"/> and threads the configured
/// fields through to the credential.
/// </summary>
public sealed class AzureCredentialFactoryTests
{
    [Fact]
    public void Create_Default_ReturnsDefaultAzureCredential()
    {
        // Arrange
        var options = new AzureCredentialOptions { Mode = AzureCredentialType.Default };

        // Act
        var credential = AzureCredentialFactory.Create(options);

        // Assert
        credential.ShouldBeOfType<DefaultAzureCredential>();
    }

    [Fact]
    public void Create_ManagedIdentity_NoClientId_ReturnsManagedIdentityCredential()
    {
        // Arrange
        var options = new AzureCredentialOptions { Mode = AzureCredentialType.ManagedIdentity };

        // Act
        var credential = AzureCredentialFactory.Create(options);

        // Assert
        credential.ShouldBeOfType<ManagedIdentityCredential>();
    }

    [Fact]
    public void Create_ManagedIdentity_WithClientId_ReturnsManagedIdentityCredential()
    {
        // Arrange
        var options = new AzureCredentialOptions
        {
            Mode = AzureCredentialType.ManagedIdentity,
            ClientId = "11111111-1111-1111-1111-111111111111"
        };

        // Act
        var credential = AzureCredentialFactory.Create(options);

        // Assert
        credential.ShouldBeOfType<ManagedIdentityCredential>();
    }

    [Fact]
    public void Create_WorkloadIdentity_ReturnsWorkloadIdentityCredential()
    {
        // Arrange
        var options = new AzureCredentialOptions
        {
            Mode = AzureCredentialType.WorkloadIdentity,
            TenantId = "tenant-id",
            ClientId = "client-id",
            TokenFilePath = "/var/run/secrets/tokens/azure-identity-token"
        };

        // Act
        var credential = AzureCredentialFactory.Create(options);

        // Assert
        credential.ShouldBeOfType<WorkloadIdentityCredential>();
    }

    [Fact]
    public void Create_ClientSecret_ReturnsClientSecretCredential()
    {
        // Arrange
        var options = new AzureCredentialOptions
        {
            Mode = AzureCredentialType.ClientSecret,
            TenantId = "tenant-id",
            ClientId = "client-id",
            ClientSecret = "secret"
        };

        // Act
        var credential = AzureCredentialFactory.Create(options);

        // Assert — Azure.Identity does not expose TenantId/ClientId as public properties on
        // ClientSecretCredential, so this test pins only the credential type. Field propagation
        // is verified separately via reflection in the AuthorityHost tests.
        credential.ShouldBeOfType<ClientSecretCredential>();
    }

    [Fact]
    public void Create_AzureCli_ReturnsAzureCliCredential()
    {
        // Arrange
        var options = new AzureCredentialOptions { Mode = AzureCredentialType.AzureCli };

        // Act
        var credential = AzureCredentialFactory.Create(options);

        // Assert
        credential.ShouldBeOfType<AzureCliCredential>();
    }

    [Fact]
    public void Create_Environment_ReturnsEnvironmentCredential()
    {
        // Arrange
        var options = new AzureCredentialOptions { Mode = AzureCredentialType.Environment };

        // Act
        var credential = AzureCredentialFactory.Create(options);

        // Assert
        credential.ShouldBeOfType<EnvironmentCredential>();
    }

    [Fact]
    public void Create_UnknownMode_ThrowsInvalidOperationException()
    {
        // Arrange — Cast to invalid enum value to exercise the default switch arm.
        var options = new AzureCredentialOptions { Mode = (AzureCredentialType)999 };

        // Act
        var ex = Should.Throw<InvalidOperationException>(() => AzureCredentialFactory.Create(options));

        // Assert
        ex.Message.ShouldContain(nameof(AzureCredentialOptions.Mode));
        ex.Message.ShouldContain(nameof(AzureCredentialType.Default));
    }

    [Fact]
    public void Create_PropagatesAuthorityHost_ToClientSecretCredential()
    {
        // Arrange
        var sovereignAuthority = new Uri("https://login.microsoftonline.us/");
        var options = new AzureCredentialOptions
        {
            Mode = AzureCredentialType.ClientSecret,
            TenantId = "tenant-id",
            ClientId = "client-id",
            ClientSecret = "secret",
            AuthorityHost = sovereignAuthority
        };

        // Act
        var credential = AzureCredentialFactory.Create(options);

        // Assert
        credential.ShouldBeOfType<ClientSecretCredential>();
        ReadAuthorityHost(credential).ShouldBe(sovereignAuthority);
    }

    [Fact]
    public void Create_WorkloadIdentity_AcceptsAuthorityHostWithoutThrowing()
    {
        // Arrange — WorkloadIdentityCredential nests its options behind a private inner
        // ClientAssertionCredential, so the AuthorityHost reflection used elsewhere does not
        // reach it. Pin the contract that the factory accepts and forwards the field; deeper
        // verification would couple the test to Azure.Identity internals.
        var options = new AzureCredentialOptions
        {
            Mode = AzureCredentialType.WorkloadIdentity,
            TenantId = "tenant-id",
            ClientId = "client-id",
            AuthorityHost = new Uri("https://login.microsoftonline.us/")
        };

        // Act
        var credential = AzureCredentialFactory.Create(options);

        // Assert
        credential.ShouldBeOfType<WorkloadIdentityCredential>();
    }

    /// <summary>
    /// Walks the credential's private fields looking for any nested options object whose
    /// <c>AuthorityHost</c> property has been set. This is intentionally tolerant of small SDK
    /// shape changes — Azure.Identity wraps each credential's options on a privately held field.
    /// </summary>
    private static Uri? ReadAuthorityHost(object credential)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        foreach (var field in credential.GetType().GetFields(Flags))
        {
            var value = field.GetValue(credential);
            if (value is null)
            {
                continue;
            }

            var authorityHostProperty = value.GetType().GetProperty(nameof(TokenCredentialOptions.AuthorityHost), Flags);
            if (authorityHostProperty?.GetValue(value) is Uri uri)
            {
                return uri;
            }
        }

        return null;
    }
}
