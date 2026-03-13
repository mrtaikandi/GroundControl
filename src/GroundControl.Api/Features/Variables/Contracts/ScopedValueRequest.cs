using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.Variables.Contracts;

internal sealed record ScopedValueRequest
{
    public Dictionary<string, string> Scopes { get; init; } = [];

    [Required]
    public required string Value { get; init; }
}