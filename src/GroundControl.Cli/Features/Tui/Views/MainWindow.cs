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
    private readonly Tabs _tabView;
    private readonly Dictionary<View, IRefreshable> _refreshables = [];

    public MainWindow(IApplication app, string serverUrl, string authMethod, IGroundControlClient client)
    {
        _app = app;
        Title = "GroundControl";

        _tabView = new Tabs
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        AddResourceTab("Scopes", new ScopeViewModel(client));
        AddResourceTab("Groups", new GroupViewModel(client));
        AddResourceTab("Templates", new TemplateViewModel(client));
        AddResourceTab("Config Entries", new ConfigEntryViewModel(client));
        AddResourceTab("Variables", new VariableViewModel(client));
        AddResourceTab("Projects", new ProjectViewModel(client));
        AddResourceTab("Snapshots", new SnapshotViewModel(client));
        AddResourceTab("Users", new UserViewModel(client));
        AddResourceTab("Roles", new RoleViewModel(client));
        AddResourceTab("Clients", new ClientViewModel(client));
        AddResourceTab("PATs", new PatViewModel(client));
        AddResourceTab("Audit", new AuditViewModel(client));

        var firstTab = _tabView.TabCollection.FirstOrDefault();
        if (firstTab is not null)
        {
            _tabView.Value = firstTab;
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
            Height = Dim.Fill(),
            CanFocus = true,
            TabStop = TabBehavior.TabGroup,
            Title = name
        };

        var listView = new ResourceListView<T>(viewModel, _app);
        var detailView = new ResourceDetailView<T>(viewModel, _app);

        listView.SelectionChanged += viewModel.SelectItem;

        container.Add(listView, detailView);

        _tabView.Add(container);
        _refreshables[container] = listView;

        listView.RefreshList();
    }

    private void RefreshActiveTab()
    {
        if (_tabView.Value is { } selectedTab && _refreshables.TryGetValue(selectedTab, out var refreshable))
        {
            refreshable.Refresh();
        }
    }

    private void ShowHelpDialog()
    {
        using var dialog = new Dialog();
        dialog.Title = "Help — Keyboard Shortcuts";
        dialog.Width = 50;
        dialog.Height = 16;

        var helpText = new Label
        {
            Text = """
                   Q         Quit
                   F1        Show this help
                   ←/→       Switch tabs
                   Tab       Move focus between search and list
                   ↑/↓       Navigate list items
                   Ctrl+N    New item
                   Ctrl+E    Edit selected item
                   Ctrl+D    Delete selected item
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
        dialog.AddButton(okButton);
        _app.Run(dialog);
    }
}