using System.Text.Json;
using JsonSchemaObjectModel.Keywords;

namespace JsonSchemaObjectModel.Tests;

/// <summary>
/// The paths that avoid allocations must give the same results as the simple paths.
/// </summary>
public class AllocationPathTests
{
    [Fact]
    public void Builder_values_are_written_directly_and_created_as_elements_on_request()
    {
        var guid = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
        JsonSchema schema = new JsonSchemaBuilder().Properties(
            ("guid", new JsonSchemaBuilder().Const(guid)),
            ("text", new JsonSchemaBuilder().Const("a \"quoted\" value")),
            ("null", new JsonSchemaBuilder().Const((string?)null)),
            ("integer", new JsonSchemaBuilder().Const(42)),
            ("decimal", new JsonSchemaBuilder().Const(1.50m)),
            ("double", new JsonSchemaBuilder().Const(0.25)),
            ("boolean", new JsonSchemaBuilder().Const(false)),
            ("enum", new JsonSchemaBuilder().Enum("A", "B", "C"))
        );

        AssertJsonEqual(
            """{"properties":{"guid":{"const":"3f2504e0-4f89-11d3-9a0c-0305e82c3301"},"text":{"const":"a \"quoted\" value"},"null":{"const":null},"integer":{"const":42},"decimal":{"const":1.50},"double":{"const":0.25},"boolean":{"const":false},"enum":{"enum":["A","B","C"]}}}""",
            schema.ToJsonString()
        );

        Assert.Equal(guid, schema.FindSubschema("/properties/guid")!.GetKeyword<ConstKeyword>()!.Value.GetGuid());
        Assert.Equal(JsonValueKind.Null, schema.FindSubschema("/properties/null")!.GetKeyword<ConstKeyword>()!.Value.ValueKind);
        Assert.Equal(1.50m, schema.FindSubschema("/properties/decimal")!.GetKeyword<ConstKeyword>()!.Value.GetDecimal());
        var values = schema.FindSubschema("/properties/enum")!.GetKeyword<EnumKeyword>()!.Values;
        Assert.Equal(["A", "B", "C"], values.Select(value => value.GetString()));
        Assert.Equal(values, schema.FindSubschema("/properties/enum")!.GetKeyword<EnumKeyword>()!.Values);
    }

    [Fact]
    public void Single_type_keywords_are_shared()
    {
        Assert.Same(
            JsonSchema.Parse("""{"type":"string"}""").GetKeyword<TypeKeyword>(),
            new JsonSchemaBuilder().Type(SchemaValueType.String).Build().GetKeyword<TypeKeyword>()
        );
    }

    [Fact]
    public void ToJsonString_works_when_a_keyword_calls_it_while_writing()
    {
        var inner = new JsonSchemaBuilder().Type(SchemaValueType.String).Build();
        JsonSchema schema = new JsonSchemaBuilder().Add(new NestedWriterKeyword(inner)).MinLength(1);

        AssertJsonEqual("""{"x-nested":"{\"type\":\"string\"}","minLength":1}""", schema.ToJsonString());
        AssertJsonEqual("""{"x-nested":"{\"type\":\"string\"}","minLength":1}""", schema.ToJsonString());
    }

    [Fact]
    public void ToJsonString_is_safe_on_many_threads()
    {
        var schemas = Enumerable.Range(0, 64).Select(index => new JsonSchemaBuilder().Title($"schema {index}").Build()).ToArray();

        var results = schemas.AsParallel().Select(schema => schema.ToJsonString()).ToArray();

        Assert.Equal(schemas.Select((_, index) => $$"""{"title":"schema {{index}}"}"""), results);
    }

    [Fact]
    public void FindSubschema_finds_properties_in_large_maps()
    {
        var names = Enumerable.Range(0, 40).Select(index => $"property{index}").ToArray();
        JsonSchema schema = new JsonSchemaBuilder().Properties(names.Select(name => (name, (JsonSchema)new JsonSchemaBuilder().Title(name))));

        foreach (var name in names)
        {
            Assert.Equal(name, schema.FindSubschema($"/properties/{name}")!.GetKeyword<TitleKeyword>()!.Value);
        }

        Assert.Null(schema.FindSubschema("/properties/property40"));
    }

    [Fact]
    public void Escaped_and_long_keyword_names_are_read()
    {
        var longName = new string('k', 100);
        var json = $$"""{"type":"string","{{longName}}":1}""";

        var schema = JsonSchema.Parse(json);

        Assert.IsType<TypeKeyword>(schema.Keywords[0]);
        Assert.Equal(longName, Assert.IsType<UnknownKeyword>(schema.Keywords[1]).Name);
    }

    [Fact]
    public void Parse_rejects_trailing_content()
    {
        Assert.ThrowsAny<JsonException>(() => JsonSchema.Parse("""{"type":"string"} {}"""));
    }

    private static void AssertJsonEqual(string expected, string actual)
    {
        using var expectedDocument = JsonDocument.Parse(expected);
        using var actualDocument = JsonDocument.Parse(actual);
        Assert.True(JsonElement.DeepEquals(expectedDocument.RootElement, actualDocument.RootElement), $"expected: {expected}{Environment.NewLine}actual:   {actual}");
    }

    private sealed class NestedWriterKeyword(JsonSchema inner) : IJsonSchemaKeyword
    {
        public string Name => "x-nested";

        public void WriteValue(Utf8JsonWriter writer) => writer.WriteStringValue(inner.ToJsonString());
    }
}
