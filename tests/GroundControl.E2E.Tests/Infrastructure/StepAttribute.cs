namespace GroundControl.E2E.Tests.Infrastructure;

/// <summary>
/// Specifies the execution order for a test method within an ordered scenario workflow.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class StepAttribute : Attribute
{
    public StepAttribute(int order)
    {
        Order = order;
    }

    /// <summary>
    /// Gets the execution order. Lower values run first.
    /// </summary>
    public int Order { get; }
}
