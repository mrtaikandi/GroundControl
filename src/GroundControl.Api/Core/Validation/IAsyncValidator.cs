namespace GroundControl.Api.Core.Validation;

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
    /// <param name="context">The validation context providing access to the HTTP context.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValidatorResult"/> indicating success or the type of failure.</returns>
    Task<ValidatorResult> ValidateAsync(T instance, ValidationContext context, CancellationToken cancellationToken = default);
}