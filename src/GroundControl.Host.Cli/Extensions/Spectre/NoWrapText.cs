using Spectre.Console;
using Spectre.Console.Rendering;

namespace GroundControl.Host.Cli.Extensions.Spectre;

/// <summary>
/// A renderable that outputs styled text segments without performing width-based wrapping,
/// allowing the terminal to handle line wrapping natively. This ensures correct reflow on
/// terminal resize unlike <see cref="Paragraph"/> or <see cref="Text"/> which insert hard line breaks.
/// </summary>
internal sealed class NoWrapText : IRenderable
{
    private readonly List<Segment> _segments = [];

    public NoWrapText Append(string text, Style? style = null)
    {
        _segments.Add(new Segment(text, style ?? Style.Plain));
        return this;
    }

    Measurement IRenderable.Measure(RenderOptions options, int maxWidth) => new(0, maxWidth);

    IEnumerable<Segment> IRenderable.Render(RenderOptions options, int maxWidth)
    {
        foreach (var segment in _segments)
        {
            yield return segment;
        }

        yield return Segment.LineBreak;
    }
}