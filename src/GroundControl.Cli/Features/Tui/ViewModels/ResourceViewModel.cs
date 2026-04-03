namespace GroundControl.Cli.Features.Tui.ViewModels;

internal abstract class ResourceViewModel<T>
{
    private readonly List<T> _allItems = [];
    private string? _nextCursor;
    private string _filter = string.Empty;

    public IReadOnlyList<T> Items { get; private set; } = [];

    public T? SelectedItem { get; private set; }

    public bool HasMore { get; private set; }

    public bool IsLoading { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string Filter
    {
        get => _filter;
        set
        {
            _filter = value;
            ApplyFilter();
        }
    }

    public event Action? ItemsChanged;

    public event Action? SelectedItemChanged;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        _allItems.Clear();
        _nextCursor = null;
        SelectedItem = default;
        SelectedItemChanged?.Invoke();

        try
        {
            var (items, nextCursor) = await FetchPageAsync(null, cancellationToken).ConfigureAwait(false);
            _allItems.AddRange(items);
            _nextCursor = nextCursor;
            HasMore = nextCursor is not null;
            ApplyFilter();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = ex.Message;
            Items = [];
            ItemsChanged?.Invoke();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
    {
        if (!HasMore || IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var (items, nextCursor) = await FetchPageAsync(_nextCursor, cancellationToken).ConfigureAwait(false);
            _allItems.AddRange(items);
            _nextCursor = nextCursor;
            HasMore = nextCursor is not null;
            ApplyFilter();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SelectItem(int index)
    {
        if (index >= 0 && index < Items.Count)
        {
            SelectedItem = Items[index];
        }
        else
        {
            SelectedItem = default;
        }

        SelectedItemChanged?.Invoke();
    }

    protected abstract Task<(IReadOnlyList<T> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        CancellationToken cancellationToken);

    internal abstract string GetDisplayText(T item);

    internal abstract IReadOnlyList<KeyValuePair<string, string>> GetDetailPairs(T item);

    protected abstract bool MatchesFilter(T item, string filter);

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(_filter))
        {
            Items = _allItems.AsReadOnly();
        }
        else
        {
            Items = _allItems.Where(item => MatchesFilter(item, _filter)).ToList().AsReadOnly();
        }

        ItemsChanged?.Invoke();
    }
}