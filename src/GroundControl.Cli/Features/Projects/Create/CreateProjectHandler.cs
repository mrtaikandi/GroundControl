using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;

namespace GroundControl.Cli.Features.Projects.Create;

internal sealed class CreateProjectHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly CreateProjectOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public CreateProjectHandler(
        IShell shell,
        IOptions<CreateProjectOptions> options,
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
        var groupId = _options.GroupId;
        var templateIdsCsv = _options.TemplateIds;

        if (name is null && _hostOptions.NoInteractive)
        {
            _shell.DisplayError("Missing required option: --name. Provide it explicitly when using --no-interactive.");
            return 1;
        }

        name ??= await _shell.PromptForStringAsync("Project name:", cancellationToken: cancellationToken);

        var description = _options.Description;
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

        List<Guid>? templateIds = null;
        if (templateIdsCsv is not null)
        {
            templateIds = ParseTemplateIds(templateIdsCsv);
            if (templateIds is null)
            {
                return 1;
            }
        }
        else if (!_hostOptions.NoInteractive)
        {
            templateIds = await PromptForTemplateSelectionAsync(cancellationToken);
        }

        var request = new CreateProjectRequest
        {
            Name = name,
            Description = description,
            GroupId = groupId,
            TemplateIds = templateIds
        };

        var (exitCode, project) = await _shell.TryCallAsync(
            ct => _client.CreateProjectHandlerAsync(request, ct), cancellationToken);

        if (exitCode != 0)
        {
            return exitCode;
        }

        _shell.DisplaySuccess(project!, _hostOptions.OutputFormat,
            p => $"Project '{p.Name}' created (id: {p.Id}, version: {p.Version}).");
        return 0;
    }

    private List<Guid>? ParseTemplateIds(string csv)
    {
        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var ids = new List<Guid>(parts.Length);

        foreach (var part in parts)
        {
            if (!Guid.TryParse(part, out var id))
            {
                _shell.DisplayError($"Invalid template ID: '{part}'. Expected a valid GUID.");
                return null;
            }

            ids.Add(id);
        }

        return ids;
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

    private async Task<List<Guid>?> PromptForTemplateSelectionAsync(CancellationToken cancellationToken)
    {
        var templates = new List<TemplateResponse>();
        string? cursor = null;

        do
        {
            var page = await _client.ListTemplatesHandlerAsync(
                groupId: null,
                globalOnly: null,
                limit: null,
                after: cursor,
                before: null,
                sortField: null,
                sortOrder: null,
                cancellationToken: cancellationToken);

            templates.AddRange(page.Data);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        if (templates.Count == 0)
        {
            return null;
        }

        var useTemplates = await _shell.ConfirmAsync("Assign templates?", defaultValue: true, cancellationToken);

        if (!useTemplates)
        {
            return null;
        }

        var selected = await _shell.PromptForMultiSelectionAsync(
            "Select templates:",
            templates,
            t => $"{t.Name} ({t.Id})",
            selectedChoices: [],
            cancellationToken: cancellationToken);

        return selected.Count > 0 ? selected.Select(t => t.Id).ToList() : null;
    }
}