namespace GroundControl.Cli.Features.Auth.Status;

internal sealed class StatusCommand : Command<StatusHandler, StatusOptions>
{
    public StatusCommand()
        : base("status", "Show current authentication status")
    {
    }
}