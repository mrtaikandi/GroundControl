using GroundControl.Host.Cli;

namespace GroundControl.Cli.Tests.Shared;

public sealed class OutputFormattingTests
{
    [Fact]
    public void RenderTable_WithTableFormat_RendersSpectreTable()
    {
        // Arrange
        var builder = new MockShellBuilder();
        var shell = builder.Build();
        List<(string Name, int Age)> items = [("Alice", 30), ("Bob", 25)];
        List<string> headers = ["Name", "Age"];
        List<Func<(string Name, int Age), string>> extractors = [i => i.Name, i => i.Age.ToString()];

        // Act
        shell.RenderTable(items, headers, extractors, OutputFormat.Table);

        // Assert
        var output = builder.GetOutput();
        output.ShouldContain("Name");
        output.ShouldContain("Age");
        output.ShouldContain("Alice");
        output.ShouldContain("30");
        output.ShouldContain("Bob");
        output.ShouldContain("25");
    }

    [Fact]
    public void RenderTable_WithJsonFormat_RendersJson()
    {
        // Arrange
        var builder = new MockShellBuilder();
        var shell = builder.Build();
        var items = new[] { new { Name = "Alice", Age = 30 } };
        List<string> headers = ["Name"];
        List<Func<object, string>> extractors = [_ => string.Empty];

        // Act
        shell.RenderTable(items, headers, extractors, OutputFormat.Json);

        // Assert
        var output = builder.GetOutput();
        output.ShouldContain("\"Name\"");
        output.ShouldContain("\"Alice\"");
        output.ShouldContain("30");
    }

    [Fact]
    public void RenderDetail_WithTableFormat_RendersKeyValueTable()
    {
        // Arrange
        var builder = new MockShellBuilder();
        var shell = builder.Build();
        List<(string Key, string Value)> pairs = [("Name", "Alice"), ("Status", "Active")];

        // Act
        shell.RenderDetail(pairs, OutputFormat.Table);

        // Assert
        var output = builder.GetOutput();
        output.ShouldContain("Name");
        output.ShouldContain("Alice");
        output.ShouldContain("Status");
        output.ShouldContain("Active");
    }

    [Fact]
    public void RenderDetail_WithJsonFormat_RendersKeyValueJson()
    {
        // Arrange
        var builder = new MockShellBuilder();
        var shell = builder.Build();
        List<(string Key, string Value)> pairs = [("Name", "Alice"), ("Status", "Active")];

        // Act
        shell.RenderDetail(pairs, OutputFormat.Json);

        // Assert
        var output = builder.GetOutput();
        output.ShouldContain("\"Name\"");
        output.ShouldContain("\"Alice\"");
        output.ShouldContain("\"Status\"");
        output.ShouldContain("\"Active\"");
    }

    [Fact]
    public void RenderJson_RendersPrettyPrintedJson()
    {
        // Arrange
        var builder = new MockShellBuilder();
        var shell = builder.Build();
        var value = new { Name = "Alice", Age = 30 };

        // Act
        shell.RenderJson(value);

        // Assert
        var output = builder.GetOutput();
        output.ShouldContain("\"Name\"");
        output.ShouldContain("\"Alice\"");
        output.ShouldContain("\"Age\"");
        output.ShouldContain("30");
        output.ShouldContain("\n");
    }

    [Fact]
    public void RenderTable_WithEmptyItems_RendersHeadersOnly()
    {
        // Arrange
        var builder = new MockShellBuilder();
        var shell = builder.Build();
        List<string> items = [];
        List<string> headers = ["Name", "Age"];
        List<Func<string, string>> extractors = [i => i, _ => "0"];

        // Act
        shell.RenderTable(items, headers, extractors, OutputFormat.Table);

        // Assert
        var output = builder.GetOutput();
        output.ShouldContain("Name");
        output.ShouldContain("Age");
    }

    [Fact]
    public void RenderTable_EscapesMarkup()
    {
        // Arrange
        var builder = new MockShellBuilder();
        var shell = builder.Build();
        List<string> items = ["<script>alert</script>"];
        List<string> headers = ["Value"];
        List<Func<string, string>> extractors = [i => i];

        // Act
        shell.RenderTable(items, headers, extractors, OutputFormat.Table);

        // Assert
        var output = builder.GetOutput();
        output.ShouldContain("<script>alert</script>");
    }
}