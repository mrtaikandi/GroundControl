using Microsoft.Extensions.Options;

namespace GroundControl.Api.Extensions.Options;

internal static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void AddOptions<TOptions>(TOptions options) where TOptions : class
        {
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        }

        public void AddOptions<TOptions, TValidator>(TOptions options, string? name = null)
            where TOptions : class
            where TValidator : IValidateOptions<TOptions>, new()
        {
            var validator = new TValidator();
            var validationResult = validator.Validate(name, options);

            if (validationResult.Failed)
            {
                var optionsType = options.GetType();
                throw new OptionsValidationException(name ?? optionsType.Name, optionsType, validationResult.Failures);
            }

            services.AddOptions(options);
        }
    }
}