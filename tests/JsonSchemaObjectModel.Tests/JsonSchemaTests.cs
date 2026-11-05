using System.Text.Json;
using JsonSchemaObjectModel.Keywords;

namespace JsonSchemaObjectModel.Tests;

public class JsonSchemaTests
{
    private static readonly JsonWriterOptions indented = new() { Indented = true };

    [Fact]
    public void Builder_writes_keywords_in_the_order_they_were_added()
    {
        JsonSchema schema = new JsonSchemaBuilder()
            .Schema(SchemaKeyword.Draft202012)
            .Type(SchemaValueType.Object)
            .Properties(
                ("id", new JsonSchemaBuilder().Const(Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"))),
                ("size", new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern("^[SML]$")),
                ("email", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Email)),
                (
                    "sides",
                    new JsonSchemaBuilder()
                        .Type(SchemaValueType.Array)
                        .PrefixItems(new JsonSchemaBuilder().Enum("FRIES", "SALAD"))
                        .MinItems(1)
                        .MaxItems(1)
                )
            )
            .Required("size", "email");

        Assert.Equal(
            """
            {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{"id":{"const":"3f2504e0-4f89-11d3-9a0c-0305e82c3301"},"size":{"type":"string","pattern":"^[SML]$"},"email":{"type":"string","format":"email"},"sides":{"type":"array","prefixItems":[{"enum":["FRIES","SALAD"]}],"minItems":1,"maxItems":1}},"required":["size","email"]}
            """,
            schema.ToJsonString()
        );
    }

    [Fact]
    public void Builder_replaces_a_keyword_in_place()
    {
        var schema = new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Type(SchemaValueType.Null).Build();

        Assert.Equal("""{"type":"null","minLength":1}""", schema.ToJsonString());
    }

    [Fact]
    public void Every_builder_method_writes_a_valid_draft_2020_12_schema_that_round_trips()
    {
        JsonSchema schema = new JsonSchemaBuilder()
            .Schema(SchemaKeyword.Draft202012)
            .Id("https://example.com/everything")
            .Anchor("root")
            .DynamicAnchor("meta")
            .Comment("Uses every builder method.")
            .Vocabulary(("https://json-schema.org/draft/2020-12/vocab/core", true))
            .Defs(("positive", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Integer).ExclusiveMinimum(0)))
            .AllOf(true, new JsonSchemaBuilder().Ref("#/$defs/positive"))
            .AnyOf(new JsonSchemaBuilder().DynamicRef("#meta"), false)
            .OneOf(true)
            .Not(false)
            .If(new JsonSchemaBuilder().Required("a"))
            .Then(new JsonSchemaBuilder().MinProperties(1))
            .Else(new JsonSchemaBuilder().MaxProperties(10))
            .DependentSchemas(("a", new JsonSchemaBuilder().Required("b")))
            .PrefixItems(true, false)
            .Items(new JsonSchemaBuilder().MultipleOf(0.5m).Minimum(-1.5m).Maximum(100).ExclusiveMaximum(101))
            .Contains(new JsonSchemaBuilder().Const(42))
            .MinContains(1)
            .MaxContains(3)
            .UniqueItems()
            .MinItems(1)
            .MaxItems(5)
            .Properties(("name", new JsonSchemaBuilder().MinLength(1).MaxLength(50)))
            .PatternProperties(("^x-", true))
            .AdditionalProperties(false)
            .PropertyNames(new JsonSchemaBuilder().Pattern("^[a-z]+$"))
            .UnevaluatedItems(false)
            .UnevaluatedProperties(false)
            .Enum(JsonValues.From(1), JsonValues.From("one"), JsonValues.Null)
            .DependentRequired(("a", ["b", "c"]))
            .Title("Everything")
            .Description("A schema that uses every builder method.")
            .Default(JsonValues.From(true))
            .Deprecated()
            .ReadOnly()
            .WriteOnly(false)
            .Examples(JsonValues.From(1.25), JsonValues.From(false))
            .Format(Formats.DateTime)
            .ContentEncoding("base64")
            .ContentMediaType("application/json")
            .ContentSchema(new JsonSchemaBuilder().Type(SchemaValueType.Object));

        var json = schema.ToJsonString(indented);

        var result = MetaSchemas.ValidateDraft202012(json);
        Assert.True(result.IsValid, MetaSchemas.Describe(result));
        Assert.Equal(json, JsonSchema.Parse(json).ToJsonString(indented));
        Assert.Equal(
            JsonSchemaKeywordRegistry.CreateDraft202012().KeywordNames.Order(StringComparer.Ordinal),
            KeywordNames(schema).Distinct().Order(StringComparer.Ordinal)
        );

        static IEnumerable<string> KeywordNames(JsonSchema schema) =>
            schema.Keywords.SelectMany(keyword =>
            {
                IEnumerable<string> descendants = keyword switch
                {
                    ISingleSubschemaKeyword single => KeywordNames(single.Schema),
                    ISubschemaArrayKeyword array => array.Schemas.SelectMany(KeywordNames),
                    ISubschemaMapKeyword map => map.Schemas.Values.SelectMany(KeywordNames),
                    _ => [],
                };
                return descendants.Prepend(keyword.Name);
            });
    }

    [Fact]
    public void Boolean_schemas_round_trip()
    {
        Assert.Same(JsonSchema.True, JsonSchema.Parse("true"));
        Assert.Same(JsonSchema.False, JsonSchema.Parse("false"));
        Assert.Equal("""{"not":true,"items":false}""", new JsonSchemaBuilder().Not(true).Items(false).Build().ToJsonString());
    }

