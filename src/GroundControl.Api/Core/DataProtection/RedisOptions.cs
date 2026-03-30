using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Core.DataProtection;

/// <summary>
/// Redis-specific options for Data Protection key ring storage.
/// </summary>
internal sealed partial class RedisOptions
{
    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Redis key name for stored keys.
    /// </summary>
    [Required]
    public string KeyName { get; set; } = "groundcontrol-data-protection";

    /// <summary>
    /// Gets or sets the connection timeout in milliseconds.
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 5000;

    [OptionsValidator]
    public sealed partial class Validator : IValidateOptions<RedisOptions>;
}