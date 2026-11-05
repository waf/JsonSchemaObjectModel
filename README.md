# JsonSchemaObjectModel

An AOT-compatible JSON Schema (draft 2020-12) **object model** for .NET, built on System.Text.Json.

It builds, parses, writes and navigates schemas. It does **not** validate instances. For validation, give the
JSON it writes to a validator such as [LateApexEarlySpeed.Json.Schema](https://github.com/lateapexearlyspeed/Lateapexearlyspeed.JsonSchema).

- All draft 2020-12 keywords are typed. Unknown keywords are kept as JSON, so every schema round-trips to semantically
  equal JSON: the same keywords in the same order, with the same values. String escapes and the form of a number such
  as `2.0` can differ.
- Custom keywords are typed too. You register a reader for each one. No reflection, no global state.
- Native AOT and trimming safe (`IsAotCompatible`). A Native AOT smoke test runs in CI.
- Every schema in the official [JSON Schema Test Suite](https://github.com/json-schema-org/JSON-Schema-Test-Suite)
  (draft 2020-12) round-trips. An independent validator checks the output against the 2020-12 meta-schema.

This library is intentionally only a lightweight object model. If you need additional features, they can be built
on top of this package. If you need something more full-featured, try the excellent
[JsonSchema.Net](https://github.com/json-everything/json-everything) library.

## Build a schema

```csharp
using JsonSchemaObjectModel;

JsonSchema restaurantSchema = new JsonSchemaBuilder()
    .Schema(SchemaKeyword.Draft202012)
    .Type(SchemaValueType.Object)
    .Properties(
        ("ownerEmail", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Email)),
        ("menus", new JsonSchemaBuilder().Type(SchemaValueType.Array).MinItems(1))
    )
    .Required("ownerEmail");

string json = restaurantSchema.ToJsonString();
```

## Parse and navigate

```csharp
using JsonSchemaObjectModel.Keywords;

var restaurantSchema = JsonSchema.Parse(json);

JsonSchema? ownerEmail = restaurantSchema.FindSubschema("/properties/ownerEmail");   // RFC 6901 JSON Pointer
string? format = ownerEmail?.Format;                                                 // accessor for common keywords
MinItemsKeyword? minItems = restaurantSchema.FindSubschema("/properties/menus")?.GetKeyword<MinItemsKeyword>();
```

Two schemas are equal if they write equal JSON. The order of keywords and properties is not important, and numbers
compare by value, so `{"maximum":1,"type":"number"}` equals `{"type":"number","maximum":1.0}`. A schema can be a
dictionary key.

`JsonSchema` has a System.Text.Json converter, so it works as a property of a serialized type,
including with source-generated `JsonSerializerContext`s.

`ToJsonString` escapes only what JSON requires (`UnsafeRelaxedJsonEscaping`), so a `pattern` such as `^[a-z]+$`
stays readable. Pass `new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }` for HTML-safe output.
`WriteTo` and the converter use the encoder of the writer that you give them.

## Custom keywords and vocabularies

Implement `IJsonSchemaKeyword<T>`, then register the type:

```csharp
sealed class PlaceholderKeyword(string placeholder) : IJsonSchemaKeyword<PlaceholderKeyword>
{
    public static string KeywordName => "placeholder";
    public string Name => KeywordName;
    public string Placeholder { get; } = placeholder;

    public static PlaceholderKeyword Read(JsonElement value, JsonSchemaReadContext context) =>
        new(context.ReadString(value));

    public void WriteValue(Utf8JsonWriter writer) => writer.WriteStringValue(Placeholder);
}

var readerOptions = new JsonSchemaReaderOptions(
    JsonSchemaKeywordRegistry.CreateDraft202012().Add<PlaceholderKeyword>());

var schema = JsonSchema.Parse(json, readerOptions);
var serializerOptions = new JsonSerializerOptions { Converters = { new JsonSchemaConverter(readerOptions) } };
```

- The compiler checks that the type has a name and a reader. The reader checks that `Name` matches `KeywordName`.
- For a reader with run-time configuration, or one type for many names (for example every `x-*` keyword), implement
  `IJsonSchemaKeyword` and register a delegate: `registry.Add("x-ui", (value, context) => ...)`.
- Add a keyword to a schema with `JsonSchemaBuilder.Add(keyword)`.
- The read context gives typed helpers (`ReadString`, `ReadSchema`, `ReadSchemaArray`, ...). Its `Error` method
  makes a `JsonException` whose `Path` is the JSON Pointer of the bad value.
- Declare the keywords in a meta-schema with `$vocabulary`, so that other implementations can check them.
  Mark the vocabulary `false` (optional), so that validators that do not know it still process the schema.
  `tests/JsonSchemaObjectModel.Tests/TestData/CustomVocabulary` has an example.

## Not in scope

- Instance validation, output formats and format assertion.
- `$ref` resolution. `FindSubschema` walks the document structure only.
- Non-negative integers above `ulong.MaxValue` in `minLength`, `maxItems` and the other count keywords. They fail to parse.
- Drafts other than 2020-12. A 2019-09 schema with an array-form `items` fails to parse.

## Development

```
git submodule update --init
dotnet build
dotnet test
dotnet publish tests/JsonSchemaObjectModel.AotSmoke -c Release -o artifacts/aot && artifacts/aot/JsonSchemaObjectModel.AotSmoke
```

### Benchmarks

`tests/JsonSchemaObjectModel.Benchmarks` compares Build, Write, Parse and Navigate against JsonSchema.Net and
NJsonSchema, on a restaurant-order schema with 1 and 9 order lines.

```
dotnet run -c Release --project tests/JsonSchemaObjectModel.Benchmarks -- --filter *
```

JsonSchema.Net 9.x binaries are under the json-everything maintenance-fee EULA. This repository uses them only to
benchmark, and the package does not depend on them.

On Windows, Native AOT needs the Visual Studio C++ tools. It also needs `vswhere.exe`
(in `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer`) on `PATH`.

## Licence

MIT. The vendored draft 2020-12 meta-schemas in `tests/JsonSchemaObjectModel.Tests/TestData/MetaSchemas` are
© JSON Schema Specification Authors, under the licence in that folder.
