using GroundControl.Api.Core.DataProtection;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection;

/// <summary>
/// Verifies that the JSON / colon-separated configuration shape documented in
/// <c>docs/guide/server/configuration.md</c> binds correctly into <see cref="DataProtectionOptions"/>,
/// including the array-shaped previous-paths/URIs and the AzureCredential URI.
/// </summary>
public sealed class DataProtectionOptionsBindingTests
{
    [Fact]
    public void Bind_MinimalFileSystemConfig_PopulatesScalarFields()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:Mode"] = "FileSystem",
                ["DataProtection:KeyStorePath"] = "/keys",
                ["DataProtection:UseDpapi"] = "true"
            })
            .Build();

        // Act
        var options = configuration.GetSection("DataProtection").Get<DataProtectionOptions>();

        // Assert
        options.ShouldNotBeNull();
        options.Mode.ShouldBe(DataProtectionMode.FileSystem);
        options.KeyStorePath.ShouldBe("/keys");
        options.UseDpapi.ShouldBeTrue();
    }

    [Fact]
    public void Bind_FileSystemCertificate_BindsPreviousPathsArray()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:Mode"] = "Certificate",
                ["DataProtection:CertificateProvider"] = "FileSystem",
                ["DataProtection:FileSystemCertificate:Path"] = "/certs/dp-2026.pfx",
                ["DataProtection:FileSystemCertificate:Password"] = "secret",
                ["DataProtection:FileSystemCertificate:PreviousPaths:0"] = "/certs/dp-2024.pfx",
                ["DataProtection:FileSystemCertificate:PreviousPaths:1"] = "/certs/dp-2025.pfx"
            })
            .Build();

        // Act
        var options = configuration.GetSection("DataProtection").Get<DataProtectionOptions>();

        // Assert
        options.ShouldNotBeNull();
        options.Mode.ShouldBe(DataProtectionMode.Certificate);
        options.CertificateProvider.ShouldBe(CertificateProviderMode.FileSystem);
        options.FileSystemCertificate.Path.ShouldBe("/certs/dp-2026.pfx");
        options.FileSystemCertificate.Password.ShouldBe("secret");
        options.FileSystemCertificate.PreviousPaths.ShouldBe(["/certs/dp-2024.pfx", "/certs/dp-2025.pfx"]);
    }

    [Fact]
    public void Bind_AzureBlobCertificate_BindsPreviousBlobUriArray()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:Mode"] = "Certificate",
                ["DataProtection:CertificateProvider"] = "AzureBlob",
                ["DataProtection:AzureBlobCertificate:BlobUri"] = "https://account.blob.core.windows.net/certs/dp-2026.pfx",
                ["DataProtection:AzureBlobCertificate:Password"] = "secret",
                ["DataProtection:AzureBlobCertificate:PreviousBlobUris:0"] = "https://account.blob.core.windows.net/certs/dp-2024.pfx",
                ["DataProtection:AzureBlobCertificate:PreviousBlobUris:1"] = "https://account.blob.core.windows.net/certs/dp-2025.pfx"
            })
            .Build();

        // Act
        var options = configuration.GetSection("DataProtection").Get<DataProtectionOptions>();

        // Assert
        options.ShouldNotBeNull();
        options.CertificateProvider.ShouldBe(CertificateProviderMode.AzureBlob);
        options.AzureBlobCertificate.BlobUri.ShouldBe(new Uri("https://account.blob.core.windows.net/certs/dp-2026.pfx"));
        options.AzureBlobCertificate.PreviousBlobUris.Length.ShouldBe(2);
        options.AzureBlobCertificate.PreviousBlobUris[0].ShouldBe(new Uri("https://account.blob.core.windows.net/certs/dp-2024.pfx"));
        options.AzureBlobCertificate.PreviousBlobUris[1].ShouldBe(new Uri("https://account.blob.core.windows.net/certs/dp-2025.pfx"));
    }

    [Fact]
    public void Bind_AzureCredential_BindsAuthorityHostUri()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:Mode"] = "Azure",
                ["DataProtection:AzureCredential:Mode"] = "ClientSecret",
                ["DataProtection:AzureCredential:TenantId"] = "tenant",
                ["DataProtection:AzureCredential:ClientId"] = "client",
                ["DataProtection:AzureCredential:ClientSecret"] = "secret",
                ["DataProtection:AzureCredential:AuthorityHost"] = "https://login.microsoftonline.us/"
            })
            .Build();

        // Act
        var options = configuration.GetSection("DataProtection").Get<DataProtectionOptions>();

        // Assert
        options.ShouldNotBeNull();
        options.AzureCredential.Mode.ShouldBe(AzureCredentialType.ClientSecret);
        options.AzureCredential.AuthorityHost.ShouldBe(new Uri("https://login.microsoftonline.us/"));
        options.AzureCredential.TenantId.ShouldBe("tenant");
        options.AzureCredential.ClientId.ShouldBe("client");
        options.AzureCredential.ClientSecret.ShouldBe("secret");
    }

    [Fact]
    public void Bind_RedisOptions_BindsAllScalarFields()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:Mode"] = "Redis",
                ["DataProtection:Redis:ConnectionString"] = "redis-host:6379",
                ["DataProtection:Redis:KeyName"] = "groundcontrol-keys",
                ["DataProtection:Redis:ConnectTimeoutMs"] = "1500"
            })
            .Build();

        // Act
        var options = configuration.GetSection("DataProtection").Get<DataProtectionOptions>();

        // Assert
        options.ShouldNotBeNull();
        options.Mode.ShouldBe(DataProtectionMode.Redis);
        options.Redis.ConnectionString.ShouldBe("redis-host:6379");
        options.Redis.KeyName.ShouldBe("groundcontrol-keys");
        options.Redis.ConnectTimeoutMs.ShouldBe(1500);
    }

    [Fact]
    public void Bind_AzureMode_BindsBlobUriAndKeyVaultKeyId()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:Mode"] = "Azure",
                ["DataProtection:Azure:BlobUri"] = "https://account.blob.core.windows.net/keys/key.xml",
                ["DataProtection:Azure:KeyVaultKeyId"] = "https://kv.vault.azure.net/keys/dp/abc"
            })
            .Build();

        // Act
        var options = configuration.GetSection("DataProtection").Get<DataProtectionOptions>();

        // Assert
        options.ShouldNotBeNull();
        options.Azure.BlobUri.ShouldBe(new Uri("https://account.blob.core.windows.net/keys/key.xml"));
        options.Azure.KeyVaultKeyId.ShouldBe(new Uri("https://kv.vault.azure.net/keys/dp/abc"));
    }

    [Fact]
    public void Bind_FromEnvironmentVariables_ProducesSameShapeAsJson()
    {
        // Arrange — Set process env vars with the documented `__` delimiter, then bind through
        // ConfigurationBuilder.AddEnvironmentVariables(). Cleaned up in Dispose.
        var prefix = $"GC_DP_BINDING_{Guid.NewGuid():N}_";
        var keys = new Dictionary<string, string?>
        {
            [$"{prefix}DataProtection__Mode"] = "Redis",
            [$"{prefix}DataProtection__CertificateProvider"] = "FileSystem",
            [$"{prefix}DataProtection__FileSystemCertificate__Path"] = "/certs/dp.pfx",
            [$"{prefix}DataProtection__Redis__ConnectionString"] = "redis-host:6379",
            [$"{prefix}DataProtection__Redis__KeyName"] = "test-keys"
        };

        try
        {
            foreach (var (key, value) in keys)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables(prefix)
                .Build();

            // Act
            var options = configuration.GetSection("DataProtection").Get<DataProtectionOptions>();

            // Assert
            options.ShouldNotBeNull();
            options.Mode.ShouldBe(DataProtectionMode.Redis);
            options.CertificateProvider.ShouldBe(CertificateProviderMode.FileSystem);
            options.FileSystemCertificate.Path.ShouldBe("/certs/dp.pfx");
            options.Redis.ConnectionString.ShouldBe("redis-host:6379");
            options.Redis.KeyName.ShouldBe("test-keys");
        }
        finally
        {
            foreach (var key in keys.Keys)
            {
                Environment.SetEnvironmentVariable(key, value: null);
            }
        }
    }
}
