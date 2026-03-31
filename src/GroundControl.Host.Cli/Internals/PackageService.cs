using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using GroundControl.Host.Cli.Internals.IO;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace GroundControl.Host.Cli.Internals;

internal sealed class PackageService : IPackageService
{
    private const string PackageName = "GlobalOperations.Cli";

    private readonly IShell _shell;
    private readonly HttpClient _httpClient;
    private readonly IFileService _fileService;
    private readonly CliHostOptions _options;

    public PackageService(
        IShell shell,
        IHttpClientFactory httpClientFactory,
        IFileService fileService,
        IOptions<CliHostOptions> options)
    {
        _shell = shell;
        _httpClient = httpClientFactory.CreateClient();
        _fileService = fileService;
        _options = options.Value;
    }

    public async Task AssertLatestVersionAsync()
    {
        try
        {
            var latestVersion = await GetLatestVersionAsync(PackageName);
            var currentVersion = GetCurrentVersion(Assembly.GetEntryAssembly()!);
            if (latestVersion is null)
            {
                _shell.DisplayError(
                    $"Unable to compare package versions. Current {currentVersion}. Latest {latestVersion}.");

                return;
            }

            _shell.DisplaySubtleMessage($"{PackageName} Version: {currentVersion}");

            if (currentVersion < latestVersion)
            {
                _shell.DisplayMessage("wrench", $"Version [green]{latestVersion}[/] is available.");
                _shell.DisplaySubtleMessage("Download using [italic]dotnet tool update -g KubernetesToolbox[/]");
            }
        }
        catch (Exception ex)
        {
            if (ex is HttpRequestException { StatusCode: HttpStatusCode.NotFound })
            {
                _shell.DisplayError($"Package {PackageName} not found; cannot determine if latest version.");
            }
            else
            {
                _shell.DisplayException(ex);
            }
        }
    }

    public async Task<NuGetVersion?> GetLatestVersionAsync(string packageName)
    {
        var result = await GetPackageVersionsAsync(packageName);
        if (result?.Versions == null || result.Versions.Count == 0)
        {
            return null;
        }

        var latestVersion = result
            .Versions
            .Select(NuGetVersion.Parse)
            .OrderByDescending(x => x)
            .First();

        return latestVersion;
    }

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Used in URLs")]
    public async Task<PackageVersionsResult?> GetPackageVersionsAsync(string packageName = PackageName)
    {
        var baseUrl = _options.PackageServer;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Package repository base URL is not configured.");
        }

        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri($"{baseUrl}/{packageName.ToLowerInvariant()}/index.json"));

        var response = await _httpClient.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        var contentString = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(contentString))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PackageVersionsResult>(contentString);
    }

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Used in URLs")]
    public async Task<string?> DownloadPackageAsync(
        string packageName,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        _shell.DisplayMessage("wrench", $"Downloading the latest {packageName} package.");
        try
        {
            NuGetVersion? targetVersion;
            if (string.IsNullOrEmpty(version))
            {
                targetVersion = await GetLatestVersionAsync(packageName);
                if (targetVersion == null)
                {
                    _shell.DisplayError($"No versions found for package {packageName}.");
                    return null;
                }
            }
            else
            {
                if (!NuGetVersion.TryParse(version, out targetVersion))
                {
                    _shell.DisplayError($"Invalid version format: {version}");
                    return null;
                }
            }

            var baseUrl = _options.PackageServer;
            var packageUrl =
                $"{baseUrl}/{packageName.ToLowerInvariant()}/{targetVersion}/{packageName.ToLowerInvariant()}.{targetVersion}.nupkg";

            var tempDir = Path.Combine(Path.GetTempPath(), "KubernetesToolbox", "Packages");
            var packageFileName = $"{packageName}.{targetVersion}.nupkg";

            using var response = await _httpClient.GetAsync(new Uri(packageUrl), cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _shell.DisplayError($"Package {packageName} version {targetVersion} not found.");
                return null;
            }

            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await _fileService.CreateAsync(responseStream, tempDir, packageFileName);

            return Path.Combine(tempDir, packageFileName);
        }
        catch (Exception ex)
        {
            _shell.DisplayException(ex);
            return null;
        }
    }

    public async Task ExtractPackageAsync(
        string packagePath,
        string extractPath,
        CancellationToken cancellationToken = default)
    {
        _shell.DisplayMessage("wrench", "Extracting package contents.");
        if (!Directory.Exists(extractPath))
        {
            Directory.CreateDirectory(extractPath);
        }

        await using var archive = await ZipFile.OpenReadAsync(packagePath, cancellationToken);

        var allEntries = archive.Entries
            .Where(e => !e.FullName.EndsWith('/') && !string.IsNullOrEmpty(e.Name))
            .ToList();

        foreach (var entry in allEntries)
        {
            var fileName = Path.GetFileName(entry.FullName);
            var destinationPath = Path.Combine(extractPath, fileName);

            if (!File.Exists(destinationPath))
            {
                await entry.ExtractToFileAsync(destinationPath, false, cancellationToken);
            }
        }

        await Task.CompletedTask;
    }

    public NuGetVersion GetCurrentVersion(Assembly assembly)
    {
        var currentVersionString = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

        return NuGetVersion.TryParse(currentVersionString, out var currentVersion)
            ? currentVersion
            : throw new InvalidOperationException($"Unable to parse version {currentVersionString}.");
    }

    public sealed record PackageVersionsResult([property: JsonPropertyName("versions")] List<string> Versions);
}