using System.Reflection;

namespace GroundControl.E2E.Tests.Infrastructure;

/// <summary>
/// Resolves the repository root path from assembly metadata injected at build time.
/// </summary>
internal static class RepositoryRootResolver
{
    /// <summary>
    /// Gets the absolute path to the repository root directory.
    /// </summary>
    public static string GetResolveRoot()
    {
        return typeof(RepositoryRootResolver).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "RepositoryRoot")?.Value
               ?? throw new InvalidOperationException("RepositoryRoot assembly metadata not found.");
    }
}