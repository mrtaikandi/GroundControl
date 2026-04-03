namespace GroundControl.Cli.Features.Tui;

[RootCommand<TuiDependencyModule>]
internal sealed class TuiCommand : Command<TuiCommandHandler, TuiCommandOptions>
{
    public TuiCommand()
        : base("tui", "Launch the interactive TUI dashboard")
    {
    }
}