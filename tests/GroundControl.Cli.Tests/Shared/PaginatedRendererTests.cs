using GroundControl.Cli.Shared.Pagination;
using GroundControl.Host.Cli;

namespace GroundControl.Cli.Tests.Shared;

public sealed class PaginatedRendererTests
{
    private sealed record TestItem(string Name, string Value);

    private static readonly IReadOnlyList<string> Headers = ["Name", "Value"];

    private static readonly IReadOnlyList<Func<TestItem, string>> ValueExtractors =
    [
        item => item.Name,
        item => item.Value
    ];

    [Fact]
    public async Task RenderPaginatedTableAsync_TableMode_SinglePage_RendersTable()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();

        var items = new List<TestItem> { new("key1", "val1"), new("key2", "val2") };

        Task<PaginatedRenderer.Page<TestItem>> FetchPage(string? cursor, CancellationToken ct)
        {
            return Task.FromResult(new PaginatedRenderer.Page<TestItem>(items, null));
        }

        // Act
        await shell.RenderPaginatedTableAsync(
            FetchPage, Headers, ValueExtractors, OutputFormat.Table,
            TestContext.Current.CancellationToken);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("key1");
        output.ShouldContain("val1");
        output.ShouldContain("key2");
        output.ShouldContain("val2");
        output.ShouldContain("Name");
        output.ShouldContain("Value");
    }

    [Fact]
    public async Task RenderPaginatedTableAsync_TableMode_MultiplePages_RendersAllRows()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();

        var page1 = new List<TestItem> { new("key1", "val1") };
        var page2 = new List<TestItem> { new("key2", "val2") };
        var pageCallCount = 0;

        Task<PaginatedRenderer.Page<TestItem>> FetchPage(string? cursor, CancellationToken ct)
        {
            pageCallCount++;
            return cursor is null
                ? Task.FromResult(new PaginatedRenderer.Page<TestItem>(page1, "cursor1"))
                : Task.FromResult(new PaginatedRenderer.Page<TestItem>(page2, null));
        }

        // Act
        await shell.RenderPaginatedTableAsync(
            FetchPage, Headers, ValueExtractors, OutputFormat.Table,
            TestContext.Current.CancellationToken);

        // Assert
        pageCallCount.ShouldBe(2);
        var output = shellBuilder.GetOutput();
        output.ShouldContain("key1");
        output.ShouldContain("key2");
    }

    [Fact]
    public async Task RenderPaginatedTableAsync_JsonMode_CollectsAllPagesIntoArray()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();

        var page1 = new List<TestItem> { new("key1", "val1") };
        var page2 = new List<TestItem> { new("key2", "val2") };

        Task<PaginatedRenderer.Page<TestItem>> FetchPage(string? cursor, CancellationToken ct)
        {
            return cursor is null
                ? Task.FromResult(new PaginatedRenderer.Page<TestItem>(page1, "cursor1"))
                : Task.FromResult(new PaginatedRenderer.Page<TestItem>(page2, null));
        }

        // Act
        await shell.RenderPaginatedTableAsync(
            FetchPage, Headers, ValueExtractors, OutputFormat.Json,
            TestContext.Current.CancellationToken);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("key1");
        output.ShouldContain("val1");
        output.ShouldContain("key2");
        output.ShouldContain("val2");
        // JSON output should be an array
        output.ShouldContain("[");
        output.ShouldContain("]");
    }

    [Fact]
    public async Task RenderPaginatedTableAsync_EmptyResult_RendersEmptyTable()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();

        Task<PaginatedRenderer.Page<TestItem>> FetchPage(string? cursor, CancellationToken ct)
        {
            return Task.FromResult(new PaginatedRenderer.Page<TestItem>([], null));
        }

        // Act
        await shell.RenderPaginatedTableAsync(
            FetchPage, Headers, ValueExtractors, OutputFormat.Table,
            TestContext.Current.CancellationToken);

        // Assert
        var output = shellBuilder.GetOutput();
        output.ShouldContain("Name");
        output.ShouldContain("Value");
    }

    [Fact]
    public async Task RenderPaginatedTableAsync_JsonMode_EmptyResult_RendersEmptyArray()
    {
        // Arrange
        var shellBuilder = new MockShellBuilder();
        var shell = shellBuilder.Build();

        Task<PaginatedRenderer.Page<TestItem>> FetchPage(string? cursor, CancellationToken ct)
        {
            return Task.FromResult(new PaginatedRenderer.Page<TestItem>([], null));
        }

        // Act
        await shell.RenderPaginatedTableAsync(
            FetchPage, Headers, ValueExtractors, OutputFormat.Json,
            TestContext.Current.CancellationToken);

        // Assert
        var output = shellBuilder.GetOutput();
        output.Trim().ShouldBe("[]");
    }
}