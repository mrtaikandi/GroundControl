using System.Reflection;

namespace GroundControl.Api.Shared.OpenApi;

internal static class OpenApiGenerator
{
    public static bool IsGeneratingDocument => Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}