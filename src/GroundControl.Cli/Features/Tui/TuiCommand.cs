namespace GroundControl.Cli.Features.Tui;

[RootCommand]
internal sealed class TuiCommand : Command<TuiCommandHandler, TuiCommandOptions>
{
    public TuiCommand()
        : base("tui", "Launch the interactive TUI dashboard")
    {
    }
}