namespace GroundControl.Cli.Features.Config.Show;

internal sealed class ShowConfigCommand : Command<ShowConfigHandler, ShowConfigOptions>
{
    public ShowConfigCommand()
        : base("show", "Display effective GroundControl configuration")
    {
    }
}

internal sealed class ShowConfigOptions;