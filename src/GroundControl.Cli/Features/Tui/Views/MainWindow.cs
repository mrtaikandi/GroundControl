using GroundControl.Api.Client.Contracts;
using GroundControl.Cli.Features.Tui.ViewModels;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace GroundControl.Cli.Features.Tui.Views;

#pragma warning disable CA2000, CA2213 // Terminal.Gui containers dispose their children

internal sealed class MainWindow : Window
{
    private static readonly string[] PlaceholderTabs =
    [
        "Config Entries",
        "Variables",
        "Projects",
        "Snapshots"
    ];

    private readonly IApplication _app;
    private readonly TabView _tabView;
    private readonly List<IRefreshable> _refreshables = [];

    public MainWindow(IApplication app, string serverUrl, string authMethod, IGroundControlClient client)
    {
        _app = app;
        Title = "GroundControl";

        _tabView = new TabView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        AddResourceTab<ScopeResponse>("Scopes", new ScopeViewModel(client));
        AddResourceTab<GroupResponse>("Groups", new GroupViewModel(client));
        AddResourceTab<TemplateResponse>("Templates", new TemplateViewModel(client));

        foreach (var name in PlaceholderTabs)
        {
            var tab = new Tab
            {
                DisplayText = name,
                View = CreatePlaceholderPanel(name)
            };

            _tabView.AddTab(tab, false);
        }

        if (_tabView.Tabs.Count > 0)
        {
            _tabView.SelectedTab = _tabView.Tabs.First();
        }

        Add(_tabView);

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
        else if (args == Key.R)
        {
            args.Handled = true;
            RefreshActiveTab();
        }
    }

    private void AddResourceTab<T>(string name, ResourceViewModel<T> viewModel)
    {
        var container = new View
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        var listView = new ResourceListView<T>(viewModel, _app);
        var detailView = new ResourceDetailView<T>(viewModel);

        listView.SelectionChanged += viewModel.SelectItem;

        container.Add(listView, detailView);

        var tab = new Tab
        {
            DisplayText = name,
            View = container
        };

        _tabView.AddTab(tab, false);
        _refreshables.Add(listView);

        listView.RefreshList();
    }

    private void RefreshActiveTab()
    {
        var selectedTab = _tabView.SelectedTab;
        if (selectedTab is null)
        {
            return;
        }

        var tabIndex = _tabView.Tabs.ToList().IndexOf(selectedTab);
        if (tabIndex >= 0 && tabIndex < _refreshables.Count)
        {
            _refreshables[tabIndex].Refresh();
        }
    }

    private static View CreatePlaceholderPanel(string resourceName)
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
                   R         Refresh current list
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