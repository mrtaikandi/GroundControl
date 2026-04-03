using System.Collections.ObjectModel;
using GroundControl.Cli.Features.Tui.ViewModels;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace GroundControl.Cli.Features.Tui.Views;

#pragma warning disable CA2000, CA2213 // Terminal.Gui containers dispose their children

internal sealed class ResourceListView<T> : FrameView, IRefreshable
{
    private readonly ResourceViewModel<T> _viewModel;
    private readonly IApplication _app;
    private readonly ListView _listView;
    private readonly TextField _searchField;
    private readonly Label _statusLabel;

    public ResourceListView(ResourceViewModel<T> viewModel, IApplication app)
    {
        _viewModel = viewModel;
        _app = app;
        Title = "List";
        X = 0;
        Y = 0;
        Width = Dim.Percent(40);
        Height = Dim.Fill();

        _searchField = new TextField
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = string.Empty
        };

        _searchField.HasFocusChanged += (_, args) =>
        {
            if (!args.NewValue)
            {
                UpdateFilter();
            }
        };

        _searchField.Accepting += (_, _) => UpdateFilter();

        _listView = new ListView
        {
            X = 0,
            Y = Pos.Bottom(_searchField),
            Width = Dim.Fill(),
            Height = Dim.Fill(1)
        };

        // Track selection via keyboard navigation
        _listView.KeyDown += OnListViewKeyDown;
        _listView.Accepting += OnListViewAccepting;

        _statusLabel = new Label
        {
            X = 0,
            Y = Pos.AnchorEnd(),
            Width = Dim.Fill(),
            Height = 1,
            Text = "Loading..."
        };

        Add(_searchField);
        Add(_listView);
        Add(_statusLabel);

        _viewModel.ItemsChanged += OnItemsChanged;
    }

    public event Action<int>? SelectionChanged;

    public void Refresh() => RefreshList();

    public void RefreshList()
    {
        _statusLabel.Text = "Loading...";
        Task.Run(async () =>
        {
            await _viewModel.LoadAsync().ConfigureAwait(false);
            _app.Invoke(() => UpdateListView());
        });
    }

    private void UpdateFilter()
    {
        _viewModel.Filter = _searchField.Text;
        UpdateListView();
    }

    private void OnListViewKeyDown(object? sender, Terminal.Gui.Input.Key args)
    {
        // After key processing, the SelectedItem may have changed
        _app.Invoke(() => HandleSelectionChange());
    }

    private void OnListViewAccepting(object? sender, EventArgs args)
    {
        HandleSelectionChange();
    }

    private void HandleSelectionChange()
    {
        var selectedIndex = _listView.SelectedItem ?? -1;
        SelectionChanged?.Invoke(selectedIndex);

        // Trigger load-more when near the end of the list
        if (_viewModel.HasMore && !_viewModel.IsLoading && selectedIndex >= _viewModel.Items.Count - 2)
        {
            Task.Run(async () =>
            {
                await _viewModel.LoadMoreAsync().ConfigureAwait(false);
                _app.Invoke(() => UpdateListView());
            });
        }
    }

    private void OnItemsChanged()
    {
        _app.Invoke(() => UpdateListView());
    }

    private void UpdateListView()
    {
        var items = _viewModel.Items;
        var displayItems = new ObservableCollection<string>();
        foreach (var item in items)
        {
            displayItems.Add(_viewModel.GetDisplayText(item));
        }

        _listView.SetSource(displayItems);

        if (_viewModel.ErrorMessage is not null)
        {
            _statusLabel.Text = $"Error: {_viewModel.ErrorMessage}";
        }
        else if (_viewModel.IsLoading)
        {
            _statusLabel.Text = "Loading...";
        }
        else
        {
            var moreIndicator = _viewModel.HasMore ? "+" : "";
            _statusLabel.Text = $"{items.Count}{moreIndicator} items";
        }
    }
}