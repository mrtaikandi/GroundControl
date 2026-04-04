using GroundControl.Cli.Shared.ErrorHandling;

namespace GroundControl.Cli.Shared;

internal readonly record struct ResolvedVersion<T>(long Version, T? Entity, int ExitCode)
{
    public bool IsSuccess => ExitCode == 0;
}

internal static class VersionResolver
{
    extension(IShell shell)
    {
        internal async Task<ResolvedVersion<TResponse>> ResolveVersionAsync<TResponse>(
            long? providedVersion,
            Func<CancellationToken, Task<TResponse>> fetchEntity,
            Func<TResponse, long> getVersion,
            CancellationToken cancellationToken)
        {
            if (providedVersion is { } version)
            {
                return new ResolvedVersion<TResponse>(version, default, 0);
            }

            var (exitCode, entity) = await shell.TryCallAsync(fetchEntity, cancellationToken);
            return exitCode != 0 ? new ResolvedVersion<TResponse>(0, default, exitCode) : new ResolvedVersion<TResponse>(getVersion(entity!), entity, 0);
        }
    }
}