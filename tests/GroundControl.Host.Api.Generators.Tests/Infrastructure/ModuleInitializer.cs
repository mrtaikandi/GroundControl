using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace GroundControl.Host.Api.Generators.Tests.Infrastructure;

internal static partial class ModuleInitializer
{
    [ModuleInitializer]
    internal static void Init()
    {
        VerifySourceGenerators.Initialize();

        // Scrub the version from GeneratedCodeAttribute so snapshots don't break on every version bump.
        VerifierSettings.ScrubLinesWithReplace(
            line => GeneratedCodeVersionRegex().Replace(line, "\"GroundControl.Host.Api.Generators\", \"1.0.0.0\""));
    }

    [GeneratedRegex("""GroundControl\.Host\.Api\.Generators",\s*"\d+\.\d+\.\d+\.\d+""")]
    private static partial Regex GeneratedCodeVersionRegex();
}