using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using GroundControl.Persistence.Contracts;

namespace GroundControl.Api.Features.Variables.Contracts;

internal sealed partial record CreateVariableRequest : IValidatableObject
{
    [Required]
    [MaxLength(200)]
    public required string Name { get; init; }

    [Required]
    public required VariableScope Scope { get; init; }

    public Guid? GroupId { get; init; }

    public Guid? ProjectId { get; init; }

    [Required]
    public required List<ScopedValueRequest> Values { get; init; }

    public bool IsSensitive { get; init; }

    [MaxLength(500)]
    public string? Description { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var sv in Values)
        {
            if (InterpolationPattern.IsMatch(sv.Value))
            {
                yield return new ValidationResult(
                    "Variable values cannot contain {{...}} interpolation placeholders.",
                    [nameof(Values)]);

                yield break;
            }
        }
    }

    [GeneratedRegex(@"\{\{[^}]+\}\}")]
    private static partial Regex InterpolationPattern { get; }
}