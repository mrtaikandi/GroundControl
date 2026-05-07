using System.Collections.Concurrent;
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

    private static IDataProtectionProvider CreateSharedProvider(string applicationName)
    {
        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName(applicationName);
        return services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
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

    [Fact]
    public void ProtectAndUnprotect_EmptyString_RoundTripsWithoutThrowing()
    {
        // Arrange
        var protector = CreateProtector();

        // Act
        var encrypted = protector.Protect(string.Empty);
        var decrypted = protector.Unprotect(encrypted);

        // Assert — DP encrypts the empty string (it has length and an MAC envelope), so the
        // ciphertext is non-empty even though the plaintext is.
        encrypted.ShouldNotBeNullOrEmpty();
        decrypted.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("héllo wörld")]
    [InlineData("日本語のテスト")]
    [InlineData("emoji-secret-🔐🚀")]
    [InlineData("\0binary-ish")]
    public void ProtectAndUnprotect_UnicodeAndControlCharacters_RoundTrip(string plainText)
    {
        // Arrange
        var protector = CreateProtector();

        // Act
        var decrypted = protector.Unprotect(protector.Protect(plainText));

        // Assert
        decrypted.ShouldBe(plainText);
    }

    [Fact]
    public void ProtectAndUnprotect_LargeString_RoundTrips()
    {
        // Arrange — 1 MiB of data exceeds typical envelope buffering thresholds.
        var protector = CreateProtector();
        var plainText = new string('x', 1024 * 1024);

        // Act
        var decrypted = protector.Unprotect(protector.Protect(plainText));

        // Assert
        decrypted.Length.ShouldBe(plainText.Length);
        decrypted.ShouldBe(plainText);
    }

    [Fact]
    public void Unprotect_NonBase64Input_ThrowsKnownException()
    {
        // Arrange — pin the contract: malformed input surfaces as an exception, not silent
        // success or a corrupted plaintext. The framework currently throws either
        // FormatException (during base64 decode) or CryptographicException (during MAC check).
        var protector = CreateProtector();

        // Act
        var thrown = Record.Exception(() => protector.Unprotect("not-base64-!@#$%"));

        // Assert
        thrown.ShouldNotBeNull("malformed input must not be silently accepted");
        thrown.ShouldBeAssignableTo<Exception>();
        (thrown is FormatException or CryptographicException).ShouldBeTrue(
            $"Expected FormatException or CryptographicException, got {thrown.GetType().Name}: {thrown.Message}");
    }

    [Fact]
    public void TwoProtectorsBackedBySameProvider_Interoperate()
    {
        // Arrange — Two DataProtectionValueProtector instances over the same shared
        // IDataProtectionProvider must be able to read each other's output.
        var sharedProvider = CreateSharedProvider("GroundControl.Tests");
        var protectorA = new DataProtectionValueProtector(sharedProvider);
        var protectorB = new DataProtectionValueProtector(sharedProvider);

        // Act
        var encrypted = protectorA.Protect("interop-value");
        var decrypted = protectorB.Unprotect(encrypted);

        // Assert
        decrypted.ShouldBe("interop-value");
    }

    [Fact]
    public async Task Protect_ConcurrentCallsFromManyThreads_AllSucceedAndRoundTrip()
    {
        // Arrange
        var protector = CreateProtector();
        var inputs = Enumerable.Range(0, 64).Select(i => $"value-{i}").ToArray();
        var roundTripped = new ConcurrentBag<string>();

        // Act — race many threads against the same protector instance.
        await Parallel.ForEachAsync(inputs, TestContext.Current.CancellationToken, (value, _) =>
        {
            var ciphertext = protector.Protect(value);
            roundTripped.Add(protector.Unprotect(ciphertext));
            return ValueTask.CompletedTask;
        });

        // Assert
        roundTripped.OrderBy(v => v, StringComparer.Ordinal)
            .ShouldBe(inputs.OrderBy(v => v, StringComparer.Ordinal));
    }
}