using System.CommandLine;

namespace GroundControl.Cli.Features.ClientConfig.Get;

internal sealed class GetClientConfigCommand : Command<GetClientConfigHandler, GetClientConfigOptions>
{
    public GetClientConfigCommand()
        : base("get", "Fetch resolved configuration for a client")
    {
        var clientIdOption = new Option<Guid>("--client-id")
        {
            Description = "The client ID to authenticate with.",
            Required = true
        };

        var clientSecretOption = new Option<string>("--client-secret")
        {
            Description = "The client secret to authenticate with.",
            Required = true
        };

        Options.Add(clientIdOption);
        Options.Add(clientSecretOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.ClientId = parseResult.GetValue(clientIdOption);
            options.ClientSecret = parseResult.GetValue(clientSecretOption) ?? string.Empty;
        });
    }
}