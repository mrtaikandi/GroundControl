using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GroundControl.Persistence.MongoDb;

/// <summary>
/// Configuration options for MongoDB storage.
/// </summary>
public sealed partial class MongoDbOptions
{
    /// <summary>
    /// Gets the configuration section name for MongoDB options.
    /// </summary>
    public const string SectionName = "Persistence:MongoDb";

    /// <summary>
    /// Gets or sets the prefix for all collection names.
    /// </summary>
    public string? CollectionPrefix { get; set; }

    /// <summary>
    /// Gets or sets the MongoDB connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the key for the connection string in the configuration.
    /// </summary>
    /// <remarks>
    /// If <see cref="ConnectionString" /> is not set, this key will be used to retrieve the connection string from the configuration.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    public string ConnectionStringKey { get; set; } = "Storage";

    /// <summary>
    /// Gets or sets the MongoDB database name.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string DatabaseName { get; set; } = "GroundControl";

    [OptionsValidator]
    internal sealed partial class Validator : IValidateOptions<MongoDbOptions>;
}