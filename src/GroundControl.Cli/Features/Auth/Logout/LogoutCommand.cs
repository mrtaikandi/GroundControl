namespace GroundControl.Cli.Features.Auth.Logout;

internal sealed class LogoutCommand : Command<LogoutHandler, LogoutOptions>
{
    public LogoutCommand()
        : base("logout", "Clear stored credentials")
    {
    }
}