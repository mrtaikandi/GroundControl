namespace GroundControl.E2E.Tests.Infrastructure;

/// <summary>
/// Writes lines to xUnit's <see cref="ITestOutputHelper"/> with buffering.
/// When <see cref="TestContext.Current"/> has no output helper (e.g. during fixture init),
/// lines are buffered. Once the helper becomes available, the buffer is flushed and
/// subsequent lines stream directly.
/// </summary>
internal sealed class TestOutputWriter
{
    private readonly string _prefix;
    private readonly List<string> _buffer = [];
    private bool _flushed;

    public TestOutputWriter(string? prefix = null)
    {
        _prefix = prefix ?? string.Empty;
    }

    public void WriteLine(string line)
    {
        var output = TestContext.Current.TestOutputHelper;
        if (output is null)
        {
            _buffer.Add(line);
            return;
        }

        if (!_flushed)
        {
            foreach (var buffered in _buffer)
            {
                output.WriteLine($"{_prefix} {buffered}");
            }

            _buffer.Clear();
            _flushed = true;
        }

        output.WriteLine($"{_prefix} {line}");
    }
}