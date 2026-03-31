namespace GroundControl.Api.Core.Validation;

/// <summary>
/// Defines an asynchronous validator for endpoints without a request body.
/// </summary>
/// <remarks>
/// Unlike <see cref="IAsyncValidator{T}"/> which validates a typed request body,
/// this interface validates using route values, headers, and other request context.
/// </remarks>
internal interface IEndpointValidator
{
    /// <summary>
    /// Validates the current request asynchronously.
    /// </summary>
    /// <param name="context">The validation context providing access to the HTTP context.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValidatorResult"/> indicating success or the type of failure.</returns>
    Task<ValidatorResult> ValidateAsync(ValidationContext context, CancellationToken cancellationToken = default);
}