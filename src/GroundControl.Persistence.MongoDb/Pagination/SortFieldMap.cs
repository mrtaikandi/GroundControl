using System.ComponentModel.DataAnnotations;
using MongoDB.Driver;

namespace GroundControl.Persistence.MongoDb.Pagination;

/// <summary>
/// Declarative mapping of sortable fields for a given entity type.
/// Handles normalization, BSON field mapping, value extraction, and collation.
/// </summary>
internal sealed class SortFieldMap<TEntity>
{
    private readonly string _defaultField;
    private readonly Dictionary<string, SortFieldEntry> _fields;
    private readonly Dictionary<string, string> _aliases;

    private SortFieldMap(string defaultField, Dictionary<string, SortFieldEntry> fields, Dictionary<string, string> aliases)
    {
        _defaultField = defaultField;
        _fields = fields;
        _aliases = aliases;
    }

    /// <summary>
    /// Creates a new <see cref="SortFieldMap{TEntity}"/> using the builder API.
    /// </summary>
    /// <param name="defaultField">The default sort field when none is specified.</param>
    /// <param name="configure">Builder configuration action.</param>
    public static SortFieldMap<TEntity> Build(string defaultField, Action<Builder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultField);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new Builder();
        configure(builder);

        var fields = builder.Fields;
        var aliases = builder.Aliases;

        if (!fields.ContainsKey(defaultField) && !aliases.ContainsKey(defaultField))
        {
            throw new ArgumentException($"Default field '{defaultField}' must be a registered field or alias.", nameof(defaultField));
        }

        return new SortFieldMap<TEntity>(defaultField, fields, aliases);
    }

    /// <summary>
    /// Normalizes the sort field from user input to a canonical field name.
    /// Returns the default field when input is null or whitespace.
    /// </summary>
    public string Normalize(string? sortField)
    {
        if (string.IsNullOrWhiteSpace(sortField))
        {
            return _defaultField;
        }

        var trimmed = sortField.Trim();

        // Check aliases first
        foreach (var alias in _aliases.Where(alias => trimmed.Equals(alias.Key, StringComparison.OrdinalIgnoreCase)))
        {
            return alias.Value;
        }

        // Check fields
        foreach (var field in _fields.Where(field => trimmed.Equals(field.Key, StringComparison.OrdinalIgnoreCase)))
        {
            return field.Key;
        }

        throw new ValidationException($"SortField '{sortField}' is not supported.");
    }

    /// <summary>
    /// Gets the BSON document field name for the given normalized sort field.
    /// </summary>
    public string GetBsonField(string normalizedField) => _fields.TryGetValue(normalizedField, out var entry)
        ? entry.BsonField
        : throw new ValidationException($"SortField '{normalizedField}' is not supported.");

    /// <summary>
    /// Extracts the sort value from an entity for cursor encoding.
    /// </summary>
    public object GetSortValue(TEntity entity, string normalizedField) => _fields.TryGetValue(normalizedField, out var entry)
        ? entry.ValueExtractor(entity)
        : throw new ValidationException($"SortField '{normalizedField}' is not supported.");

    /// <summary>
    /// Gets the collation to use for the given sort field, or null if none is needed.
    /// </summary>
    public Collation? GetCollation(string normalizedField, IMongoDbContext context) => _fields.TryGetValue(normalizedField, out var entry) && entry.UseCollation
        ? context.DefaultCollation
        : null;

    internal sealed class Builder
    {
        internal Dictionary<string, SortFieldEntry> Fields { get; } = new(StringComparer.Ordinal);

        internal Dictionary<string, string> Aliases { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Registers a sortable field.
        /// </summary>
        /// <param name="name">Logical field name (case-sensitive canonical form).</param>
        /// <param name="bsonField">BSON document field name (e.g. "_id" for Id).</param>
        /// <param name="valueExtractor">Function to extract the sort value from an entity.</param>
        /// <param name="collation">Whether this field requires case-insensitive collation.</param>
        public Builder Field(string name, string bsonField, Func<TEntity, object> valueExtractor, bool collation = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(bsonField);
            ArgumentNullException.ThrowIfNull(valueExtractor);

            Fields[name] = new SortFieldEntry(bsonField, valueExtractor, collation);
            return this;
        }

        /// <summary>
        /// Registers an alias that maps to an existing field.
        /// </summary>
        /// <param name="alias">The alias name (e.g. "name").</param>
        /// <param name="target">The target field name it resolves to (e.g. "dimension").</param>
        public Builder Alias(string alias, string target)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(alias);
            ArgumentException.ThrowIfNullOrWhiteSpace(target);

            Aliases[alias] = target;
            return this;
        }
    }

    internal sealed record SortFieldEntry(string BsonField, Func<TEntity, object> ValueExtractor, bool UseCollation);
}