using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Users;

internal static class GrantParser
{
    internal static (List<GrantDto>? Grants, List<string>? InvalidValues) Parse(string[]? grantValues)
    {
        if (grantValues is null or { Length: 0 })
        {
            return (null, null);
        }

        var grants = new List<GrantDto>(grantValues.Length);
        List<string>? invalidValues = null;

        foreach (var value in grantValues)
        {
            if (Guid.TryParse(value, out var roleId))
            {
                grants.Add(new GrantDto { RoleId = roleId });
            }
            else
            {
                invalidValues ??= [];
                invalidValues.Add(value);
            }
        }

        return (grants.Count > 0 ? grants : null, invalidValues);
    }
}