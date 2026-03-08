namespace GroundControl.Persistence;

/// <summary>
/// Thrown when a store operation violates a unique key constraint.
/// </summary>
public sealed class DuplicateKeyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateKeyException" /> class.
    /// </summary>
    public DuplicateKeyException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateKeyException" /> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DuplicateKeyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateKeyException" /> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DuplicateKeyException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}