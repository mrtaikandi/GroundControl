namespace GroundControl.Link.Tests.Internals;

public sealed class ConfigurationParserTests
{
    [Fact]
    public void Parse_NestedObject_ProducesColonSeparatedKeys()
    {
        // Arrange
        const string Json = """{"data": {"Db": {"value": {"Host": "localhost", "Port": "5432"}}}}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.Count.ShouldBe(2);
        result.Config["Db:Host"].Value.ShouldBe("localhost");
        result.Config["Db:Port"].Value.ShouldBe("5432");
        result.SnapshotVersion.ShouldBeNull();
    }

    [Fact]
    public void Parse_DeeplyNestedObject_ProducesMultiLevelKeys()
    {
        // Arrange
        var json = """{"data": {"A": {"value": {"B": {"C": "deep"}}}}}""";

        // Act
        var result = ConfigurationParser.Parse(json);

        // Assert
        result.Config.Count.ShouldBe(1);
        result.Config["A:B:C"].Value.ShouldBe("deep");
    }

    [Fact]
    public void Parse_ArrayElements_UseNumericIndex()
    {
        // Arrange
        const string Json = """{"data": {"Hosts": {"value": ["alpha", "beta", "gamma"]}}}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.Count.ShouldBe(3);
        result.Config["Hosts:0"].Value.ShouldBe("alpha");
        result.Config["Hosts:1"].Value.ShouldBe("beta");
        result.Config["Hosts:2"].Value.ShouldBe("gamma");
    }

    [Fact]
    public void Parse_ArrayOfObjects_UsesNumericIndexWithPropertyName()
    {
        // Arrange
        const string Json = """{"data": {"Servers": {"value": [{"Host": "a"}, {"Host": "b"}]}}}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.Count.ShouldBe(2);
        result.Config["Servers:0:Host"].Value.ShouldBe("a");
        result.Config["Servers:1:Host"].Value.ShouldBe("b");
    }

    [Fact]
    public void Parse_NullValues_AreOmitted()
    {
        // Arrange
        const string Json = """{"data": {"Present": {"value": "yes"}, "Missing": {"value": null}, "Also": {"value": "here"}}}""";

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
        const string Json = """{"data": {"Enabled": {"value": true}, "Count": {"value": 42}, "Rate": {"value": 3.14}}}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.Count.ShouldBe(3);
        result.Config["Enabled"].Value.ShouldBe("True");
        result.Config["Count"].Value.ShouldBe("42");
        result.Config["Rate"].Value.ShouldBe("3.14");
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
        const string Json = """{"data": {"Simple": {"value": "value"}, "Another": {"value": "one"}}}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config.Count.ShouldBe(2);
        result.Config["Simple"].Value.ShouldBe("value");
        result.Config["Another"].Value.ShouldBe("one");
    }

    [Fact]
    public void Parse_KeysAreCaseInsensitive()
    {
        // Arrange
        var json = """{"data": {"MyKey": {"value": "value"}}}""";

        // Act
        var result = ConfigurationParser.Parse(json);

        // Assert
        result.Config["mykey"].Value.ShouldBe("value");
        result.Config["MYKEY"].Value.ShouldBe("value");
    }

    [Fact]
    public void Parse_ValidJson_ReturnsEntriesAndVersion()
    {
        // Arrange
        const string Json = """{"data":{"Key1":{"value":"Value1"},"Key2":{"value":"Value2"}},"snapshotVersion":42}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config["Key1"].Value.ShouldBe("Value1");
        result.Config["Key2"].Value.ShouldBe("Value2");
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
        const string Json = """{"data":{"K":{"value":"V"}}}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config["K"].Value.ShouldBe("V");
        result.SnapshotVersion.ShouldBeNull();
    }

    [Fact]
    public void Parse_NestedData_Flattens()
    {
        // Arrange
        const string Json = """{"data":{"Logging":{"value":{"LogLevel":{"Default":"Warning"}}}},"snapshotVersion":1}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config["Logging:LogLevel:Default"].Value.ShouldBe("Warning");
        result.SnapshotVersion.ShouldBe("1");
    }

    [Fact]
    public void Parse_SensitiveFlag_PropagatesToEntry()
    {
        // Arrange
        const string Json = """{"data":{"Api:Key":{"value":"secret","isSensitive":true},"Api:Url":{"value":"https://example.com"}}}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config["Api:Key"].Value.ShouldBe("secret");
        result.Config["Api:Key"].IsSensitive.ShouldBeTrue();
        result.Config["Api:Url"].Value.ShouldBe("https://example.com");
        result.Config["Api:Url"].IsSensitive.ShouldBeFalse();
    }

    [Fact]
    public void Parse_SensitiveNestedObject_PropagatesSensitivityToAllLeaves()
    {
        // Arrange
        const string Json = """{"data":{"Db":{"value":{"Host":"h","Password":"p"},"isSensitive":true}}}""";

        // Act
        var result = ConfigurationParser.Parse(Json);

        // Assert
        result.Config["Db:Host"].IsSensitive.ShouldBeTrue();
        result.Config["Db:Password"].IsSensitive.ShouldBeTrue();
    }
}