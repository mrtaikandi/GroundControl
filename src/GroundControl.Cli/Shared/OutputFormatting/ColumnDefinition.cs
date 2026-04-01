namespace GroundControl.Cli.Shared.OutputFormatting;

/// <summary>
/// Defines a table column with a header and a function to extract the display value from an entity.
/// </summary>
/// <typeparam name="T">The type of entity the column renders.</typeparam>
/// <param name="Header">The column header text.</param>
/// <param name="ValueExtractor">A function that extracts the display value from an instance of <typeparamref name="T"/>.</param>
public sealed record ColumnDefinition<T>(string Header, Func<T, string> ValueExtractor);