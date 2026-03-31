using Spectre.Console;
using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace GroundControl.Host.Cli.Extensions.Spectre;

/// <summary>
/// Provides extensions for the <see cref="PromptExtensions"/> class.
/// </summary>
public static class PromptExtensions
{
    /// <summary>
    /// Sets the validation criteria for the prompt.
    /// </summary>
    /// <typeparam name="T">The prompt result type.</typeparam>
    /// <param name="obj">The prompt.</param>
    /// <param name="validator">The validation criteria.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static TextPrompt<T> Validate<T>(this TextPrompt<T> obj, Func<T, ValidationResult> validator)
    {
        ArgumentNullException.ThrowIfNull(obj);

        obj.Validator = input =>
        {
            var validationResult = validator(input);
            return validationResult == ValidationResult.Success
                ? global::Spectre.Console.ValidationResult.Success()
                : global::Spectre.Console.ValidationResult.Error(validationResult.ErrorMessage ?? "Invalid input.");
        };

        return obj;
    }
}