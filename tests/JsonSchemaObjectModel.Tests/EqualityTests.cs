using System.Text.Json;
using JsonSchemaObjectModel.Keywords;

namespace JsonSchemaObjectModel.Tests;

/// <summary>
/// Two schemas are equal if, and only if, they write equal JSON, as <see cref="JsonElement.DeepEquals"/> defines it.
/// </summary>
public class EqualityTests
{
    //  A registry with no readers makes every keyword an UnknownKeyword. Its schemas compare by JSON only.
    private static readonly JsonSchemaReaderOptions untyped = new(new JsonSchemaKeywordRegistry());

    public static TheoryData<string, int> Cases => TestSuite.SchemaCases();

    [Theory]
    [MemberData(nameof(Cases))]
    public void Typed_and_untyped_parses_of_a_schema_are_equal_with_the_same_hash_code(string file, int group)
    {
        var json = TestSuite.LoadSchema(file, group);

        var typed = JsonSchema.FromJsonElement(json);
        var untypedSchema = JsonSchema.FromJsonElement(json, untyped);
        var reparsed = JsonSchema.Parse(typed.ToJsonString());

        AssertEqual(typed, untypedSchema);
        AssertEqual(untypedSchema, typed);
        AssertEqual(typed, reparsed);
    }

    [Fact]
    public void Equality_agrees_with_DeepEquals_for_all_pairs_of_schemas_in_each_Test_Suite_file()
    {
        var comparisons = 0;
        var cases = TestSuite.SchemaCases().Select(row => row.Data).ToArray();
        foreach (var file in cases.Select(item => item.Item1).Distinct())
        {
            var originals = cases.Where(item => item.Item1 == file).Select(item => TestSuite.LoadSchema(file, item.Item2)).ToArray();
            var schemas = originals.Select(original => JsonSchema.FromJsonElement(original)).ToArray();

            for (var left = 0; left < schemas.Length; left++)
            {
                for (var right = 0; right < schemas.Length; right++)
                {
                    var expected = JsonElement.DeepEquals(originals[left], originals[right]);
                    Assert.True(
                        expected == schemas[left].Equals(schemas[right]),
                        $"{file}: {originals[left]} and {originals[right]}"
                    );
                    comparisons++;
                }
            }
        }

        Assert.True(comparisons > 1000);
    }

    [Theory]
    [InlineData("""{"type":"string","minLength":1}""", """{"minLength":1,"type":"string"}""")]
    [InlineData("""{"maximum":1}""", """{"maximum":1.0}""")]
    [InlineData("""{"maximum":1e400}""", """{"maximum":10e399}""")]
    [InlineData("""{"const":{"a":1,"b":[1,2]}}""", """{"const":{"b":[1.0,2],"a":1e0}}""")]
    [InlineData("""{"title":"A"}""", """{"title":"A"}""")]
    [InlineData("""{"properties":{"a":true,"b":false}}""", """{"properties":{"b":false,"a":true}}""")]
    [InlineData("""{"x-rating":{"stars":5,"note":"Good"}}""", """{"x-rating":{"note":"Good","stars":5.0}}""")]
    [InlineData("""{"x-big":1e99999999999}""", """{"x-big":10e99999999998}""")]
    public void Schemas_with_equal_JSON_are_equal(string left, string right)
    {
        AssertEqual(JsonSchema.Parse(left), JsonSchema.Parse(right));
    }

    [Theory]
    [InlineData("true", "{}")]
    [InlineData("true", "false")]
    [InlineData("""{"type":"string"}""", """{"type":["string"]}""")]
    [InlineData("""{"type":["string","null"]}""", """{"type":["null","string"]}""")]
    [InlineData("""{"enum":["A","B"]}""", """{"enum":["B","A"]}""")]
    [InlineData("""{"required":["a"]}""", """{"required":["a"],"title":"t"}""")]
    [InlineData("""{"items":{"minLength":1}}""", """{"items":{"minLength":2}}""")]
    [InlineData("""{"const":1}""", """{"const":"1"}""")]
    [InlineData("""{"x-big":1e99999999999}""", """{"x-big":1e99999999998}""")]
    public void Schemas_with_different_JSON_are_not_equal(string left, string right)
    {
        Assert.NotEqual(JsonSchema.Parse(left), JsonSchema.Parse(right));
    }

