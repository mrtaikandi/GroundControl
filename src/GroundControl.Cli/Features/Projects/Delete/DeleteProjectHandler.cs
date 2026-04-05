using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.ErrorHandling.ConflictRetryHelper;

namespace GroundControl.Cli.Features.Projects.Delete;

internal sealed class DeleteProjectHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly DeleteProjectOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public DeleteProjectHandler(
        IShell shell,
        IOptions<DeleteProjectOptions> options,
        IOptions<CliHostOptions> hostOptions,
        IGroundControlClient client)
    {
        _shell = shell;
        _options = options.Value;
        _hostOptions = hostOptions.Value;
        _client = client;
    }

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var resolved = await _shell.ResolveVersionAsync(
            _options.Version,
            ct => _client.GetProjectHandlerAsync(_options.Id, ct),
            r => r.Version,
            cancellationToken);

        if (!resolved.IsSuccess)
        {
            return resolved.ExitCode;
        }

        var version = resolved.Version;
        var current = resolved.Entity;

        if (!_options.Yes && !_hostOptions.NoInteractive)
        {
            var name = current?.Name ?? _options.Id.ToString();
            var confirmed = await _shell.ConfirmAsync(
                $"Delete project '{name}'?", defaultValue: false, cancellationToken);

            if (!confirmed)
            {
                _shell.DisplaySubtleMessage("Delete cancelled.");
                return 0;
            }
        }

        return await _shell.TryCallWithConflictHandlingAsync(
            _hostOptions.NoInteractive,
            async ct =>
            {
                GroundControlClient.SetIfMatch(version);
                await _client.DeleteProjectHandlerAsync(_options.Id, ct);
                _shell.DisplaySuccess("Project deleted.", _hostOptions.OutputFormat);
                return 0;
            },
            async ct =>
            {
                var latest = await _client.GetProjectHandlerAsync(_options.Id, ct);
                var diffs = new List<FieldDiff>();

                if (current is not null)
                {
                    if (current.Name != latest.Name)
                    {
                        diffs.Add(new FieldDiff("Name", current.Name, latest.Name));
                    }

                    if ((current.Description ?? string.Empty) != (latest.Description ?? string.Empty))
                    {
                        diffs.Add(new FieldDiff("Description", current.Description ?? string.Empty, latest.Description ?? string.Empty));
                    }

                    if (current.GroupId != latest.GroupId)
                    {
                        diffs.Add(new FieldDiff("Group Id", current.GroupId?.ToString() ?? string.Empty, latest.GroupId?.ToString() ?? string.Empty));
                    }

                    var currentTemplates = string.Join(", ", current.TemplateIds);
                    var latestTemplates = string.Join(", ", latest.TemplateIds);
                    if (currentTemplates != latestTemplates)
                    {
                        diffs.Add(new FieldDiff("Template Ids", currentTemplates, latestTemplates));
                    }
                }

                return new ConflictInfo(latest.Version, diffs);
            },
            async (newVersion, ct) =>
            {
                GroundControlClient.SetIfMatch(newVersion);
                await _client.DeleteProjectHandlerAsync(_options.Id, ct);
                _shell.DisplaySuccess("Project deleted.", _hostOptions.OutputFormat);
            },
            cancellationToken);
    }
}