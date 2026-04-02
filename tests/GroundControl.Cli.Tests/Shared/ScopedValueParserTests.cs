using GroundControl.Cli.Shared.Parsing;

namespace GroundControl.Cli.Tests.Shared;

public sealed class ScopedValueParserTests
{
    [Fact]
    public void ParseSingle_DefaultScope_ReturnsEmptyScopes()
    {
        // Arrange & Act
        var result = ScopedValueParser.ParseSingle("default=myValue");

        // Assert
        result.Scopes.ShouldBeEmpty();
        result.Value.ShouldBe("myValue");
    }

    [Fact]
    public void ParseSingle_DefaultScope_CaseInsensitive()
    {
        // Arrange & Act
        var result = ScopedValueParser.ParseSingle("DEFAULT=myValue");

        // Assert
        result.Scopes.ShouldBeEmpty();
        result.Value.ShouldBe("myValue");
    }

    [Fact]
    public void ParseSingle_SingleScope_ParsesDimensionAndValue()
    {
        // Arrange & Act
        var result = ScopedValueParser.ParseSingle("environment:Production=connection-string");

        // Assert
        result.Scopes.Count.ShouldBe(1);
        result.Scopes["environment"].ShouldBe("Production");
        result.Value.ShouldBe("connection-string");
    }

    [Fact]
    public void ParseSingle_MultipleScopes_ParsesAll()
    {
        // Arrange & Act
        var result = ScopedValueParser.ParseSingle("environment:Production,region:EU=db-prod-eu.example.com");

        // Assert
        result.Scopes.Count.ShouldBe(2);
        result.Scopes["environment"].ShouldBe("Production");
        result.Scopes["region"].ShouldBe("EU");
        result.Value.ShouldBe("db-prod-eu.example.com");
    }

    [Fact]
    public void ParseSingle_ValueWithEqualsSign_PreservesValueContent()
    {
        // Arrange & Act
        var result = ScopedValueParser.ParseSingle("default=Server=localhost;Database=mydb");

        // Assert
        result.Scopes.ShouldBeEmpty();
        result.Value.ShouldBe("Server=localhost;Database=mydb");
    }

    [Fact]
    public void ParseSingle_EmptyValue_ReturnsEmptyString()
    {
        // Arrange & Act
        var result = ScopedValueParser.ParseSingle("default=");

        // Assert
        result.Scopes.ShouldBeEmpty();
        result.Value.ShouldBe(string.Empty);
    }

    [Fact]
    public void ParseSingle_MissingEquals_ThrowsFormatException()
    {
        // Arrange & Act & Assert
        var ex = Should.Throw<FormatException>(() => ScopedValueParser.ParseSingle("noequals"));
        ex.Message.ShouldContain("Invalid scoped value format");
    }

    [Fact]
    public void ParseSingle_MissingColonInScope_ThrowsFormatException()
    {
        // Arrange & Act & Assert
        var ex = Should.Throw<FormatException>(() => ScopedValueParser.ParseSingle("environment=value"));
        ex.Message.ShouldContain("Invalid scope qualifier");
    }

    [Fact]
    public void ParseSingle_EmptyDimension_ThrowsFormatException()
    {
        // Arrange & Act & Assert
        var ex = Should.Throw<FormatException>(() => ScopedValueParser.ParseSingle(":Production=value"));
        ex.Message.ShouldContain("Both dimension and value are required");
    }

    [Fact]
    public void ParseSingle_EmptyScopeValue_ThrowsFormatException()
    {
        // Arrange & Act & Assert
        var ex = Should.Throw<FormatException>(() => ScopedValueParser.ParseSingle("environment:=value"));
        ex.Message.ShouldContain("Both dimension and value are required");
    }

    [Fact]
    public void ParseSingle_DuplicateDimension_ThrowsFormatException()
    {
        // Arrange & Act & Assert
        var ex = Should.Throw<FormatException>(
            () => ScopedValueParser.ParseSingle("environment:A,environment:B=value"));

        ex.Message.ShouldContain("Duplicate scope dimension");
    }

    [Fact]
    public void ParseSingle_WhitespaceInput_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() => ScopedValueParser.ParseSingle("  "));
    }

    [Fact]
    public void Parse_MultipleValues_ParsesAll()
    {
        // Arrange
        var values = new List<string>
        {
            "default=localhost",
            "environment:Production=db-prod.example.com"
        };

        // Act
        var result = ScopedValueParser.Parse(values, valuesJson: null);

        // Assert
        result.Count.ShouldBe(2);
        result[0].Scopes.ShouldBeEmpty();
        result[0].Value.ShouldBe("localhost");
        result[1].Scopes.Count.ShouldBe(1);
        result[1].Value.ShouldBe("db-prod.example.com");
    }

    [Fact]
    public void Parse_NullValues_ReturnsEmptyList()
    {
        // Arrange & Act
        var result = ScopedValueParser.Parse(null, valuesJson: null);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_EmptyValues_ReturnsEmptyList()
    {
        // Arrange & Act
        var result = ScopedValueParser.Parse([], valuesJson: null);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_JsonInput_DeserializesCorrectly()
    {
        // Arrange
        var json = """
            [
                { "scopes": {}, "value": "localhost" },
                { "scopes": { "environment": "Production" }, "value": "db-prod.example.com" }
            ]
            """;

        // Act
        var result = ScopedValueParser.Parse(null, valuesJson: json);

        // Assert
        result.Count.ShouldBe(2);
        result[0].Scopes.ShouldBeEmpty();
        result[0].Value.ShouldBe("localhost");
        result[1].Scopes["environment"].ShouldBe("Production");
        result[1].Value.ShouldBe("db-prod.example.com");
    }

    [Fact]
    public void Parse_JsonInput_MissingValue_ThrowsFormatException()
    {
        // Arrange
        var json = """[{ "scopes": {} }]""";

        // Act & Assert
        var ex = Should.Throw<FormatException>(() => ScopedValueParser.Parse(null, valuesJson: json));
        ex.Message.ShouldContain("non-null 'value'");
    }

    [Fact]
    public void Parse_JsonInput_InvalidJson_ThrowsFormatException()
    {
        // Arrange & Act & Assert
        var ex = Should.Throw<FormatException>(() => ScopedValueParser.Parse(null, valuesJson: "not json"));
        ex.Message.ShouldContain("Invalid JSON input");
    }

    [Fact]
    public void Parse_JsonInput_TakesPrecedenceOverValues()
    {
        // Arrange
        var values = new List<string> { "default=fromValues" };
        var json = """[{ "scopes": {}, "value": "fromJson" }]""";

        // Act
        var result = ScopedValueParser.Parse(values, valuesJson: json);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Value.ShouldBe("fromJson");
    }
}