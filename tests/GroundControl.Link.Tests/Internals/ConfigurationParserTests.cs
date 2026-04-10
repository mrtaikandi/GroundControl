namespace GroundControl.Link.Tests.Internals;

public sealed class ConfigurationParserTests
{
    [Fact]
    public void Parse_NestedObject_ProducesColonSeparatedKeys()
    {
        // Arrange
        const string Json = """{"data": {"Db": {"Host": "localhost", "Port": "5432"}}}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.Count.ShouldBe(2);
        result.Config["Db:Host"].ShouldBe("localhost");
        result.Config["Db:Port"].ShouldBe("5432");
        result.SnapshotVersion.ShouldBeNull();
    }

    [Fact]
    public void Parse_DeeplyNestedObject_ProducesMultiLevelKeys()
    {
        // Arrange
        var json = """{"data": {"A": {"B": {"C": "deep"}}}}""";

        // Act
        var result = ConfigurationParser.Parse(json);

        // Assert
        result.Config.Count.ShouldBe(1);
        result.Config["A:B:C"].ShouldBe("deep");
    }

    [Fact]
    public void Parse_ArrayElements_UseNumericIndex()
    {
        // Arrange
        const string Json = """{"data": {"Hosts": ["alpha", "beta", "gamma"]}}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.Count.ShouldBe(3);
        result.Config["Hosts:0"].ShouldBe("alpha");
        result.Config["Hosts:1"].ShouldBe("beta");
        result.Config["Hosts:2"].ShouldBe("gamma");
    }

    [Fact]
    public void Parse_ArrayOfObjects_UsesNumericIndexWithPropertyName()
    {
        // Arrange
        const string Json = """{"data": {"Servers": [{"Host": "a"}, {"Host": "b"}]}}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.Count.ShouldBe(2);
        result.Config["Servers:0:Host"].ShouldBe("a");
        result.Config["Servers:1:Host"].ShouldBe("b");
    }

    [Fact]
    public void Parse_NullValues_AreOmitted()
    {
        // Arrange
        const string Json = """{"data": {"Present": "yes", "Missing": null, "Also": "here"}}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.Count.ShouldBe(2);
        result.Config.ShouldContainKey("Present");
        result.Config.ShouldContainKey("Also");
        result.Config.ShouldNotContainKey("Missing");
    }

    [Fact]
    public void Parse_BooleanAndNumericValues_ConvertedToString()
    {
        // Arrange
        const string Json = """{"data": {"Enabled": true, "Count": 42, "Rate": 3.14}}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.Count.ShouldBe(3);
        result.Config["Enabled"].ShouldBe("True");
        result.Config["Count"].ShouldBe("42");
        result.Config["Rate"].ShouldBe("3.14");
    }

    [Fact]
    public void Parse_EmptyDataObject_ReturnsEmptyConfig()
    {
        // Arrange
        const string Json = """{"data": {}, "snapshotVersion": 1}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.ShouldBeEmpty();
        result.SnapshotVersion.ShouldBe("1");
    }

    [Fact]
    public void Parse_MissingDataProperty_ReturnsEmptyConfig()
    {
        // Arrange
        const string Json = """{"snapshotVersion": 1}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.ShouldBeEmpty();
        result.SnapshotVersion.ShouldBe("1");
    }

    [Fact]
    public void Parse_FlatKeyValues_ReturnedDirectly()
    {
        // Arrange
        const string Json = """{"data": {"Simple": "value", "Another": "one"}}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.Count.ShouldBe(2);
        result.Config["Simple"].ShouldBe("value");
        result.Config["Another"].ShouldBe("one");
    }

    [Fact]
    public void Parse_KeysAreCaseInsensitive()
    {
        // Arrange
        var json = """{"data": {"MyKey": "value"}}""";

        // Act
        var result = ConfigurationParser.Parse(json);

        // Assert
        result.Config["mykey"].ShouldBe("value");
        result.Config["MYKEY"].ShouldBe("value");
    }

    [Fact]
    public void Parse_ValidJson_ReturnsEntriesAndVersion()
    {
        // Arrange
        const string Json = """{"data":{"Key1":"Value1","Key2":"Value2"},"snapshotVersion":42}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.ShouldContainKeyAndValue("Key1", "Value1");
        result.Config.ShouldContainKeyAndValue("Key2", "Value2");
        result.SnapshotVersion.ShouldBe("42");
    }

    [Fact]
    public void Parse_NoDataProperty_ReturnsEmptyConfigWithVersion()
    {
        // Arrange
        const string Json = """{"snapshotVersion":1}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.ShouldBeEmpty();
        result.SnapshotVersion.ShouldBe("1");
    }

    [Fact]
    public void Parse_NoVersion_ReturnsNullSnapshotVersion()
    {
        // Arrange
        const string Json = """{"data":{"K":"V"}}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.ShouldContainKeyAndValue("K", "V");
        result.SnapshotVersion.ShouldBeNull();
    }

    [Fact]
    public void Parse_NestedData_Flattens()
    {
        // Arrange
        const string Json = """{"data":{"Logging":{"LogLevel":{"Default":"Warning"}}},"snapshotVersion":1}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.ShouldContainKeyAndValue("Logging:LogLevel:Default", "Warning");
        result.SnapshotVersion.ShouldBe("1");
    }
}