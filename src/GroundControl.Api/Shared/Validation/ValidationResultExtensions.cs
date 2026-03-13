using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Shared.Validation;

internal static class ValidationResultExtensions
{
    extension(ValidationResult)
    {
        /// <summary>
        /// Represents the success of the validation.
        /// </summary>
        /// <returns>A <see cref="ValidationResult"/> indicating success.</returns>
        public static ValidationResult Success() => ValidationResult.Success!;

        /// <summary>
        /// Creates a <see cref="ValidationResult"/> representing a validation error.
        /// </summary>
        /// <param name="error">The error message.</param>
        /// <param name="memberNames">The member names associated with the error.</param>
        /// <returns>A <see cref="ValidationResult"/> with the specified error.</returns>
        public static ValidationResult Error(string error, params IEnumerable<string>? memberNames) =>
            new(error, memberNames);
    }
}