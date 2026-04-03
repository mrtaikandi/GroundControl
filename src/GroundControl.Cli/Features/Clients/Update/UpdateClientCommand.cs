using System.CommandLine;

namespace GroundControl.Cli.Features.Clients.Update;

internal sealed class UpdateClientCommand : Command<UpdateClientHandler, UpdateClientOptions>
{
    public UpdateClientCommand()
        : base("update", "Update a client")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The client ID" };
        var projectIdOption = new Option<Guid>("--project-id", "The project ID");
        var nameOption = new Option<string?>("--name", "The new client name");
        var isActiveOption = new Option<bool?>("--is-active", "Whether the client is active");
        var expiresAtOption = new Option<DateTimeOffset?>("--expires-at", "The new expiration timestamp (ISO 8601)");
        var versionOption = new Option<long?>("--version", "The expected version for optimistic concurrency");

        Arguments.Add(idArgument);
        Options.Add(projectIdOption);
        Options.Add(nameOption);
        Options.Add(isActiveOption);
        Options.Add(expiresAtOption);
        Options.Add(versionOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.ProjectId = parseResult.GetValue(projectIdOption);
            options.Name = parseResult.GetValue(nameOption);
            options.IsActive = parseResult.GetValue(isActiveOption);
            options.ExpiresAt = parseResult.GetValue(expiresAtOption);
            options.Version = parseResult.GetValue(versionOption);
        });
    }
}