using GroundControl.Api.Shared.Extensions.Options;
using Microsoft.Extensions.Options;

namespace GroundControl.Api.Shared.Configuration;

internal static class GroundControlOptionsExtensions
{
    /// <summary>
    /// Registers and validates <see cref="GroundControlOptions" /> from the configuration.
    /// </summary>
    /// <returns>The bound <see cref="GroundControlOptions" /> instance for immediate use during startup.</returns>
    public static GroundControlOptions AddGroundControlOptions(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetRequiredSection(GroundControlOptions.SectionName);

        services.AddValidation();
        services.Configure<GroundControlOptions>(section);
        services.AddSingleton<IValidateOptions<GroundControlOptions>, GroundControlOptions.Validator>();

        var options = section.Get<GroundControlOptions>()
                     ?? throw new InvalidOperationException($"Failed to bind '{GroundControlOptions.SectionName}' configuration section.");

        GroundControlOptions.Validator.ThrowIfInvalid(options);

        return options;
    }
}