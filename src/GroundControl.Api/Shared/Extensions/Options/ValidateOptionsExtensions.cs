using Microsoft.Extensions.Options;

namespace GroundControl.Api.Shared.Extensions.Options;

internal static class ValidateOptionsExtensions
{
    extension<T, TOption>(T validator)
        where T : IValidateOptions<TOption>, new()
        where TOption : class
    {
        public static void ThrowIfInvalid(TOption option, string? name = null)
        {
            var instance = new T();
            var validationResult = instance.Validate(name, option);
            if (validationResult.Failed)
            {
                throw new OptionsValidationException(typeof(T).Name, typeof(T), validationResult.Failures);
            }
        }

        public static bool TryValidate(TOption option, out IEnumerable<string> failures, string? name = null)
        {
            var instance = new T();
            var validationResult = instance.Validate(name, option);
            failures = validationResult.Failures ?? [];

            return validationResult.Succeeded;
        }
    }
}