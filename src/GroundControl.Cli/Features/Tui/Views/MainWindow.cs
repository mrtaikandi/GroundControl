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
    private readonly IApplication _app;
    private readonly TabView _tabView;
    private readonly Dictionary<Tab, IRefreshable> _refreshables = [];

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
        AddResourceTab<ConfigEntryResponse>("Config Entries", new ConfigEntryViewModel(client));
        AddResourceTab<VariableResponse>("Variables", new VariableViewModel(client));
        AddResourceTab<ProjectResponse>("Projects", new ProjectViewModel(client));
        AddResourceTab<SnapshotSummaryResponse>("Snapshots", new SnapshotViewModel(client));
        AddResourceTab<UserResponse>("Users", new UserViewModel(client));
        AddResourceTab<RoleResponse>("Roles", new RoleViewModel(client));
        AddResourceTab<ClientResponse>("Clients", new ClientViewModel(client));
        AddResourceTab<PatResponse>("PATs", new PatViewModel(client));
        AddResourceTab<AuditRecordResponse>("Audit", new AuditViewModel(client));

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
        var detailView = new ResourceDetailView<T>(viewModel, _app);

        listView.SelectionChanged += viewModel.SelectItem;

        container.Add(listView, detailView);

        var tab = new Tab
        {
            DisplayText = name,
            View = container
        };

        _tabView.AddTab(tab, false);
        _refreshables[tab] = listView;

        listView.RefreshList();
    }

    private void RefreshActiveTab()
    {
        if (_tabView.SelectedTab is { } selectedTab && _refreshables.TryGetValue(selectedTab, out var refreshable))
        {
            refreshable.Refresh();
        }
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
                   N         New item
                   E         Edit selected item
                   D         Delete selected item
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