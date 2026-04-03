using System.CommandLine;

namespace GroundControl.Cli.Features.Users.Get;

internal sealed class GetUserCommand : Command<GetUserHandler, GetUserOptions>
{
    public GetUserCommand()
        : base("get", "Get a user by ID")
    {
        var idArgument = new Argument<Guid>("id") { Description = "The user ID" };

        Arguments.Add(idArgument);

        ConfigureOptions((parseResult, options) =>
        {
            options.Id = parseResult.GetValue(idArgument);
        });
    }
}