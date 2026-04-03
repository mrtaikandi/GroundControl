using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Shared.ErrorHandling;
using Microsoft.Extensions.Options;
using static GroundControl.Cli.Shared.ErrorHandling.ConflictRetryHelper;

namespace GroundControl.Cli.Features.Projects.Update;

internal sealed class UpdateProjectHandler : ICommandHandler
{
    private readonly IShell _shell;
    private readonly UpdateProjectOptions _options;
    private readonly CliHostOptions _hostOptions;
    private readonly IGroundControlClient _client;

    public UpdateProjectHandler(
        IShell shell,
        IOptions<UpdateProjectOptions> options,
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
        var version = _options.Version;

        if (version is null)
        {
            try
            {
                var current = await _client.GetProjectHandlerAsync(_options.Id, cancellationToken);
                version = current.Version;
            }
            catch (GroundControlApiClientException<ProblemDetails> ex)
            {
                _shell.RenderProblemDetails(ex.Result);
                return 1;
            }
        }

        List<Guid>? templateIds = null;
        if (_options.TemplateIds is not null)
        {
            templateIds = [];
            foreach (var part in _options.TemplateIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Guid.TryParse(part, out var id))
                {
                    _shell.DisplayError($"Invalid template ID: '{part}'. Expected a valid GUID.");
                    return 1;
                }

                templateIds.Add(id);
            }
        }

        // WhenWritingNull serializer policy omits null fields from the JSON body,
        // so only fields the user explicitly provided are sent to the API.
        var request = new UpdateProjectRequest
        {
            Name = _options.Name!,
            Description = _options.Description,
            GroupId = _options.GroupId,
            TemplateIds = templateIds
        };

        try
        {
            GroundControlClient.SetIfMatch(version.Value);
            var project = await _client.UpdateProjectHandlerAsync(_options.Id, request, cancellationToken);
            _shell.DisplaySuccess($"Project '{project.Name}' updated (version: {project.Version}).");
            return 0;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex) when (ex.StatusCode == 409)
        {
            var retried = await _shell.HandleConflictAsync(
                async ct =>
                {
                    var current = await _client.GetProjectHandlerAsync(_options.Id, ct);
                    var diffs = new List<FieldDiff>();

                    if (_options.Name is not null && _options.Name != current.Name)
                    {
                        diffs.Add(new FieldDiff("Name", _options.Name, current.Name));
                    }

                    if (_options.Description is not null && _options.Description != (current.Description ?? string.Empty))
                    {
                        diffs.Add(new FieldDiff("Description", _options.Description, current.Description ?? string.Empty));
                    }

                    if (_options.GroupId is not null && _options.GroupId != current.GroupId)
                    {
                        diffs.Add(new FieldDiff("Group Id", _options.GroupId.Value.ToString(), current.GroupId?.ToString() ?? string.Empty));
                    }

                    if (templateIds is not null)
                    {
                        var currentTemplates = string.Join(", ", current.TemplateIds);
                        var requestedTemplates = string.Join(", ", templateIds);
                        if (requestedTemplates != currentTemplates)
                        {
                            diffs.Add(new FieldDiff("Template Ids", requestedTemplates, currentTemplates));
                        }
                    }

                    return new ConflictInfo(current.Version, diffs);
                },
                async (newVersion, ct) =>
                {
                    GroundControlClient.SetIfMatch(newVersion);
                    var project = await _client.UpdateProjectHandlerAsync(_options.Id, request, ct);
                    _shell.DisplaySuccess($"Project '{project.Name}' updated (version: {project.Version}).");
                },
                _hostOptions.NoInteractive,
                cancellationToken);

            return retried ? 0 : 1;
        }
        catch (GroundControlApiClientException<HttpValidationProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
        catch (GroundControlApiClientException<ProblemDetails> ex)
        {
            _shell.RenderProblemDetails(ex.Result);
            return 1;
        }
    }
}