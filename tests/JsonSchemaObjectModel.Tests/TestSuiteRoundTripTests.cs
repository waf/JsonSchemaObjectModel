using System.Text.Json;
using JsonSchemaObjectModel.Keywords;

namespace JsonSchemaObjectModel.Tests;

/// <summary>
/// Every schema in the official test suite must parse, and must write back as the same JSON.
/// </summary>
public class TestSuiteRoundTripTests
{
    public static TheoryData<string, int> Cases => TestSuite.SchemaCases();

    [Theory]
    [MemberData(nameof(Cases))]
    public void Schema_round_trips(string file, int group)
    {
        var original = TestSuite.LoadSchema(file, group);

        var schema = JsonSchema.FromJsonElement(original);
        using var written = JsonDocument.Parse(schema.ToJsonString());

        Assert.True(
            JsonElement.DeepEquals(original, written.RootElement),
            $"{TestSuite.LoadDescription(file, group)}{Environment.NewLine}expected: {original}{Environment.NewLine}actual:   {written.RootElement}"
        );
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Standard_keywords_parse_as_typed_keywords(string file, int group)
    {
        var schema = JsonSchema.FromJsonElement(TestSuite.LoadSchema(file, group));
        var standardNames = JsonSchemaKeywordRegistry.CreateDraft202012().KeywordNames.ToHashSet(StringComparer.Ordinal);

        foreach (var keyword in AllKeywords(schema))
        {
            Assert.Equal(!standardNames.Contains(keyword.Name), keyword is UnknownKeyword);
        }
    }

    private static IEnumerable<IJsonSchemaKeyword> AllKeywords(JsonSchema schema)
    {
        foreach (var keyword in schema.Keywords)
        {
            yield return keyword;
            var children = keyword switch
            {
                ISingleSubschemaKeyword single => [single.Schema],
                ISubschemaArrayKeyword array => array.Schemas.AsEnumerable(),
                ISubschemaMapKeyword map => map.Schemas.Values,
                _ => [],
            };
            foreach (var descendant in children.SelectMany(AllKeywords))
            {
                yield return descendant;
            }
        }
    }
}
