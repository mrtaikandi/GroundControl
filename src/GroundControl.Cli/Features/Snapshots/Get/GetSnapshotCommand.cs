using System.CommandLine;

namespace GroundControl.Cli.Features.Snapshots.Get;

internal sealed class GetSnapshotCommand : Command<GetSnapshotHandler, GetSnapshotOptions>
{
    public GetSnapshotCommand()
        : base("get", "Get a snapshot by ID")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The snapshot ID" };
        var projectIdOption = new Option<Guid>("--project-id", "The project ID");
        var decryptOption = new Option<bool?>("--decrypt", "Decrypt sensitive values");

        Arguments.Add(idArgument);
        Options.Add(projectIdOption);
        Options.Add(decryptOption);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
            options.ProjectId = parseResult.GetValue(projectIdOption);
            options.Decrypt = parseResult.GetValue(decryptOption);
        });
    }
}