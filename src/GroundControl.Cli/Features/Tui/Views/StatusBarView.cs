using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace GroundControl.Cli.Features.Tui.Views;

internal sealed class StatusBarView : StatusBar
{
    public StatusBarView(string serverUrl, string authMethod)
    {
        var serverShortcut = new Shortcut
        {
            Title = $"Server: {serverUrl}",
            Key = Key.Empty
        };

        var authShortcut = new Shortcut
        {
            Title = $"Auth: {authMethod}",
            Key = Key.Empty
        };

        var quitShortcut = new Shortcut
        {
            Title = "Quit",
            Key = Key.Q
        };

        var helpShortcut = new Shortcut
        {
            Title = "Help",
            Key = Key.F1
        };

        Add(serverShortcut, authShortcut, quitShortcut, helpShortcut);
    }
}