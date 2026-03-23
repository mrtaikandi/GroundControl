using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Features.Auth.Contracts;

internal sealed record RefreshRequest
{
    [Required(AllowEmptyStrings = false)]
    public required string RefreshToken { get; init; }
}