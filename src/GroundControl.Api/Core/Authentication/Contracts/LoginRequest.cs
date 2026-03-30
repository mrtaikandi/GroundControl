using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Core.Authentication.Contracts;

internal sealed record LoginRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(256)]
    public required string Username { get; init; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(256)]
    public required string Password { get; init; }
}