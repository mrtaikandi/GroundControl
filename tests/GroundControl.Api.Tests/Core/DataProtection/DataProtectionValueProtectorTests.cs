using System.Security.Cryptography;
using GroundControl.Api.Core.DataProtection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace GroundControl.Api.Tests.Core.DataProtection;

public sealed class DataProtectionValueProtectorTests
{
    private static DataProtectionValueProtector CreateProtector(string? applicationName = null)
    {
        var services = new ServiceCollection();
        services.AddDataProtection()
            .SetApplicationName(applicationName ?? "GroundControl.Tests");

        var provider = services.BuildServiceProvider();
        return new DataProtectionValueProtector(provider.GetRequiredService<IDataProtectionProvider>());
    }

    [Fact]
    public void Protect_ReturnsNonEmptyStringDifferentFromInput()
    {
        // Arrange
        var protector = CreateProtector();
        var plainText = "secret-value";

        // Act
        var encrypted = protector.Protect(plainText);

        // Assert
        encrypted.ShouldNotBeNullOrEmpty();
        encrypted.ShouldNotBe(plainText);
    }

    [Fact]
    public void ProtectAndUnprotect_RoundTripsSuccessfully()
    {
        // Arrange
        var protector = CreateProtector();
        var plainText = "my-sensitive-config-value";

        // Act
        var encrypted = protector.Protect(plainText);
        var decrypted = protector.Unprotect(encrypted);

        // Assert
        decrypted.ShouldBe(plainText);
    }

    [Fact]
    public void Protect_SameValueTwice_ProducesDifferentCiphertext()
    {
        // Arrange
        var protector = CreateProtector();
        var plainText = "same-value";

        // Act
        var encrypted1 = protector.Protect(plainText);
        var encrypted2 = protector.Protect(plainText);

        // Assert
        encrypted1.ShouldNotBe(encrypted2);
    }

    [Fact]
    public void Unprotect_WithTamperedCiphertext_ThrowsCryptographicException()
    {
        // Arrange
        var protector = CreateProtector();
        var encrypted = protector.Protect("some-value");
        var tampered = encrypted + "TAMPERED";

        // Act & Assert
        Should.Throw<CryptographicException>(() => protector.Unprotect(tampered));
    }

    [Fact]
    public void Unprotect_WithDifferentApplicationKey_ThrowsCryptographicException()
    {
        // Arrange
        var protector1 = CreateProtector("App1");
        var protector2 = CreateProtector("App2");
        var encrypted = protector1.Protect("secret");

        // Act & Assert
        Should.Throw<CryptographicException>(() => protector2.Unprotect(encrypted));
    }
}