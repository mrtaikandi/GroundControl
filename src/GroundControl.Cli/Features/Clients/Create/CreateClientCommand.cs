using System.CommandLine;

namespace GroundControl.Cli.Features.Clients.Create;

internal sealed class CreateClientCommand : Command<CreateClientHandler, CreateClientOptions>
{
    public CreateClientCommand()
        : base("create", "Create a new client")
    {
        var projectIdOption = new Option<Guid>("--project-id") { Description = "The project ID" };
        var nameOption = new Option<string?>("--name") { Description = "The client name (Required)" };
        var scopesOption = new Option<string?>("--scopes") { Description = "Comma-separated scope assignments (dimension=value,dimension=value)" };
        var expiresAtOption = new Option<DateTimeOffset?>("--expires-at") { Description = "The expiration timestamp (ISO 8601)" };

        Options.Add(projectIdOption);
        Options.Add(nameOption);
        Options.Add(scopesOption);
        Options.Add(expiresAtOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.ProjectId = parseResult.GetValue(projectIdOption);
            options.Name = parseResult.GetValue(nameOption);
            options.Scopes = parseResult.GetValue(scopesOption);
            options.ExpiresAt = parseResult.GetValue(expiresAtOption);
        });
    }
}