    [Fact]
    public void Type_keeps_its_json_form()
    {
        Assert.Equal("""{"type":["string"]}""", JsonSchema.Parse("""{"type":["string"]}""").ToJsonString());
        Assert.Equal("""{"type":"string"}""", JsonSchema.Parse("""{"type":"string"}""").ToJsonString());
        Assert.Equal(
            SchemaValueType.String | SchemaValueType.Null,
            JsonSchema.Parse("""{"type":["null","string"]}""").Type
        );
        Assert.Equal(
            """{"type":["object","null"]}""",
            new JsonSchemaBuilder().Type(SchemaValueType.Null | SchemaValueType.Object).Build().ToJsonString()
        );
    }

    [Fact]
    public void Numbers_round_trip_beyond_decimal_and_double_precision()
    {
        const string json = """{"maximum":1e400,"minimum":0.10000000000000000000000000000000001,"minLength":2.0}""";

        var schema = JsonSchema.Parse(json);

        Assert.Equal("""{"maximum":1e400,"minimum":0.10000000000000000000000000000000001,"minLength":2}""", schema.ToJsonString());
        Assert.Equal(2UL, schema.GetKeyword<MinLengthKeyword>()!.Value);
    }

    [Theory]
    [InlineData("""{"minItems":-1}""", "/minItems")]
    [InlineData("""{"minItems":1.5}""", "/minItems")]
    [InlineData("""{"type":"text"}""", "/type")]
    [InlineData("""{"type":["string","string"]}""", "/type")]
    [InlineData("""{"required":["a","a"]}""", "/required")]
    [InlineData("""{"allOf":[]}""", "/allOf")]
    [InlineData("""{"multipleOf":0}""", "/multipleOf")]
    [InlineData("""{"properties":{"a/b":{"items":[true]}}}""", "/properties/a~1b/items")]
    [InlineData("""{"prefixItems":[true,{"not":1}]}""", "/prefixItems/1/not")]
    [InlineData("""{"$defs":{"a":{"type":"string","type":"number"}}}""", null)]
    public void Invalid_keyword_values_fail_with_their_location(string json, string? location)
    {
        var exception = Assert.ThrowsAny<JsonException>(() => JsonSchema.Parse(json));

        if (location is not null)
        {
            Assert.Equal(location, exception.Path);
        }
    }

    [Fact]
    public void Duplicate_keywords_in_an_element_fail()
    {
        using var document = JsonDocument.Parse("""{"properties":{"a":{"type":"string","type":"number"}}}""");

        var exception = Assert.Throws<JsonException>(() => JsonSchema.FromJsonElement(document.RootElement));

        Assert.Equal("/properties/a/type", exception.Path);
    }

    [Fact]
    public void Unknown_keywords_round_trip()
    {
        const string json = """{"x-ui":{"widget":"date","order":[3,1]},"type":"string"}""";

        var schema = JsonSchema.Parse(json);

        Assert.IsType<UnknownKeyword>(schema.Keywords[0]);
        Assert.Equal(json, schema.ToJsonString());
    }

    [Fact]
    public void Parsed_schemas_do_not_depend_on_the_source_document()
    {
        JsonSchema schema;
        using (var document = JsonDocument.Parse("""{"const":{"a":[1]},"x-other":"value"}"""))
        {
            schema = JsonSchema.FromJsonElement(document.RootElement);
        }

        Assert.Equal("""{"const":{"a":[1]},"x-other":"value"}""", schema.ToJsonString());
    }

    [Theory]
    [InlineData("", "root")]
    [InlineData("#", "root")]
    [InlineData("/properties/menus", "menus")]
    [InlineData("/properties/menus/prefixItems/1", "second")]
    [InlineData("#/properties/menus/prefixItems/0", "first")]
    [InlineData("/properties/a~1b~0c", "escaped")]
    [InlineData("#/properties/a~1b~0c", "escaped")]
    [InlineData("#/properties/with%20space", "space")]
    [InlineData("/$defs/item/not", "not")]
    [InlineData("/properties/menus/prefixItems/2", null)]
    [InlineData("/properties/menus/prefixItems/01", null)]
    [InlineData("/properties/menus/prefixItems/-", null)]
    [InlineData("/properties/missing", null)]
    [InlineData("/required/0", null)]
    [InlineData("/properties", null)]
    public void FindSubschema_follows_json_pointers(string pointer, string? expectedTitle)
    {
        JsonSchema schema = new JsonSchemaBuilder()
            .Title("root")
            .Properties(
                (
                    "menus",
                    new JsonSchemaBuilder().Title("menus").PrefixItems(new JsonSchemaBuilder().Title("first"), new JsonSchemaBuilder().Title("second"))
                ),
                ("a/b~c", new JsonSchemaBuilder().Title("escaped")),
                ("with space", new JsonSchemaBuilder().Title("space"))
            )
            .Defs(("item", new JsonSchemaBuilder().Not(new JsonSchemaBuilder().Title("not"))))
            .Required("menus");

        Assert.Equal(expectedTitle, schema.FindSubschema(pointer)?.GetKeyword<TitleKeyword>()?.Value);
    }

    [Theory]
    [InlineData("properties")]
    [InlineData("/properties/a~2")]
    public void FindSubschema_rejects_invalid_pointers(string pointer)
    {
        JsonSchema schema = new JsonSchemaBuilder().Properties(("a", true));

        Assert.Throws<FormatException>(() => schema.FindSubschema(pointer));
    }
}
