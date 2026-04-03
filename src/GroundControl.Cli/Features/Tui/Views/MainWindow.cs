using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace GroundControl.Cli.Features.Tui.Views;

#pragma warning disable CA2000 // Terminal.Gui containers dispose their children

internal sealed class MainWindow : Window
{
    private static readonly string[] ResourceTabs =
    [
        "Scopes",
        "Groups",
        "Templates",
        "Config Entries",
        "Variables",
        "Projects",
        "Snapshots"
    ];

    private readonly IApplication _app;

    public MainWindow(IApplication app, string serverUrl, string authMethod)
    {
        _app = app;
        Title = "GroundControl";

        var tabView = new TabView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        foreach (var name in ResourceTabs)
        {
            var tab = new Tab
            {
                DisplayText = name,
                View = CreateSplitPanel(name)
            };

            tabView.AddTab(tab, false);
        }

        if (tabView.Tabs.Count > 0)
        {
            tabView.SelectedTab = tabView.Tabs.First();
        }

        Add(tabView);

        var statusBar = new StatusBarView(serverUrl, authMethod);
        Add(statusBar);

        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, Key args)
    {
        if (args == Key.Q)
        {
            args.Handled = true;
            _app.RequestStop();
        }
        else if (args == Key.F1)
        {
            args.Handled = true;
            ShowHelpDialog();
        }
    }

    private static View CreateSplitPanel(string resourceName)
    {
        var container = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        var listFrame = new FrameView
        {
            Title = "List",
            X = 0,
            Y = 0,
            Width = Dim.Percent(40),
            Height = Dim.Fill()
        };

        var detailFrame = new FrameView
        {
            Title = "Details",
            X = Pos.Right(listFrame),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        listFrame.Add(new Label
        {
            Text = $"No {resourceName.ToLower(CultureInfo.CurrentCulture)} loaded.",
            X = 1,
            Y = 1
        });

        detailFrame.Add(new Label
        {
            Text = "Select an item to view details.",
            X = 1,
            Y = 1
        });

        container.Add(listFrame, detailFrame);
        return container;
    }

    private void ShowHelpDialog()
    {
        using var dialog = new Dialog
        {
            Title = "Help — Keyboard Shortcuts",
            Width = 50,
            Height = 14
        };

        var helpText = new Label
        {
            Text = """
                   Q         Quit
                   F1        Show this help
                   Tab       Next tab
                   Shift+Tab Previous tab
                   N         New item (planned)
                   E         Edit item (planned)
                   D         Delete item (planned)
                   R         Refresh (planned)
                   """,
            X = 2,
            Y = 1
        };

        var okButton = new Button
        {
            Text = "OK",
            IsDefault = true
        };

        okButton.Accepting += (_, _) => _app.RequestStop(dialog);

        dialog.Add(helpText);
        dialog.Add(okButton);
        _app.Run(dialog);
    }
}