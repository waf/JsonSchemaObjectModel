using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonSchemaObjectModel.Keywords;

namespace JsonSchemaObjectModel.Tests;

/// <summary>
/// A custom vocabulary with typed keywords, declared by a meta-schema with <c>$vocabulary</c>.
/// </summary>
public class CustomVocabularyTests
{
    private const string Dialect = "https://example.com/schema/restaurant";

    private static readonly string directory = Path.Combine(AppContext.BaseDirectory, "TestData", "CustomVocabulary");
    private static readonly string dialectJson = File.ReadAllText(Path.Combine(directory, "dialect.json"));
    private static readonly string vocabularyJson = File.ReadAllText(Path.Combine(directory, "vocabulary.json"));

    private static readonly JsonSchemaReaderOptions readerOptions = new(
        JsonSchemaKeywordRegistry
            .CreateDraft202012()
            .Add(PlaceholderKeyword.KeywordName, PlaceholderKeyword.Read)
            .Add(MinDateKeyword.KeywordName, MinDateKeyword.Read)
    );

    private static JsonSchema OrderSchema() =>
        new JsonSchemaBuilder()
            .Schema(Dialect)
            .Type(SchemaValueType.Object)
            .Properties(
                ("customerName", new JsonSchemaBuilder().Type(SchemaValueType.String).Add(new PlaceholderKeyword("e.g. Alex Smith"))),
                (
                    "pickupDate",
                    new JsonSchemaBuilder()
                        .Type(SchemaValueType.String)
                        .Format(Formats.Date)
                        .Add(new MinDateKeyword(new DateOnly(2026, 12, 1), "Pickup must be on or after the opening day."))
                )
            )
            .Required("customerName");

    [Fact]
    public void Custom_keywords_round_trip_through_System_Text_Json_as_typed_keywords()
    {
        var serializerOptions = new JsonSerializerOptions { Converters = { new JsonSchemaConverter(readerOptions) } };
        var response = new OrderResponse(OrderSchema());

        var json = JsonSerializer.Serialize(response, serializerOptions);
        var read = JsonSerializer.Deserialize<OrderResponse>(json, serializerOptions)!;

        var pickupDate = read.Schema.FindSubschema("/properties/pickupDate")!.GetKeyword<MinDateKeyword>()!;
        Assert.Equal(new DateOnly(2026, 12, 1), pickupDate.Date);
        Assert.Equal("Pickup must be on or after the opening day.", pickupDate.Message);
        Assert.Equal("e.g. Alex Smith", read.Schema.Properties!["customerName"].GetKeyword<PlaceholderKeyword>()!.Placeholder);
        Assert.Equal(json, JsonSerializer.Serialize(read, serializerOptions));
    }

    [Fact]
    public void Without_a_reader_custom_keywords_are_kept_as_unknown_keywords()
    {
        var json = OrderSchema().ToJsonString();

        var schema = JsonSchema.Parse(json);

        Assert.IsType<UnknownKeyword>(schema.FindSubschema("/properties/customerName")!.GetKeyword(PlaceholderKeyword.KeywordName));
        Assert.Equal(json, schema.ToJsonString());
    }

    [Fact]
    public void The_vocabulary_meta_schemas_are_valid_draft_2020_12_schemas()
    {
        foreach (var metaSchema in new[] { dialectJson, vocabularyJson })
        {
            var result = MetaSchemas.ValidateDraft202012(metaSchema);
            Assert.True(result.IsValid, MetaSchemas.Describe(result));
            Assert.Equal(
                JsonSchema.Parse(metaSchema).ToJsonString(),
                JsonSchema.Parse(JsonSchema.Parse(metaSchema).ToJsonString()).ToJsonString()
            );
        }
    }

    [Fact]
    public void An_independent_validator_accepts_the_schema_against_the_custom_dialect()
    {
        var result = MetaSchemas.Validate(OrderSchema().ToJsonString(), dialectJson, vocabularyJson);

        Assert.True(result.IsValid, MetaSchemas.Describe(result));
    }

    [Fact]
    public void An_independent_validator_rejects_a_malformed_custom_keyword()
    {
        var json = OrderSchema().ToJsonString().Replace("\"date\":\"2026-12-01\",", "", StringComparison.Ordinal);

        var result = MetaSchemas.Validate(json, dialectJson, vocabularyJson);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationErrors, error => error.InstanceLocation.ToString() == "/properties/pickupDate/minDate");
    }

    [Fact]
    public void The_reader_rejects_a_malformed_custom_keyword_with_its_location()
    {
        var exception = Assert.Throws<JsonException>(() =>
            JsonSchema.Parse("""{"properties":{"pickupDate":{"minDate":{"date":"1 Dec"}}}}""", readerOptions)
        );

        Assert.Equal("/properties/pickupDate/minDate", exception.Path);
    }

    private sealed record OrderResponse([property: JsonPropertyName("$schema")] JsonSchema Schema);

    private sealed class PlaceholderKeyword(string placeholder) : IJsonSchemaKeyword
    {
        public const string KeywordName = "placeholder";

        public string Name => KeywordName;

        public string Placeholder { get; } = placeholder;

        public static PlaceholderKeyword Read(JsonElement value, JsonSchemaReadContext context) => new(context.ReadString(value));

        public void WriteValue(Utf8JsonWriter writer) => writer.WriteStringValue(Placeholder);
    }

    private sealed class MinDateKeyword(DateOnly date, string? message) : IJsonSchemaKeyword
    {
        public const string KeywordName = "minDate";

        public string Name => KeywordName;

        public DateOnly Date { get; } = date;

        public string? Message { get; } = message;

        public static MinDateKeyword Read(JsonElement value, JsonSchemaReadContext context)
        {
            if (
                value.ValueKind != JsonValueKind.Object
                || !value.TryGetProperty("date", out var date)
                || date.ValueKind != JsonValueKind.String
                || !DateOnly.TryParseExact(date.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            )
            {
                throw context.Error("The value must be an object with a 'date' in the format yyyy-MM-dd.");
            }

            return new(parsed, value.TryGetProperty("message", out var message) ? context.ReadString(message) : null);
        }

        public void WriteValue(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteString("date", Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (Message is not null)
            {
                writer.WriteString("message", Message);
            }

            writer.WriteEndObject();
        }
    }
}
