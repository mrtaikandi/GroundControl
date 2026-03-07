using System.ComponentModel.DataAnnotations;
using GroundControl.Api.Shared.Security;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Shared.Configuration;

/// <summary>
/// Root configuration options for GroundControl.
/// </summary>
internal sealed partial class GroundControlOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "GroundControl";

    /// <summary>
    /// Gets or sets the security options.
    /// </summary>
    [Required]
    [ValidateObjectMembers]
    public SecurityOptions Security { get; set; } = new();

    [OptionsValidator]
    internal sealed partial class Validator : IValidateOptions<GroundControlOptions>;
}