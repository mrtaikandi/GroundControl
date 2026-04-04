namespace GroundControl.Cli.Features.Tui.ViewModels;

internal readonly record struct DetailPair(string Key, string Value);

internal readonly record struct EntityMetadata(
    long Version,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

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

    internal abstract IReadOnlyList<DetailPair> GetDetailPairs(T item);

    internal abstract string ResourceTypeName { get; }

    internal abstract IReadOnlyList<FieldDefinition> GetFormFields();

    internal abstract IReadOnlyList<FieldDefinition> GetEditFormFields(T item);

    internal abstract string GetResourceName(T item);

    internal abstract Task CreateAsync(
        Dictionary<string, string> fieldValues,
        CancellationToken cancellationToken = default);

    internal abstract Task UpdateAsync(
        T item,
        Dictionary<string, string> fieldValues,
        CancellationToken cancellationToken = default);

    internal abstract Task DeleteAsync(T item, CancellationToken cancellationToken = default);

    protected abstract bool MatchesFilter(T item, string filter);

    protected static IReadOnlyList<DetailPair> GetStandardMetadataPairs(EntityMetadata metadata) =>
    [
        new("Version", metadata.Version.ToString(CultureInfo.InvariantCulture)),
        new("Created At", metadata.CreatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Created By", metadata.CreatedBy.ToString()),
        new("Updated At", metadata.UpdatedAt.ToString("u", CultureInfo.InvariantCulture)),
        new("Updated By", metadata.UpdatedBy.ToString())
    ];

    protected static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    protected static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var guid) ? guid : null;

    protected static List<Guid> ParseGuidList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => Guid.TryParse(s, out _))
            .Select(Guid.Parse)
            .ToList();
    }

    protected static List<string> ParseCommaSeparated(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    protected static bool? ParseBool(string? value) =>
        bool.TryParse(value, out var result) ? result : null;

    protected static TEnum ParseEnum<TEnum>(string? value, TEnum defaultValue) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var result) ? result : defaultValue;

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