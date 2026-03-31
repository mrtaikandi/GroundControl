using System.Reflection;
using NuGet.Versioning;

namespace GroundControl.Host.Cli.Internals;

/// <summary>
/// Service for managing NuGet packages, including version checks, downloads, and extraction.
/// </summary>
internal interface IPackageService
{
    /// <summary>
    /// Asserts that the current version is the latest available version.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task AssertLatestVersionAsync();

    /// <summary>
    /// Gets the latest available version of a package from NuGet.
    /// </summary>
    /// <param name="packageName">The name of the package.</param>
    /// <returns>The latest <see cref="NuGetVersion"/>, or null if not found.</returns>
    Task<NuGetVersion?> GetLatestVersionAsync(string packageName);

    /// <summary>
    /// Downloads a package from NuGet.
    /// </summary>
    /// <param name="packageName">The name of the package to download.</param>
    /// <param name="version">The specific version to download. If null, the latest version is downloaded.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The path to the downloaded package file, or null if download failed.</returns>
    Task<string?> DownloadPackageAsync(
        string packageName,
        string? version = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts a package to the specified directory.
    /// </summary>
    /// <param name="packagePath">The path to the package file.</param>
    /// <param name="extractPath">The directory where the package should be extracted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous extraction operation.</returns>
    Task ExtractPackageAsync(string packagePath, string extractPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current version of an assembly.
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <returns>The <see cref="NuGetVersion"/> of the assembly, or null if version information is not available.</returns>
    NuGetVersion? GetCurrentVersion(Assembly assembly);
}