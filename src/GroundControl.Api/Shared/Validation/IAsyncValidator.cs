using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Shared.Validation;

/// <summary>
/// Defines an asynchronous validator for a specific request type.
/// </summary>
/// <typeparam name="T">The type to validate.</typeparam>
internal interface IAsyncValidator<in T>
{
    /// <summary>
    /// Validates the specified instance asynchronously.
    /// </summary>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A list of <see cref="ValidationResult"/> representing validation failures.
    /// An empty list indicates the instance is valid.
    /// </returns>
    Task<IReadOnlyList<ValidationResult>> ValidateAsync(T instance, CancellationToken cancellationToken = default);
}