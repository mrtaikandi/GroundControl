namespace GroundControl.Cli.Features.Tui.ViewModels;

internal sealed class FieldDefinition
{
    public required string Label { get; init; }

    public required FieldType Type { get; init; }

    public bool IsRequired { get; init; }

    public string DefaultValue { get; init; } = string.Empty;
}

internal enum FieldType
{
    Text
}