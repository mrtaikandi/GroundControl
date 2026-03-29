// ReSharper disable CheckNamespace
#pragma warning disable IDE0130

namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Indicates that a string parameter, return value, or field
/// should be interpreted according to a specific syntax.
/// This is a polyfill for .NET 7's StringSyntaxAttribute.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.ReturnValue)]
internal sealed class StringSyntaxAttribute : Attribute
{
    /// <summary>
    /// The syntax identifier (e.g., "Regex", "DateTimeFormat", "Json").
    /// </summary>
    public string Syntax { get; }

    /// <summary>
    /// Optional arguments for the syntax.
    /// </summary>
    public object[] Arguments { get; }

    public StringSyntaxAttribute(string syntax)
    {
        Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
        Arguments = [];
    }

    public StringSyntaxAttribute(string syntax, params object[] arguments)
    {
        Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
        Arguments = arguments ?? Array.Empty<object>();
    }
}