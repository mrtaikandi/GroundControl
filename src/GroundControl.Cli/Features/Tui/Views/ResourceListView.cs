using System.Collections.ObjectModel;
using GroundControl.Cli.Features.Tui.ViewModels;
using Terminal.Gui.App;
using Terminal.Gui.Input;
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
    private readonly ResourceFormDialog _formDialog;
    private readonly DeleteConfirmationDialog _deleteDialog;
    private int _lastSelectedIndex = -1;

    public ResourceListView(ResourceViewModel<T> viewModel, IApplication app)
    {
        _viewModel = viewModel;
        _app = app;
        _formDialog = new ResourceFormDialog(app);
        _deleteDialog = new DeleteConfirmationDialog(app);
        Title = "List";
        X = 0;
        Y = 0;
        Width = Dim.Percent(40);
        Height = Dim.Fill();
        CanFocus = true;
        TabStop = TabBehavior.TabGroup;

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

        _listView.KeyDown += OnListViewKeyDown;
        _listView.Accepting += OnListViewAccepting;

        KeyDown += OnKeyDown;

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
        _ = Task.Run(async () =>
        {
            try
            {
                await _viewModel.LoadAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _app.Invoke(() => _statusLabel.Text = $"Error: {ex.Message}");
            }
        });
    }

    private void UpdateFilter()
    {
        _viewModel.Filter = _searchField.Text;
        UpdateListView();
    }

    private void OnKeyDown(object? sender, Key args)
    {
        if (args == Key.N.WithCtrl)
        {
            args.Handled = true;
            HandleCreate();
            return;
        }

        if (args == Key.E.WithCtrl)
        {
            args.Handled = true;
            HandleEdit();
            return;
        }

        if (args == Key.D.WithCtrl)
        {
            args.Handled = true;
            HandleDelete();
        }
    }

    private void OnListViewKeyDown(object? sender, Key args)
    {
        // Defer selection check to after the key is processed
        _app.Invoke(() => HandleSelectionChangeIfNeeded());
    }

    private void OnListViewAccepting(object? sender, EventArgs args)
    {
        HandleSelectionChangeIfNeeded();
    }

    private void HandleCreate()
    {
        var fields = _viewModel.GetFormFields();
        var result = _formDialog.Show($"New {_viewModel.ResourceTypeName}", fields);
        if (result is null)
        {
            return;
        }

        ExecuteCrudOperation(async () =>
        {
            await _viewModel.CreateAsync(result).ConfigureAwait(false);
            await _viewModel.LoadAsync().ConfigureAwait(false);
        });
    }

    private void HandleEdit()
    {
        if (_viewModel.SelectedItem is not { } selectedItem)
        {
            return;
        }

        var fields = _viewModel.GetEditFormFields(selectedItem);
        var result = _formDialog.Show($"Edit {_viewModel.ResourceTypeName}", fields);
        if (result is null)
        {
            return;
        }

        ExecuteCrudOperation(async () =>
        {
            await _viewModel.UpdateAsync(selectedItem, result).ConfigureAwait(false);
            await _viewModel.LoadAsync().ConfigureAwait(false);
        });
    }

    private void HandleDelete()
    {
        if (_viewModel.SelectedItem is not { } selectedItem)
        {
            return;
        }

        var resourceName = _viewModel.GetResourceName(selectedItem);
        if (!_deleteDialog.Show(_viewModel.ResourceTypeName, resourceName))
        {
            return;
        }

        ExecuteCrudOperation(async () =>
        {
            await _viewModel.DeleteAsync(selectedItem).ConfigureAwait(false);
            await _viewModel.LoadAsync().ConfigureAwait(false);
        });
    }

    private void ExecuteCrudOperation(Func<Task> operation)
    {
        _statusLabel.Text = "Working...";
        _ = Task.Run(async () =>
        {
            try
            {
                await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _app.Invoke(() =>
                {
                    UpdateListView();
                    ShowErrorDialog(ex.Message);
                });
            }
        });
    }

    private void ShowErrorDialog(string message)
    {
        MessageBox.ErrorQuery(_app, "Error", message, "OK");
    }

    private void HandleSelectionChangeIfNeeded()
    {
        var selectedIndex = _listView.SelectedItem ?? -1;
        if (selectedIndex == _lastSelectedIndex)
        {
            return;
        }

        _lastSelectedIndex = selectedIndex;
        SelectionChanged?.Invoke(selectedIndex);

        // Trigger load-more when near the end of the list
        if (_viewModel.HasMore && !_viewModel.IsLoading && selectedIndex >= _viewModel.Items.Count - 2)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _viewModel.LoadMoreAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _app.Invoke(() => _statusLabel.Text = $"Error: {ex.Message}");
                }
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