namespace GroundControl.Host.Cli.Internals.Spectre;

using SpectreFormats = global::Spectre.Console.ExceptionFormats;

internal static class ExceptionFormatsExtensions
{
    extension(ExceptionFormats formats)
    {
        public SpectreFormats ToSpectreExceptionFormats() => formats switch
        {
            ExceptionFormats.Default => SpectreFormats.Default,
            ExceptionFormats.ShortenPaths => SpectreFormats.ShortenPaths,
            ExceptionFormats.ShortenTypes => SpectreFormats.ShortenTypes,
            ExceptionFormats.ShortenMethods => SpectreFormats.ShortenMethods,
            ExceptionFormats.ShowLinks => SpectreFormats.ShowLinks,
            ExceptionFormats.ShortenEverything => SpectreFormats.ShortenEverything,
            ExceptionFormats.NoStackTrace => SpectreFormats.NoStackTrace,
            _ => throw new ArgumentOutOfRangeException(nameof(formats), formats, null)
        };
    }
}