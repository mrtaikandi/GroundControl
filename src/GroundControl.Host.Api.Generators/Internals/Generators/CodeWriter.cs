using System.CodeDom.Compiler;
using Microsoft.CodeAnalysis.CSharp;

namespace GroundControl.Host.Api.Generators.Internals.Generators;

/// <summary>
/// Provides a fluent API for writing C# code with automatic indentation management.
/// </summary>
internal sealed class CodeWriter : IDisposable
{
    private readonly IndentedTextWriter _indentedWriter;
    private readonly TextWriter _writer;

    public CodeWriter(TextWriter? writer = null)
    {
        _writer = writer ?? new StringWriter();
        LanguageVersion = LanguageVersion.Latest;
        _indentedWriter = new IndentedTextWriter(_writer, new string(' ', 4));
    }

    public int CurrentIndent => _indentedWriter.Indent;

    public TextWriter InnerWriter => _indentedWriter.InnerWriter;

    public LanguageVersion LanguageVersion { get; set; }

    public string NewLine => _indentedWriter.NewLine;

    /// <inheritdoc />
    public void Dispose()
    {
        _writer.Dispose();
        _indentedWriter.Dispose();
    }

    public CodeWriter Indent(bool condition = true)
    {
        if (condition)
        {
            _indentedWriter.Indent++;
        }

        return this;
    }

    /// <inheritdoc />
    public override string ToString() => _writer.ToString() ?? string.Empty;

    public CodeWriter Unindent(bool condition = true)
    {
        if (condition)
        {
            _indentedWriter.Indent--;
        }

        return this;
    }

    public CodeWriter Write([StringSyntax("csharp")] string? value = null)
    {
        if (!TryWriteNormalizedMultiLine(value))
        {
            _indentedWriter.Write(value);
        }

        return this;
    }

    public CodeWriter Write(char value)
    {
        _indentedWriter.Write(value);
        return this;
    }

    public CodeWriter WriteClosingBracket() => WriteClosingBracket(true);

    public CodeWriter WriteClosingBracket(bool emitNewLine)
    {
        _indentedWriter.Indent--;

        if (emitNewLine)
        {
            _indentedWriter.WriteLine("}");
        }
        else
        {
            _indentedWriter.Write("}");
        }

        return this;
    }

    public CodeWriter WriteClosingBracketIf(bool condition)
    {
        if (condition)
        {
            WriteClosingBracket();
        }

        return this;
    }

    public CodeWriter WriteClosingParenthesis() => WriteLine(")");

    public CodeWriter WriteIf(
        bool condition,
        [StringSyntax("csharp")] string? value = null,
        [StringSyntax("csharp")] string? elseValue = null)
    {
        if (condition)
        {
            Write(value);
        }
        else if (elseValue != null)
        {
            Write(elseValue);
        }

        return this;
    }

    public CodeWriter WriteJoin(string separator, IEnumerable<string?> values)
    {
        _indentedWriter.Write(string.Join(separator, values));
        return this;
    }

    /// <summary>
    /// Writes a line of text with correct indentation. If the input contains multiple lines, each line will be
    /// written with proper indentation.
    /// If the input is null, an empty line will be written with normal indentation.
    /// <br />
    /// To write a new line with correct indentation, use <see cref="WriteNewLine" /> instead.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public CodeWriter WriteLine([StringSyntax("csharp")] string? value = null)
    {
        if (value is null)
        {
            _indentedWriter.WriteLineNoTabs(string.Empty);
        }
        else if (!TryWriteNormalizedMultiLine(value))
        {
            _indentedWriter.WriteLine(value);
        }

        return this;
    }

    public CodeWriter WriteLineIf(
        bool condition,
        [StringSyntax("csharp")] string? value = null,
        string? elseValue = null)
    {
        if (condition)
        {
            WriteLine(value);
        }
        else if (elseValue != null)
        {
            WriteLine(elseValue);
        }

        return this;
    }

    public CodeWriter WriteLineJoin(string separator, IEnumerable<string?> values)
    {
        using var enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return WriteLine();
        }

        var firstValue = enumerator.Current;
        if (!enumerator.MoveNext())
        {
            // Only one value is available; no need to join.
            return WriteLine(firstValue);
        }

        _indentedWriter.Write(firstValue);

        do
        {
            WriteLine(separator);
            _indentedWriter.Write(enumerator.Current);
        }
        while (enumerator.MoveNext());

        _indentedWriter.WriteLine();

        return this;
    }

    public CodeWriter WriteLines(IEnumerable<string?> values)
    {
        foreach (var value in values)
        {
            WriteLine(value);
        }

        return this;
    }

    public CodeWriter WriteLinesIf(bool condition, IEnumerable<string?> values)
    {
        if (condition)
        {
            WriteLines(values);
        }

        return this;
    }

    /// <summary>
    /// Writes a new line with correct indentation.
    /// </summary>
    public CodeWriter WriteNewLine()
    {
        _indentedWriter.WriteLine();
        return this;
    }

    /// <summary>
    /// Writes a new line with correct indentation if the specified condition is true.
    /// </summary>
    /// <param name="condition">A boolean value that determines whether to write a new line.</param>
    public CodeWriter WriteNewLineIf(bool condition)
    {
        if (condition)
        {
            WriteNewLine();
        }

        return this;
    }

    public CodeWriter WriteOpeningBracket()
    {
        _indentedWriter.WriteLine("{");
        _indentedWriter.Indent++;

        return this;
    }

    public CodeWriter WriteOpeningBracketIf(bool condition)
    {
        if (condition)
        {
            WriteOpeningBracket();
        }

        return this;
    }

    public CodeWriter WriteOpenParenthesis() => Write("(");

    public CodeWriter WriteRaw([StringSyntax("csharp")] string? value = null)
    {
        _indentedWriter.Write(value);
        return this;
    }

    public CodeWriter WriteWhitespace() =>
        Write(" ");

    internal bool TryWriteNormalizedMultiLine(string? value, string? prefix = null)
    {
        if (value is null)
        {
            return false;
        }

        if (value.IndexOfAny(['\r', '\n']) == -1)
        {
            return false;
        }

        var lines = value.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        if (lines.Length < 2)
        {
            return false;
        }

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) && line != lines[lines.Length - 1])
            {
                _indentedWriter.WriteLineNoTabs(string.Empty);
                continue;
            }

            // Write non-empty lines or the last line
            if (!string.IsNullOrEmpty(prefix))
            {
                _indentedWriter.Write(prefix);
            }

            _indentedWriter.WriteLine(line);
        }

        return true;
    }
}