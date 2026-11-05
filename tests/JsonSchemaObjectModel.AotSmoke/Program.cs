//  Publish this app with Native AOT and run it. It exits with 0 when the library works without reflection.
//  dotnet publish tests/JsonSchemaObjectModel.AotSmoke -c Release -o artifacts/aot
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonSchemaObjectModel;
using JsonSchemaObjectModel.Keywords;

var readerOptions = new JsonSchemaReaderOptions(
    JsonSchemaKeywordRegistry
        .CreateDraft202012()
        .Add<PlaceholderKeyword>()
);

JsonSchema schema = new JsonSchemaBuilder()
    .Schema(SchemaKeyword.Draft202012)
    .Type(SchemaValueType.Object)
    .Properties(
        ("id", new JsonSchemaBuilder().Const(Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"))),
        ("customerName", new JsonSchemaBuilder().Type(SchemaValueType.String).Add(new PlaceholderKeyword("e.g. Alex Smith"))),
        ("menus", new JsonSchemaBuilder().PrefixItems(new JsonSchemaBuilder().Enum("LUNCH", "DINNER")).MinItems(1).MaxItems(1))
    )
    .Required("customerName");

var context = new SmokeJsonContext(new JsonSerializerOptions { Converters = { new JsonSchemaConverter(readerOptions) } });
var json = JsonSerializer.Serialize(new Envelope(schema), context.Envelope);
var read = JsonSerializer.Deserialize(json, context.Envelope)!;

var failures = new List<string>();
Check(json == JsonSerializer.Serialize(read, context.Envelope), "The schema does not round-trip.");
Check(
    read.Schema.FindSubschema("/properties/customerName")?.GetKeyword<PlaceholderKeyword>()?.Placeholder == "e.g. Alex Smith",
    "The custom keyword is not typed after a round trip."
);
Check(read.Schema.FindSubschema("/properties/menus/prefixItems/0")?.GetKeyword<EnumKeyword>() is not null, "FindSubschema failed.");
Check(read.Schema.Required is ["customerName"], "The required accessor failed.");

var withoutConst = read.Schema.Rewrite((JsonSchema current, in SchemaLocation _) =>
    current.BooleanValue is null ? current.Without(ConstKeyword.KeywordName) : current);
Check(
    withoutConst.FindSubschema("/properties/id")?.GetKeyword<ConstKeyword>() is null
        && read.Schema.FindSubschema("/properties/id")?.GetKeyword<ConstKeyword>() is not null,
    "Rewrite failed."
);
var pointers = new List<string>();
read.Schema.Walk((JsonSchema _, in SchemaLocation location) =>
{
    pointers.Add(location.ToPointer());
    return WalkAction.Continue;
});
Check(pointers.Contains("/properties/menus/prefixItems/0") && pointers.Count == 5, "Walk failed.");

foreach (var failure in failures)
{
    Console.Error.WriteLine(failure);
}

Console.WriteLine(failures.Count == 0 ? $"OK {json}" : "FAILED");
return failures.Count == 0 ? 0 : 1;

void Check(bool condition, string failure)
{
    if (!condition)
    {
        failures.Add(failure);
    }
}

internal sealed record Envelope([property: JsonPropertyName("$schema")] JsonSchema Schema);

[JsonSerializable(typeof(Envelope))]
internal sealed partial class SmokeJsonContext : JsonSerializerContext;

internal sealed class PlaceholderKeyword(string placeholder) : IJsonSchemaKeyword<PlaceholderKeyword>
{
    public static string KeywordName => "placeholder";

    public string Name => KeywordName;

    public string Placeholder { get; } = placeholder;

    public static PlaceholderKeyword Read(JsonElement value, JsonSchemaReadContext context) => new(context.ReadString(value));

    public void WriteValue(Utf8JsonWriter writer) => writer.WriteStringValue(Placeholder);
}
