namespace GroundControl.Host.Cli;

/// <summary>
/// The exception that is thrown when a required set of choices is empty.
/// </summary>
public class EmptyChoicesException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmptyChoicesException" /> class.
    /// </summary>
    public EmptyChoicesException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmptyChoicesException" /> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public EmptyChoicesException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmptyChoicesException" /> class with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    public EmptyChoicesException(string message, Exception inner)
        : base(message, inner)
    {
    }
}