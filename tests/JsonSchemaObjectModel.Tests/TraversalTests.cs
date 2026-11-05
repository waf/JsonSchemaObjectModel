using System.Text.Json;
using JsonSchemaObjectModel.Keywords;

namespace JsonSchemaObjectModel.Tests;

public class TraversalTests
{
    public static TheoryData<string, int> Cases => TestSuite.SchemaCases();

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_walk_visits_every_subschema_and_its_pointer_finds_the_same_instance(string file, int group)
    {
        var root = JsonSchema.FromJsonElement(TestSuite.LoadSchema(file, group));
        var visited = 0;

        root.Walk(
            (JsonSchema schema, in SchemaLocation location) =>
            {
                visited++;
                Assert.Same(schema, root.FindSubschema(location.ToPointer()));
                return WalkAction.Continue;
            }
        );

        Assert.Equal(CountSchemas(root), visited);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void A_rewrite_that_changes_nothing_returns_the_same_instance(string file, int group)
    {
        var root = JsonSchema.FromJsonElement(TestSuite.LoadSchema(file, group));
        var identity = new IdentityRewriter();

        Assert.Same(root, root.Rewrite(ref identity));
        Assert.Same(root, root.Rewrite((JsonSchema schema, in SchemaLocation _) => schema));
        Assert.Equal(CountSchemas(root), identity.Calls);
    }

    [Fact]
    public void A_rewrite_shares_the_parts_that_did_not_change()
    {
        var order = JsonSchema.Parse(
            """
            {"type":"object","$comment":"An order","properties":{
              "dish":{"type":"string","$comment":"From the menu"},
              "lines":{"type":"array","items":{"type":"object","properties":{"qty":{"type":"integer"}}}}}}
            """
        );

        var result = order.Rewrite((JsonSchema schema, in SchemaLocation _) => schema.BooleanValue is null ? schema.Without(CommentKeyword.KeywordName) : schema);

        Assert.Equal(
            JsonSchema.Parse(
                """{"type":"object","properties":{"dish":{"type":"string"},"lines":{"type":"array","items":{"type":"object","properties":{"qty":{"type":"integer"}}}}}}"""
            ),
            result
        );
        Assert.Same(order.Properties!["lines"], result.Properties!["lines"]);
        Assert.NotSame(order.Properties!["dish"], result.Properties!["dish"]);
        Assert.Same(order.Keywords[0], result.Keywords[0]);
    }

    [Fact]
    public void A_rewrite_can_prepare_a_restaurant_order_schema_for_strict_structured_output()
    {
        var order = JsonSchema.Parse(
            """
            {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{
              "customerEmail":{"type":"string","format":"email"},
              "table":{"type":"string","pattern":"^T[0-9]+$"},
              "lines":{"type":"array","items":{"type":"object","properties":{
                "dish":{"type":"string"},"quantity":{"type":"integer","minimum":1}},"required":["dish"]}}},
             "required":["lines"]}
            """
        );
        var strict = new StrictStructuredOutput();

        var result = order.Rewrite(ref strict);

        var expected = JsonSchema.Parse(
            """
            {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{
              "customerEmail":{"type":"string"},
              "table":{"type":"string"},
              "lines":{"type":"array","items":{"type":"object","properties":{
                "dish":{"type":"string"},"quantity":{"type":"integer","minimum":1}},"required":["dish","quantity"],"additionalProperties":false}}},
             "required":["customerEmail","table","lines"],"additionalProperties":false}
            """
        );
        Assert.Equal(expected, result);
        Assert.True(MetaSchemas.ValidateDraft202012(result.ToJsonString()).IsValid);
        Assert.Contains("\"format\":\"email\"", order.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_location_describes_the_keyword_and_the_name_or_index()
    {
        var order = JsonSchema.Parse("""{"properties":{"a/b~c":{"allOf":[true,{"items":false}]}}}""");
        var seen = new List<string>();

        order.Walk(
            (JsonSchema _, in SchemaLocation location) =>
            {
                seen.Add($"{location.Depth}|{location.KeywordName}|{location.PropertyName}|{location.Index}|{location.ToPointer()}");
                return WalkAction.Continue;
            }
        );

        Assert.Equal(
            [
                "0||||",
                "1|properties|a/b~c||/properties/a~1b~0c",
                "2|allOf||0|/properties/a~1b~0c/allOf/0",
                "2|allOf||1|/properties/a~1b~0c/allOf/1",
                "3|items|||/properties/a~1b~0c/allOf/1/items",
            ],
            seen
        );
    }

    [Fact]
    public void A_visitor_can_skip_children_and_stop_the_walk()
    {
        var order = JsonSchema.Parse("""{"properties":{"starter":{"items":true},"main":{"items":true},"dessert":true}}""");
        var skipped = new List<string>();
        var stopped = new List<string>();

        order.Walk(
            (JsonSchema _, in SchemaLocation location) =>
            {
                skipped.Add(location.ToPointer());
                return location.PropertyName == "starter" ? WalkAction.SkipChildren : WalkAction.Continue;
            }
        );
        order.Walk(
            (JsonSchema _, in SchemaLocation location) =>
            {
                stopped.Add(location.ToPointer());
                return location.PropertyName == "main" ? WalkAction.Stop : WalkAction.Continue;
            }
        );

        Assert.Equal(["", "/properties/starter", "/properties/main", "/properties/main/items", "/properties/dessert"], skipped);
        Assert.Equal(["", "/properties/starter", "/properties/starter/items", "/properties/main"], stopped);
    }

    [Fact]
    public void A_struct_visitor_keeps_its_result()
    {
        var order = JsonSchema.Parse("""{"properties":{"dish":{"type":"string"},"lines":{"items":{"type":"string"}}}}""");
        var counter = new TypeCounter();

        order.Walk(ref counter);

        Assert.Equal(2, counter.StringSchemas);
    }

    [Fact]
    public void The_rewrite_goes_into_custom_subschema_keywords()
    {
        var options = new JsonSchemaReaderOptions(JsonSchemaKeywordRegistry.CreateDraft202012().Add<GarnishKeyword>());
        var dish = JsonSchema.Parse("""{"x-garnish":{"type":"string","format":"uri"}}""", options);

        var result = dish.Rewrite((JsonSchema schema, in SchemaLocation _) => schema.BooleanValue is null ? schema.Without(FormatKeyword.KeywordName) : schema);

        Assert.IsType<GarnishKeyword>(result.Keywords[0]);
        Assert.Equal("""{"x-garnish":{"type":"string"}}""", result.ToJsonString());
    }

    [Fact]
    public void A_rewriter_must_not_return_null()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => JsonSchema.Parse("""{"items":{"type":"string"}}""").Rewrite((JsonSchema schema, in SchemaLocation location) => location.IsRoot ? schema : null!)
        );

        Assert.Contains("/items", exception.Message, StringComparison.Ordinal);
    }

    private static int CountSchemas(JsonSchema schema)
    {
        var count = 1;
        foreach (var keyword in schema.Keywords)
        {
            count += keyword switch
            {
                ISingleSubschemaKeyword single => CountSchemas(single.Schema),
                ISubschemaArrayKeyword array => array.Schemas.Sum(CountSchemas),
                ISubschemaMapKeyword map => map.Schemas.Values.Sum(CountSchemas),
                _ => 0,
            };
        }

        return count;
    }

    private struct IdentityRewriter : ISchemaRewriter
    {
        public int Calls { get; private set; }

        public JsonSchema Rewrite(JsonSchema schema, in SchemaLocation location)
        {
            Calls++;
            return schema;
        }
    }

    private struct TypeCounter : ISchemaVisitor
    {
        public int StringSchemas { get; private set; }

        public WalkAction Visit(JsonSchema schema, in SchemaLocation location)
        {
            if (schema.Type == SchemaValueType.String)
            {
                StringSchemas++;
            }

            return WalkAction.Continue;
        }
    }

    /// <summary>
    /// The shape that strict structured-output APIs for LLMs ask for: each object lists all its properties as required
    /// and allows no others, and there are no format or pattern assertions.
    /// </summary>
    private readonly struct StrictStructuredOutput : ISchemaRewriter
    {
        public JsonSchema Rewrite(JsonSchema schema, in SchemaLocation location)
        {
            if (schema.BooleanValue is not null)
            {
                return schema;
            }

            schema = schema.Without(FormatKeyword.KeywordName).Without(PatternKeyword.KeywordName);
            if (schema.Properties is { } properties)
            {
                schema = schema.With(new RequiredKeyword(properties.Keys)).With(AdditionalPropertiesKeyword.For(false));
            }

            return schema;
        }
    }

    private sealed class GarnishKeyword(JsonSchema schema) : SingleSubschemaKeyword(schema), IJsonSchemaKeyword<GarnishKeyword>
    {
        public static string KeywordName => "x-garnish";

        public override string Name => KeywordName;

        public static GarnishKeyword Read(JsonElement value, JsonSchemaReadContext context) => new(context.ReadSchema(value));

        public override GarnishKeyword WithSchema(JsonSchema schema) => ReferenceEquals(schema, Schema) ? this : new(schema);
    }
}