    [Fact]
    public void A_built_schema_equals_the_parse_of_the_same_JSON()
    {
        JsonSchema built = new JsonSchemaBuilder()
            .Schema(SchemaKeyword.Draft202012)
            .Type(SchemaValueType.Object)
            .Properties(
                ("dish", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1)),
                ("price", new JsonSchemaBuilder().Type(SchemaValueType.Number).Minimum(0).MultipleOf(0.01m)),
                ("size", new JsonSchemaBuilder().Enum("S", "M", "L")),
                ("currency", new JsonSchemaBuilder().Const("THB")),
                ("quantity", new JsonSchemaBuilder().Const(1.50m)),
                ("orderId", new JsonSchemaBuilder().Const(Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301")))
            )
            .Required("dish", "price")
            .AdditionalProperties(false);

        var parsed = JsonSchema.Parse(
            """
            {"required":["dish","price"],"additionalProperties":false,"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object",
             "properties":{"price":{"type":"number","minimum":0.0,"multipleOf":1e-2},"dish":{"minLength":1,"type":"string"},
             "size":{"enum":["S","M","L"]},"currency":{"const":"THB"},"quantity":{"const":1.5},
             "orderId":{"const":"3f2504e0-4f89-11d3-9a0c-0305e82c3301"}}}
            """
        );

        AssertEqual(built, parsed);
        AssertEqual(built, JsonSchema.Parse(parsed.ToJsonString(), untyped));
    }

    [Fact]
    public void A_custom_keyword_compares_by_its_JSON()
    {
        var options = new JsonSchemaReaderOptions(JsonSchemaKeywordRegistry.CreateDraft202012().Add<PlaceholderKeyword>());
        const string json = """{"type":"string","placeholder":"Pad thai"}""";

        var custom = JsonSchema.Parse(json, options);
        var unknown = JsonSchema.Parse(json);

        Assert.IsType<PlaceholderKeyword>(custom.Keywords[1]);
        Assert.IsType<UnknownKeyword>(unknown.Keywords[1]);
        AssertEqual(custom, unknown);
        AssertEqual(custom, JsonSchema.Parse(json, options));
        Assert.NotEqual(custom, JsonSchema.Parse("""{"type":"string","placeholder":"Green curry"}""", options));
    }

    [Fact]
    public void SchemaMap_equality_ignores_the_order_of_entries()
    {
        var first = SchemaMap.Create(("starter", true), ("main", new JsonSchemaBuilder().MinLength(1)));
        var second = SchemaMap.Create(("main", new JsonSchemaBuilder().MinLength(1)), ("starter", true));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, SchemaMap.Create(("starter", true)));
    }

    [Fact]
    public void Equal_schemas_work_as_dictionary_keys()
    {
        var cache = new Dictionary<JsonSchema, string> { [JsonSchema.Parse("""{"type":"string","maxLength":10}""")] = "short text" };

        Assert.Equal("short text", cache[JsonSchema.Parse("""{"maxLength":10.0,"type":"string"}""")]);
    }

    private static void AssertEqual(JsonSchema expected, JsonSchema actual)
    {
        Assert.True(expected.Equals(actual), $"expected: {expected}{Environment.NewLine}actual:   {actual}");
        Assert.True(expected.Equals((object)actual));
        Assert.Equal(expected.GetHashCode(), actual.GetHashCode());
    }

    private sealed class PlaceholderKeyword(string placeholder) : IJsonSchemaKeyword<PlaceholderKeyword>
    {
        public static string KeywordName => "placeholder";

        public string Name => KeywordName;

        public string Placeholder { get; } = placeholder;

        public static PlaceholderKeyword Read(JsonElement value, JsonSchemaReadContext context) => new(context.ReadString(value));

        public void WriteValue(Utf8JsonWriter writer) => writer.WriteStringValue(Placeholder);
    }
}
