# JsonSchemaObjectModel

[![Nuget](https://img.shields.io/nuget/v/JsonSchemaObjectModel.svg?style=flat&color=005ca4)](https://www.nuget.org/packages/JsonSchemaObjectModel/)
[![Code Coverage](https://codecov.io/gh/waf/JsonSchemaObjectModel/branch/main/graph/badge.svg?token=DVRUKSDMJ9)](https://app.codecov.io/gh/waf/JsonSchemaObjectModel)
[![Build Status](https://github.com/waf/JsonSchemaObjectModel/workflows/main%20build/badge.svg)](https://github.com/waf/JsonSchemaObjectModel/actions/workflows/main.yml)

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

## Walk and rewrite

A schema is immutable. `Rewrite` returns a changed copy, and shares every part that did not change. The rewriter gets
each schema after its subschemas, so one pass can change a whole tree. For example, to prepare a restaurant-order
schema for a strict structured-output API:

```csharp
var strict = restaurantSchema.Rewrite((JsonSchema schema, in SchemaLocation location) =>
{
    if (schema.BooleanValue is not null)
    {
        return schema;
    }

    schema = schema.Without(FormatKeyword.KeywordName).Without(PatternKeyword.KeywordName);
    return schema.Properties is { } properties
        ? schema.With(new RequiredKeyword(properties.Keys)).With(AdditionalPropertiesKeyword.For(false))
        : schema;
});
```

`Walk` visits each schema, parent first. The visitor returns `Continue`, `SkipChildren` or `Stop`.

```csharp
restaurantSchema.Walk((JsonSchema schema, in SchemaLocation location) =>
{
    Console.WriteLine($"{location.ToPointer()}: {schema.Type}");   // for example "/properties/menus: Array"
    return WalkAction.Continue;
});
```

- `With(keyword)` and `Without(name)` edit one schema. They return the same instance if nothing changes.
- Both methods have an overload for a struct that implements `ISchemaVisitor` or `ISchemaRewriter`. With a struct,
  a walk or a rewrite that changes nothing allocates nothing.
- The walk and the rewrite go into subschema keywords only. They do not resolve `$ref`.

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
- A keyword with subschemas implements `ISingleSubschemaKeyword`, `ISubschemaArrayKeyword` or `ISubschemaMapKeyword`,
  or derives from `SingleSubschemaKeyword` and the other base classes. Then `FindSubschema`, `Walk` and `Rewrite` go into it.
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
