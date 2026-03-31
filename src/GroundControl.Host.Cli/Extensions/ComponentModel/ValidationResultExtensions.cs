using System.ComponentModel.DataAnnotations;

namespace GroundControl.Host.Cli.Extensions.ComponentModel;

/// <summary>
/// Provides extension methods for the <see cref="ValidationResult"/> class to facilitate the creation of validation results.
/// </summary>
public static class ValidationResultExtensions
{
    extension(ValidationResult validationResult)
    {
        /// <summary>
        /// Gets the successful <see cref="ValidationResult"/> instance.
        /// </summary>
        /// <returns>
        /// A successful <see cref="ValidationResult"/>.
        /// </returns>
        public static ValidationResult Success() => ValidationResult.Success!;

        /// <summary>
        /// Creates an error <see cref="ValidationResult"/> for the specified message and members.
        /// </summary>
        /// <param name="error">
        /// The validation error message.
        /// </param>
        /// <param name="memberNames">
        /// The member names associated with the validation error.
        /// </param>
        /// <returns>
        /// A failed <see cref="ValidationResult"/> containing the error details.
        /// </returns>
        public static ValidationResult Error(string error, IEnumerable<string>? memberNames = null) =>
            new ValidationResult(error, memberNames);
    }
}