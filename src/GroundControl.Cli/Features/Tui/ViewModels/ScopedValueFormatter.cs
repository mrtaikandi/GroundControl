using GroundControl.Api.Client.Contracts;

namespace GroundControl.Cli.Features.Tui.ViewModels;

internal static class ScopedValueFormatter
{
    internal static string Format(ICollection<ScopedValue> values)
    {
        if (values.Count == 0)
        {
            return "-";
        }

        return string.Join("; ", values.Select(v =>
        {
            var scope = v.Scopes is { Count: > 0 }
                ? string.Join(", ", v.Scopes.Select(s => $"{s.Key}={s.Value}"))
                : "default";
            return $"[{scope}] {v.Value}";
        }));
    }
}