using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Templates.Create;

internal sealed class CreateTemplateHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly CreateTemplateOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public CreateTemplateHandler(
        IShell shell,
        IOptions<CreateTemplateOptions> options,
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
        var name = _options.Name;
        var description = _options.Description;
        var groupId = _options.GroupId;

        if (name is null && _hostOptions.NoInteractive)
        {
            _shell.DisplayError("Missing required option: --name. Provide it explicitly when using --no-interactive.");
            return 1;
        }

        name ??= await _shell.PromptForStringAsync("Template name:", cancellationToken: cancellationToken);

        if (description is null && !_hostOptions.NoInteractive)
        {
            description = await _shell.PromptForStringAsync("Description:", isOptional: true, cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(description))
            {
                description = null;
            }
        }

        if (groupId is null && !_hostOptions.NoInteractive)
        {
            groupId = await PromptForGroupSelectionAsync(cancellationToken);
        }

        var request = new CreateTemplateRequest
        {
            Name = name,
            Description = description,
            GroupId = groupId
        };

        var (exitCode, template) = await _shell.TryCallAsync(
            ct => _client.CreateTemplateHandlerAsync(request, ct), cancellationToken);

        if (exitCode != 0)
        {
            return exitCode;
        }

        _shell.DisplaySuccess(template!, _hostOptions.OutputFormat,
            t => $"Template '{t.Name}' created (id: {t.Id}, version: {t.Version}).");
        return 0;
    }

    private async Task<Guid?> PromptForGroupSelectionAsync(CancellationToken cancellationToken)
    {
        var groups = new List<GroupResponse>();
        string? cursor = null;

        do
        {
            var page = await _client.ListGroupsHandlerAsync(
                limit: null,
                after: cursor,
                before: null,
                sortField: null,
                sortOrder: null,
                cancellationToken: cancellationToken);

            groups.AddRange(page.Data);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        if (groups.Count == 0)
        {
            return null;
        }

        var useGroup = await _shell.ConfirmAsync("Assign to a group?", defaultValue: true, cancellationToken);

        if (!useGroup)
        {
            return null;
        }

        var selected = await _shell.PromptForSelectionAsync(
            "Select group:",
            groups,
            g => $"{g.Name} ({g.Id})",
            enableSearch: true,
            cancellationToken: cancellationToken);

        return selected.Id;
    }
}