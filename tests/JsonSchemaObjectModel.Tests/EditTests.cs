using System.Collections.Immutable;
using JsonSchemaObjectModel.Keywords;

namespace JsonSchemaObjectModel.Tests;

/// <summary>
/// Immutable edits: a keyword with new subschemas, and a schema with a keyword added or removed.
/// </summary>
public class EditTests
{
    [Fact]
    public void Every_subschema_keyword_in_the_Test_Suite_rebuilds_itself_and_returns_itself_when_nothing_changes()
    {
        var replacement = new JsonSchemaBuilder().Title("replacement").Build();
        var covered = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (file, group) in TestSuite.SchemaCases().Select(row => row.Data))
        {
            foreach (var keyword in AllKeywords(JsonSchema.FromJsonElement(TestSuite.LoadSchema(file, group))))
            {
                switch (keyword)
                {
                    case ISingleSubschemaKeyword single:
                        Assert.Same(single, single.WithSchema(single.Schema));
                        var changedSingle = single.WithSchema(replacement);
                        Assert.Equal(single.GetType(), changedSingle.GetType());
                        Assert.Same(replacement, changedSingle.Schema);
                        break;
                    case ISubschemaArrayKeyword array:
                        Assert.Same(array, array.WithSchemas(array.Schemas));
                        var changedArray = array.WithSchemas([replacement, .. array.Schemas]);
                        Assert.Equal(array.GetType(), changedArray.GetType());
                        Assert.Equal(array.Schemas.Length + 1, changedArray.Schemas.Length);
                        Assert.Same(replacement, changedArray.Schemas[0]);
                        break;
                    case ISubschemaMapKeyword map:
                        Assert.Same(map, map.WithSchemas(map.Schemas));
                        var changedMap = map.WithSchemas(SchemaMap.Create(("dessert", replacement)));
                        Assert.Equal(map.GetType(), changedMap.GetType());
                        Assert.Same(replacement, changedMap.Schemas["dessert"]);
                        break;
                    default:
                        continue;
                }

                covered.Add(keyword.Name);
            }
        }

        string[] expected =
        [
            "$defs", "additionalProperties", "allOf", "anyOf", "contains", "contentSchema", "dependentSchemas", "else", "if",
            "items", "not", "oneOf", "patternProperties", "prefixItems", "properties", "propertyNames", "then",
            "unevaluatedItems", "unevaluatedProperties",
        ];
        Assert.Equal(expected, covered.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void WithSchema_keeps_sharing_the_instances_for_true_and_false()
    {
        var items = ItemsKeyword.For(new JsonSchemaBuilder().MinLength(1));

        Assert.Same(ItemsKeyword.For(false), items.WithSchema(false));
    }

    [Fact]
    public void With_replaces_a_keyword_in_place_and_adds_a_new_keyword_at_the_end()
    {
        var dish = JsonSchema.Parse("""{"type":"string","minLength":1,"title":"Dish"}""");

        Assert.Equal("""{"type":"string","minLength":3,"title":"Dish"}""", dish.With(MinLengthKeyword.For(3)).ToJsonString());
        Assert.Equal("""{"type":"string","minLength":1,"title":"Dish","maxLength":40}""", dish.With(MaxLengthKeyword.For(40)).ToJsonString());
        Assert.Equal("""{"type":"string","minLength":1,"title":"Dish"}""", dish.ToJsonString());
    }

    [Fact]
    public void With_returns_the_same_schema_when_it_already_has_the_keyword_instance()
    {
        var dish = JsonSchema.Parse("""{"type":"string"}""");

        Assert.Same(dish, dish.With(dish.Keywords[0]));
    }

    [Fact]
    public void Without_removes_a_keyword_and_returns_the_same_schema_when_the_keyword_is_absent()
    {
        var dish = JsonSchema.Parse("""{"type":"string","format":"email"}""");

        Assert.Equal("""{"type":"string"}""", dish.Without(FormatKeyword.KeywordName).ToJsonString());
        Assert.Same(dish, dish.Without(PatternKeyword.KeywordName));
        Assert.Same(JsonSchema.Empty, JsonSchema.Parse("""{"format":"email"}""").Without(FormatKeyword.KeywordName));
    }

    [Fact]
    public void Boolean_schemas_cannot_be_edited()
    {
        Assert.Throws<InvalidOperationException>(() => JsonSchema.True.With(MinLengthKeyword.For(1)));
        Assert.Throws<InvalidOperationException>(() => JsonSchema.False.Without(TypeKeyword.KeywordName));
        Assert.Equal("""{"minLength":1}""", JsonSchema.Empty.With(MinLengthKeyword.For(1)).ToJsonString());
    }

    private static IEnumerable<IJsonSchemaKeyword> AllKeywords(JsonSchema schema)
    {
        foreach (var keyword in schema.Keywords)
        {
            yield return keyword;
            IEnumerable<JsonSchema> children = keyword switch
            {
                ISingleSubschemaKeyword single => [single.Schema],
                ISubschemaArrayKeyword array => array.Schemas.AsEnumerable(),
                ISubschemaMapKeyword map => map.Schemas.Values,
                _ => [],
            };
            foreach (var child in children)
            {
                foreach (var nested in AllKeywords(child))
                {
                    yield return nested;
                }
            }
        }
    }
}
