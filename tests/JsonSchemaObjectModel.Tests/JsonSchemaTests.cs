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
            .SchemaDialect(SchemaKeyword.Draft202012)
            .Type(JsonSchemaType.Object)
            .Properties(
                ("id", new JsonSchemaBuilder().Const(Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"))),
                ("size", new JsonSchemaBuilder().Type(JsonSchemaType.String).Pattern("^[SML]$")),
                ("email", new JsonSchemaBuilder().Type(JsonSchemaType.String).Format(JsonSchemaFormats.Email)),
                (
                    "sides",
                    new JsonSchemaBuilder()
                        .Type(JsonSchemaType.Array)
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
        var schema = new JsonSchemaBuilder().Type(JsonSchemaType.String).MinLength(1).Type(JsonSchemaType.Null).Build();

        Assert.Equal("""{"type":"null","minLength":1}""", schema.ToJsonString());
    }

    [Fact]
    public void Builder_Add_rejects_a_duplicate_and_Set_replaces_it()
    {
        var builder = new JsonSchemaBuilder().Title("Lunch").Add(new UntypedKeyword("x-menu", JsonElements.From("lunch")));

        Assert.Throws<ArgumentException>(() => builder.Add(new TitleKeyword("Dinner")));
        builder.Set(new TitleKeyword("Dinner")).Set(new UntypedKeyword("x-menu", JsonElements.From("dinner")));

        Assert.Equal("""{"title":"Dinner","x-menu":"dinner"}""", builder.Build().ToJsonString());
    }

    [Fact]
    public void Type_shortcuts_start_a_builder_with_the_type()
    {
        (JsonSchemaBuilder Shortcut, JsonSchemaType Type)[] cases =
        [
            (JsonSchemaBuilder.String(), JsonSchemaType.String),
            (JsonSchemaBuilder.Number(), JsonSchemaType.Number),
            (JsonSchemaBuilder.Integer(), JsonSchemaType.Integer),
            (JsonSchemaBuilder.Boolean(), JsonSchemaType.Boolean),
            (JsonSchemaBuilder.Object(), JsonSchemaType.Object),
            (JsonSchemaBuilder.Array(), JsonSchemaType.Array),
            (JsonSchemaBuilder.Null(), JsonSchemaType.Null),
        ];

        foreach (var (shortcut, type) in cases)
        {
            JsonSchema schema = shortcut.MinLength(1);
            Assert.Equal(new JsonSchemaBuilder().Type(type).MinLength(1).Build().ToJsonString(), schema.ToJsonString());
            Assert.Same(TypeKeyword.For(type), schema.GetKeyword<TypeKeyword>());
        }

        Assert.NotSame(JsonSchemaBuilder.String(), JsonSchemaBuilder.String());
    }

    [Fact]
    public void Default_and_Examples_accept_primitive_values()
    {
        var guid = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
        (JsonSchemaBuilder Primitive, JsonElement Element)[] cases =
        [
            (new JsonSchemaBuilder().Default("soup"), JsonElements.From("soup")),
            (new JsonSchemaBuilder().Default((string?)null), JsonElements.Null),
            (new JsonSchemaBuilder().Default(2), JsonElements.From(2)),
            (new JsonSchemaBuilder().Default(12.50m), JsonElements.From(12.50m)),
            (new JsonSchemaBuilder().Default(0.5), JsonElements.From(0.5)),
            (new JsonSchemaBuilder().Default(true), JsonElements.From(true)),
            (new JsonSchemaBuilder().Default(guid), JsonElements.From(guid)),
        ];

        foreach (var (primitive, element) in cases)
        {
            JsonSchema expected = new JsonSchemaBuilder().Default(element);
            JsonSchema actual = primitive;
            Assert.Equal(expected.ToJsonString(), actual.ToJsonString());
            Assert.Equal(expected, actual);
            Assert.Equal(expected.GetHashCode(), actual.GetHashCode());
            Assert.True(JsonElement.DeepEquals(element, actual.GetKeyword<DefaultKeyword>()!.Value));
        }

        JsonSchema examples = new JsonSchemaBuilder().Examples("Margherita", "Marinara");
        Assert.Equal("""{"examples":["Margherita","Marinara"]}""", examples.ToJsonString());
        Assert.Equal(new JsonSchemaBuilder().Examples(JsonElements.From("Margherita"), JsonElements.From("Marinara")).Build(), examples);
        Assert.Throws<ArgumentNullException>(() => new JsonSchemaBuilder().Examples("Margherita", null!));
    }

    [Fact]
    public void Every_builder_method_writes_a_valid_draft_2020_12_schema_that_round_trips()
    {
        JsonSchema schema = new JsonSchemaBuilder()
            .SchemaDialect(SchemaKeyword.Draft202012)
            .Id("https://example.com/everything")
            .Anchor("root")
            .DynamicAnchor("meta")
            .Comment("Uses every builder method.")
            .Vocabulary(("https://json-schema.org/draft/2020-12/vocab/core", true))
            .Defs(("positive", new JsonSchemaBuilder().Type(JsonSchemaType.Number | JsonSchemaType.Integer).ExclusiveMinimum(0)))
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
            .Enum(JsonElements.From(1), JsonElements.From("one"), JsonElements.Null)
            .DependentRequired(("a", ["b", "c"]))
            .Title("Everything")
            .Description("A schema that uses every builder method.")
            .Default(JsonElements.From(true))
            .Deprecated()
            .ReadOnly()
            .WriteOnly(false)
            .Examples(JsonElements.From(1.25), JsonElements.From(false))
            .Format(JsonSchemaFormats.DateTime)
            .ContentEncoding("base64")
            .ContentMediaType("application/json")
            .ContentSchema(new JsonSchemaBuilder().Type(JsonSchemaType.Object));

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
            JsonSchemaType.String | JsonSchemaType.Null,
            JsonSchema.Parse("""{"type":["null","string"]}""").Type
        );
        Assert.Equal(
            """{"type":["object","null"]}""",
            new JsonSchemaBuilder().Type(JsonSchemaType.Null | JsonSchemaType.Object).Build().ToJsonString()
        );
        Assert.Equal(
            """{"type":["null","object"]}""",
            new JsonSchemaBuilder().Type(JsonSchemaType.Null, JsonSchemaType.Object).Build().ToJsonString()
        );
        Assert.Equal("""{"type":["string"]}""", new JsonSchemaBuilder().Type([JsonSchemaType.String]).Build().ToJsonString());

        var parsed = JsonSchema.Parse("""{"type":["null","string"]}""").GetKeyword<TypeKeyword>()!;
        Assert.True(parsed.IsArrayForm);
        Assert.Equal([JsonSchemaType.Null, JsonSchemaType.String], parsed.Types);
        Assert.False(JsonSchema.Parse("""{"type":"string"}""").GetKeyword<TypeKeyword>()!.IsArrayForm);
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
    [InlineData("""{"maxLength":2.0}""", 2UL)]
    [InlineData("""{"maxLength":1e2}""", 100UL)]
    [InlineData("""{"maxLength":1.5e1}""", 15UL)]
    [InlineData("""{"maxLength":100e-2}""", 1UL)]
    [InlineData("""{"maxLength":-0}""", 0UL)]
    [InlineData("""{"maxLength":0e999999999999999999999}""", 0UL)]
    [InlineData("""{"maxLength":18446744073709551615}""", ulong.MaxValue)]
    [InlineData("""{"maxLength":1.8446744073709551615e19}""", ulong.MaxValue)]
    public void Non_negative_integers_are_read_from_any_integer_form(string json, ulong expected)
    {
        Assert.Equal(expected, JsonSchema.Parse(json).GetKeyword<MaxLengthKeyword>()!.Value);
    }

    [Theory]
    [InlineData("""{"multipleOf":1e-400}""")]
    [InlineData("""{"multipleOf":0.000000000000000000000000000000000000001}""")]
    [InlineData("""{"multipleOf":1e400}""")]
    public void MultipleOf_accepts_positive_values_beyond_double_range(string json)
    {
        Assert.Equal(json, JsonSchema.Parse(json).ToJsonString());
    }

    [Theory]
    [InlineData("""{"minItems":-1}""", "/minItems")]
    [InlineData("""{"minItems":1.5}""", "/minItems")]
    [InlineData("""{"type":"text"}""", "/type")]
    [InlineData("""{"type":["string","string"]}""", "/type")]
    [InlineData("""{"type":[]}""", "/type")]
    [InlineData("""{"required":["a","a"]}""", "/required")]
    [InlineData("""{"allOf":[]}""", "/allOf")]
    [InlineData("""{"multipleOf":0}""", "/multipleOf")]
    [InlineData("""{"multipleOf":-0.0}""", "/multipleOf")]
    [InlineData("""{"multipleOf":-1e-400}""", "/multipleOf")]
    [InlineData("""{"maxLength":-0.5}""", "/maxLength")]
    [InlineData("""{"maxLength":18446744073709551616}""", "/maxLength")]
    [InlineData("""{"maxLength":1e400}""", "/maxLength")]
    [InlineData("""{"properties":{"a/b":{"items":[true]}}}""", "/properties/a~1b/items")]
    [InlineData("""{"prefixItems":[true,{"not":1}]}""", "/prefixItems/1/not")]
    [InlineData("""{"$defs":{"a":{"type":"string","type":"number"}}}""", null)]
    [InlineData("""{"properties":{"a":true,"a":false}}""", "/properties/a")]
    [InlineData("""{"$vocabulary":{"https://example.com/menu":true,"https://example.com/menu":false}}""", "/$vocabulary/https:~1~1example.com~1menu")]
    [InlineData("""{"$vocabulary":{"a":1}}""", "/$vocabulary/a")]
    [InlineData("""{"dependentRequired":{"dessert":["main"],"dessert":["drink"]}}""", "/dependentRequired/dessert")]
    [InlineData("""{"dependentRequired":{"dessert":["main","main"]}}""", "/dependentRequired/dessert")]
    [InlineData("""{"dependentRequired":{"dessert":"main"}}""", "/dependentRequired/dessert")]
    [InlineData("""{"dependentRequired":["dessert"]}""", "/dependentRequired")]
    [InlineData("""{"uniqueItems":"yes"}""", "/uniqueItems")]
    [InlineData("""{"minItems":"1"}""", "/minItems")]
    [InlineData("""{"allOf":{"type":"string"}}""", "/allOf")]
    [InlineData("""{"properties":[true]}""", "/properties")]
    [InlineData("""{"required":["table",1]}""", "/required")]
    [InlineData("""{"enum":{"S":1}}""", "/enum")]
    [InlineData("""{"type":1}""", "/type")]
    [InlineData("""{"type":["string",1]}""", "/type")]
    [InlineData("""{"type":["string","text"]}""", "/type")]
    [InlineData("""{"format":1}""", "/format")]
    [InlineData("""{"$vocabulary":["https://example.com/menu"]}""", "/$vocabulary")]
    public void Invalid_keyword_values_fail_with_their_location(string json, string? location)
    {
        //  Exactly JsonException, as Parse documents. An ArgumentException from a keyword constructor must not escape.
        var exception = Assert.Throws<JsonException>(() => JsonSchema.Parse(json));

        if (location is not null)
        {
            Assert.Equal(location, exception.Path);
        }
    }

    [Theory]
    [InlineData("""{"type":"string"} {"type":"number"}""")]
    [InlineData("""{"type":"string"}}""")]
    [InlineData("""{"type":"string"},""")]
    public void Text_after_the_schema_fails(string json)
    {
        Assert.ThrowsAny<JsonException>(() => JsonSchema.Parse(json));
        Assert.ThrowsAny<JsonException>(() => JsonSchema.Parse(System.Text.Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Whitespace_around_the_schema_is_accepted()
    {
        const string json = " \n{\"type\":\"string\"}\t ";

        Assert.Equal("""{"type":"string"}""", JsonSchema.Parse(json).ToJsonString());
        Assert.Equal("""{"type":"string"}""", JsonSchema.Parse(System.Text.Encoding.UTF8.GetBytes(json)).ToJsonString());
    }

    [Fact]
    public void A_type_keyword_needs_at_least_one_type()
    {
        Assert.Throws<ArgumentException>(() => new TypeKeyword(Array.Empty<JsonSchemaType>()));
    }

    [Fact]
    public void Keyword_constructors_reject_invalid_arguments()
    {
        Assert.Throws<ArgumentException>(() => new TypeKeyword(JsonSchemaType.None));
        Assert.Throws<ArgumentException>(() => new TypeKeyword(JsonSchemaType.String | (JsonSchemaType)(1 << 20)));
        Assert.Throws<ArgumentException>(() => new TypeKeyword([JsonSchemaType.String | JsonSchemaType.Null]));
        Assert.Throws<ArgumentException>(() => new TypeKeyword([JsonSchemaType.String, JsonSchemaType.String]));
        Assert.Throws<ArgumentOutOfRangeException>(() => TypeKeyword.GetTypeName(JsonSchemaType.String | JsonSchemaType.Null));
        Assert.Throws<ArgumentException>(() => new AllOfKeyword(new List<JsonSchema>()));
        Assert.Throws<ArgumentException>(() => new RequiredKeyword(["table", "table"]));
        Assert.Throws<ArgumentNullException>(() => new RequiredKeyword(["table", null!]));
        Assert.Throws<ArgumentException>(
            () => new VocabularyKeyword([KeyValuePair.Create("https://example.com/menu", true), KeyValuePair.Create("https://example.com/menu", false)])
        );
        Assert.Throws<ArgumentException>(
            () =>
                new DependentRequiredKeyword(
                    [KeyValuePair.Create<string, IEnumerable<string>>("dessert", ["main"]), KeyValuePair.Create<string, IEnumerable<string>>("dessert", ["drink"])]
                )
        );
        Assert.Throws<ArgumentException>(
            () => new DependentRequiredKeyword([KeyValuePair.Create<string, IEnumerable<string>>("dessert", ["main", "main"])])
        );
    }

    [Fact]
    public void Type_names_convert_both_ways()
    {
        foreach (var type in Enum.GetValues<JsonSchemaType>().Where(type => type != JsonSchemaType.None && (type & (type - 1)) == 0))
        {
            var name = TypeKeyword.GetTypeName(type);

            Assert.True(TypeKeyword.TryParseTypeName(name, out var parsed));
            Assert.Equal(type, parsed);
            using var document = JsonDocument.Parse($"\"{name}\"");
            Assert.True(TypeKeyword.TryParseTypeName(document.RootElement, out parsed));
            Assert.Equal(type, parsed);
        }

        Assert.False(TypeKeyword.TryParseTypeName("text", out var unknown));
        Assert.Equal(JsonSchemaType.None, unknown);
        Assert.False(TypeKeyword.TryParseTypeName((string?)null, out _));
    }

    [Fact]
    public void Collection_overloads_of_the_builder_write_the_same_JSON_as_the_params_overloads()
    {
        JsonSchema bowl = new JsonSchemaBuilder().MinLength(1);
        var guid = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");

        JsonSchema fromParams = new JsonSchemaBuilder()
            .Defs(("bowl", bowl))
            .AllOf(bowl, true)
            .AnyOf(bowl, false)
            .OneOf(bowl)
            .DependentSchemas(("noodles", bowl))
            .PrefixItems(bowl, true)
            .Properties(("soup", bowl))
            .PatternProperties(("^side-", bowl))
            .Enum(JsonElements.From(12.50m), JsonElements.From(guid))
            .Required("soup", "noodles")
            .Type(JsonSchemaType.Object, JsonSchemaType.Null)
            .Vocabulary(("https://example.com/vocab/menu", false))
            .DependentRequired(("noodles", ["soup"]))
            .Examples(JsonElements.From(1), JsonElements.From("one"))
            .Examples("one", "two");

        JsonSchema fromCollections = new JsonSchemaBuilder()
            .Defs(new List<(string, JsonSchema)> { ("bowl", bowl) })
            .AllOf(new List<JsonSchema> { bowl, true })
            .AnyOf(new List<JsonSchema> { bowl, false })
            .OneOf(new List<JsonSchema> { bowl })
            .DependentSchemas(new List<(string, JsonSchema)> { ("noodles", bowl) })
            .PrefixItems(new List<JsonSchema> { bowl, true })
            .Properties(new List<(string, JsonSchema)> { ("soup", bowl) })
            .PatternProperties(new List<(string, JsonSchema)> { ("^side-", bowl) })
            .Enum(new List<JsonElement> { JsonElements.From(12.50m), JsonElements.From(guid) })
            .Required(new List<string> { "soup", "noodles" })
            .Type(new List<JsonSchemaType> { JsonSchemaType.Object, JsonSchemaType.Null })
            .Vocabulary(new List<(string, bool)> { ("https://example.com/vocab/menu", false) })
            .DependentRequired(new List<(string, IEnumerable<string>)> { ("noodles", ["soup"]) })
            .Examples(new List<JsonElement> { JsonElements.From(1), JsonElements.From("one") })
            .Examples(new List<string> { "one", "two" });

        JsonSchema fromPairs = new JsonSchemaBuilder()
            .Vocabulary(new Dictionary<string, bool> { ["https://example.com/vocab/menu"] = false })
            .Defs(new Dictionary<string, JsonSchema> { ["bowl"] = bowl })
            .DependentSchemas(new Dictionary<string, JsonSchema> { ["noodles"] = bowl })
            .Properties(new Dictionary<string, JsonSchema> { ["soup"] = bowl })
            .PatternProperties(new Dictionary<string, JsonSchema> { ["^side-"] = bowl })
            .DependentRequired(new Dictionary<string, IEnumerable<string>> { ["noodles"] = ["soup"] });

        Assert.Equal(fromParams.ToJsonString(), fromCollections.ToJsonString());
        Assert.Equal(
            """{"$vocabulary":{"https://example.com/vocab/menu":false},"$defs":{"bowl":{"minLength":1}},"dependentSchemas":{"noodles":{"minLength":1}},"properties":{"soup":{"minLength":1}},"patternProperties":{"^side-":{"minLength":1}},"dependentRequired":{"noodles":["soup"]}}""",
            fromPairs.ToJsonString()
        );
        Assert.Equal(
            """{"enum":["S","M","L"]}""",
            new JsonSchemaBuilder().Enum(new List<string> { "S", "M", "L" }).Build().ToJsonString()
        );
        Assert.Equal("""{"enum":["S","M"]}""", new JsonSchemaBuilder().Enum((IEnumerable<string>)["S", "M"]).Build().ToJsonString());
    }

    [Fact]
    public void Builder_removes_a_keyword_by_name()
    {
        var builder = new JsonSchemaBuilder().Type(JsonSchemaType.String).MinLength(1).Format("menu-code");

        builder.Remove(TypeKeyword.KeywordName).Remove("missing");

        Assert.Equal("""{"minLength":1,"format":"menu-code"}""", builder.Build().ToJsonString());
        Assert.Equal("menu-code", builder.Build().Format);
    }

    [Fact]
    public void Escaped_and_long_keyword_names_find_their_reader()
    {
        var longName = "x-" + new string('a', 100);
        var registry = JsonSchemaKeywordRegistry.CreateDraft202012().Add(longName, (value, context) => new UntypedKeyword(longName, value));

        var schema = JsonSchema.Parse($$"""{"type":"string","{{longName}}":1,"x-{{new string('b', 100)}}":2}""", new JsonSchemaReadOptions(registry));

        Assert.IsType<TypeKeyword>(schema.Keywords[0]);
        Assert.Equal("type", schema.Keywords[0].Name);
        Assert.Same(longName, schema.Keywords[1].Name);
        Assert.IsType<UntypedKeyword>(schema.Keywords[2]);
        Assert.Equal(JsonSchema.Parse($$"""{"type":"string","{{longName}}":1,"x-{{new string('b', 100)}}":2}"""), schema);
    }

    [Fact]
    public void A_reader_that_returns_a_keyword_with_another_name_fails()
    {
        var registry = JsonSchemaKeywordRegistry.CreateDraft202012().Add("x-dish", (_, _) => new TitleKeyword("Pad thai"));

        Assert.Throws<InvalidOperationException>(() => JsonSchema.Parse("""{"x-dish":1}""", new JsonSchemaReadOptions(registry)));
    }

    [Fact]
    public void Accessors_return_the_typed_keyword_values()
    {
        var order = JsonSchema.Parse(
            """{"$defs":{"line":{"type":"object"}},"prefixItems":[{"$ref":"#/$defs/line"}],"items":false,"required":["table"]}"""
        );

        Assert.Equal(["line"], order.Defs!.Keys);
        Assert.Equal("""{"$ref":"#/$defs/line"}""", Assert.Single(order.PrefixItems!.Value).ToJsonString());
        Assert.Same(JsonSchema.False, order.Items);
        Assert.Equal(["table"], order.Required!.Value);
        Assert.True(order.TryGetKeyword<ItemsKeyword>(out var items));
        Assert.Same(JsonSchema.False, items.Schema);
        Assert.False(order.TryGetKeyword<ContainsKeyword>(out var contains));
        Assert.Null(contains);

        Assert.Null(JsonSchema.Empty.Defs);
        Assert.Null(JsonSchema.Empty.PrefixItems);
        Assert.Null(JsonSchema.Empty.Items);
        Assert.Null(JsonSchema.Empty.Required);
    }

    [Fact]
    public void A_schema_parsed_without_a_standard_reader_can_be_copied_with_the_builder()
    {
        var registry = JsonSchemaKeywordRegistry.CreateDraft202012();
        registry.Remove(FormatKeyword.KeywordName);
        var parsed = JsonSchema.Parse("""{"type":"string","format":"email"}""", new JsonSchemaReadOptions(registry));

        var builder = new JsonSchemaBuilder();
        foreach (var keyword in parsed.Keywords)
        {
            builder.Add(keyword);
        }

        var copy = builder.Build();
        Assert.IsType<UntypedKeyword>(copy.Keywords[1]);
        Assert.Null(copy.Format);
        Assert.Equal("""{"type":"string","format":"email"}""", copy.ToJsonString());
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

        Assert.IsType<UntypedKeyword>(schema.Keywords[0]);
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
