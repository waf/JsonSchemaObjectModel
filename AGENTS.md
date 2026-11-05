## What this is

`JsonSchemaObjectModel` is a JSON Schema (draft 2020-12) **object model** for .NET, built on System.Text.Json and published to NuGet.
It builds, parses, writes and navigates schemas. It does not validate instances.

Everything targets `net10.0`. `global.json` pins the SDK to 10.0.403 (`latestFeature`) and the test runner to Microsoft.Testing.Platform.

## Goals and non-goals

Goals, in priority order:

1. **A lossless object model.** Every schema round-trips: parse, then write, gives semantically equal JSON.
   - The keyword order, the values, and the string or array form of `type` stay the same.
   - The text can differ. String escapes depend on the encoder, and a non-negative integer such as `2.0` is written as `2`.
   - The tests compare with `JsonElement.DeepEquals`.
   - All draft 2020-12 keywords are typed classes.
   - A keyword with no reader is kept as an `UnknownKeyword` that holds its JSON.
   - Numbers are kept as JSON, so values beyond `decimal` and `double` survive. Checks on numbers, such as `multipleOf` > 0, read the JSON text, not a `double`.
   - One deliberate limit: the non-negative integer keywords (`minLength`, `maxItems`, ...) hold a `ulong`. A value above `ulong.MaxValue` fails to parse. No real length or count comes near it.
   - `type` keeps its string or array form.
2. **Custom keywords and vocabularies are first class.** A caller registers a reader and gets typed keywords back. There is no global registry and no reflection.
3. **Native AOT and trimming safe.** The library sets `IsAotCompatible`, and warnings are errors. Do not add reflection, `dynamic`, `Activator`, reflection-based `JsonSerializer` calls, or attribute scanning.
4. **Low allocation, within reason.**
   - Build, Parse, Write and Navigate are the hot paths. The benchmarks measure them against JsonSchema.Net and NJsonSchema.
   - Keep each hot path at or below its current allocations.
5. **Immutable and thread-safe.** A `JsonSchema` and its keywords never change after construction. `JsonSchemaReaderOptions` is a frozen snapshot that is safe to share.

Non-goals. Do not add these without the maintainer's agreement:

- Instance validation, output formats or format assertion. The README points users to JsonSchema.Net or LateApexEarlySpeed.Json.Schema for those.
- `$ref` resolution. `FindSubschema` walks the document structure only.
- Drafts other than 2020-12.
- Runtime dependencies. The package depends only on the BCL.

## Build, test, benchmark

```console
git submodule update --init       # tests/JSON-Schema-Test-Suite is a submodule
dotnet build                      # whole solution; warnings are errors
dotnet test                       # all tests (Microsoft.Testing.Platform + xUnit v3)
```

- **Running a subset of tests:** use the MTP filters, not VSTest syntax. Examples:
  - `dotnet test --project tests/JsonSchemaObjectModel.Tests/JsonSchemaObjectModel.Tests.csproj --filter-class "*CustomVocabularyTests"`
  - `--filter-method "*FindSubschema*"`
- **Fast direct run:** you can also run the test executable directly: `tests/JsonSchemaObjectModel.Tests/bin/Debug/net10.0/JsonSchemaObjectModel.Tests.exe -class <FullyQualifiedClassName>`.
- **Native AOT smoke test:** publish the smoke app with Native AOT, then run it. It exits with 0 on success. It must publish with no `IL2xxx` or `IL3xxx` warnings.

  ```console
  dotnet publish tests/JsonSchemaObjectModel.AotSmoke -c Release -o artifacts/aot
  artifacts/aot/JsonSchemaObjectModel.AotSmoke
  ```

  On Windows, the native linker needs the Visual Studio C++ tools. It also needs `vswhere.exe` on `PATH`, from `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer`. CI runs the AOT smoke test on Linux only.
- **Benchmarks:** BenchmarkDotNet, with Build, Write, Parse and Navigate on a restaurant-order schema of 1 and 9 order lines.

  ```console
  dotnet run -c Release --project tests/JsonSchemaObjectModel.Benchmarks -- --filter *
  dotnet run -c Release --project tests/JsonSchemaObjectModel.Benchmarks -- --filter *Parse*
  ```

  - A full run takes a few minutes.
  - `GlobalSetup` checks that every library produces the same JSON and finds the same keyword. If it throws, the comparison is invalid. Fix the setup, not the check.
  - The JsonSchema.Net benchmarks give each build its own `SchemaRegistry`. JsonSchema.Net 8 and later register every built schema in `SchemaRegistry.Global`, which grows without limit in a long-running process.
  - JsonSchema.Net 9.x binaries are under a maintenance-fee EULA. They are a benchmark-only dependency. Never reference them from `src/`.

Before you call a change done: run `dotnet build`, then `dotnet test`. If the change touches `src/`, also run the AOT smoke test. If the change touches a hot path, run the benchmarks.

## Layout

- `src/JsonSchemaObjectModel/`:
  - `JsonSchema.cs`: the immutable schema. It has `Parse`, `FromJsonElement`, `WriteTo`, `ToJsonString`, `GetKeyword`, `FindSubschema` (JSON Pointer), `With`, `Without` and equality.
  - `JsonSchema.Traversal.cs`: `Walk` (parent first) and `Rewrite` (children first, with structural sharing).
  - `SchemaLocation.cs`: `SchemaLocation`, the visitor and rewriter contracts, and the pooled path of a walk.
  - `JsonSchemaBuilder.cs`: the fluent builder and the `JsonValues` helpers.
  - `Reading.cs`: `JsonSchemaKeywordRegistry`, `JsonSchemaReaderOptions`, `JsonSchemaReadContext`, the internal reader, and `SchemaReadException`.
  - `StandardKeywordReaders.cs`: the draft 2020-12 readers, and the typed read helpers on `JsonSchemaReadContext`.
  - `SchemaMap.cs`: the immutable ordered map of named subschemas, for `properties`, `$defs` and the other map keywords.
  - `JsonNumber.cs`: the public JSON number type for `minimum`, `maximum`, `multipleOf` and the exclusive limits. Equality compares values (`1` equals `1.0`).
  - `JsonValueEquality.cs`: the equality and hash rules for JSON values. `Keywords/KeywordEquality.cs` applies them to keywords.
  - `JsonNumberParts.cs`: the internal reader of number text (sign, significand, scale). Use it for checks and comparisons on numbers. Do not convert to `double`.
  - `Keywords/`: one class per keyword, grouped by vocabulary.
    - The base classes in `KeywordBases.cs` group keywords by value shape.
    - `PrimitiveJsonValue.cs` holds a builder-supplied `const` value.
    - `SharedKeywords.cs` builds the caches for shared keyword instances.
  - `JsonSchemaAccessors.cs`: extension properties such as `schema.Properties` and `schema.Required`.
  - `JsonSchemaConverter.cs`: the System.Text.Json converter.
- `tests/JsonSchemaObjectModel.Tests/`: the xUnit v3 tests.
  - `TestSuiteRoundTripTests` runs every draft 2020-12 schema in the official Test Suite.
  - `TestData/MetaSchemas` holds the vendored 2020-12 meta-schemas, which are BSD-licensed. Tests do not use the network.
  - `TestData/CustomVocabulary` holds an example `$vocabulary` dialect.
  - LateApexEarlySpeed.Json.Schema is a test-only, independent validator that checks the output against meta-schemas.
- `tests/JsonSchemaObjectModel.AotSmoke/`: the Native AOT smoke app.
- `tests/JsonSchemaObjectModel.Benchmarks/`: the BenchmarkDotNet comparisons.
- `tests/JSON-Schema-Test-Suite/`: the git submodule.

## Design notes

- **Keywords:** each keyword implements `IJsonSchemaKeyword` (`Name`, `WriteValue`).
  - A custom keyword type can implement `IJsonSchemaKeyword<TSelf>` instead. It has a static `KeywordName` and `Read`, and registers with `registry.Add<T>()`. It also declares `public string Name => KeywordName;`. The interface has no default `Name`, because a default interface member cannot be called on the concrete type.
  - Keep the delegate overloads `Add(name, reader)` and `Set(name, reader)`. They serve readers with run-time configuration, and one type for many names.
  - An `UnknownKeyword` can have a standard name, for example after a parse with a registry that has no reader for it. The builder accepts it, so a copy of a parsed schema works. The typed accessors only see typed keywords.
  - A keyword with subschemas also implements `ISingleSubschemaKeyword`, `ISubschemaArrayKeyword` or `ISubschemaMapKeyword`. `FindSubschema` and the tests use those interfaces to walk a schema, so a custom keyword with subschemas must implement one.
    - Each interface has a "with" method (`WithSchema` or `WithSchemas`) that returns a keyword of the same type with other subschemas. It must return `this` when the subschemas are the current instances, so that edits share unchanged parts.
    - The 19 standard subschema keywords override it with a covariant return type. The single-subschema keywords call their `For` factory, so `true` and `false` stay shared.
  - `JsonSchema.With` and `JsonSchema.Without` make an edited copy of a schema. They throw for a boolean schema, because `true` is not `{}`.
  - Each standard keyword has a `KeywordName` constant. Its reader is registered in `StandardKeywordReaders.AddDraft202012`.
  - The test `Every_builder_method_writes_a_valid_draft_2020_12_schema_that_round_trips` fails when a standard keyword has no builder method.
- **Reading:**
  - The reader returns the registry's own name string for a known keyword, so parsing does not allocate keyword names.
  - Each keyword reader gets the `JsonElement` value and a `JsonSchemaReadContext`.
  - The reader rejects duplicate keywords. A keyword whose `Name` differs from its JSON name is a bug in the reader.
- **Errors:**
  - Readers throw `context.Error(message)`. A `SchemaReadException` collects the location while it unwinds, and `JsonSchemaReader.ReadRoot` turns it into a public `JsonException` whose `Path` is the JSON Pointer of the bad value.
  - A successful parse allocates no path objects. Keep it that way: do not track paths on the success path.
- **Allocation techniques.** Keep these when you change the code:
  - **`Parse` and the converter:** they use `JsonElement.ParseValue`, which gives one document that owns its memory. `Clone()` on its elements is then free, so `const`, `enum` and unknown keywords share it.
  - **Builder values:** they are written directly, and become `JsonElement`s only when `.Value`/`.Values` is read. That happens lazily, and a race between threads at most makes a spare copy.
    - A `const` value is a `PrimitiveJsonValue`. Its numeric and GUID values share one 16-byte field, so nothing is boxed.
    - A string `enum` from the builder is a plain string array. It does not use `PrimitiveJsonValue`, which would make each entry twice as large.
    - A number for `minimum`, `maximum` and the other number keywords is a `JsonNumber`. A builder value stays a `long`, `decimal` or `double`, so it needs no JSON document. A parsed value keeps its element, which shares the document of the parse.
  - **Arrays and maps:**
    - Readers make exact-size arrays and wrap them with `ImmutableCollectionsMarshal.AsImmutableArray`.
    - A keyword adopts an `ImmutableArray` without copying it.
    - A map keyword holds a `SchemaMap`, which is immutable, so a keyword adopts one without a copy. The reader builds its entry array at the exact size.
  - **Shared instances:** keywords are immutable, so the reader and the builder share instances through `For(...)` factories.
    - `TypeKeyword.For` for a single type.
    - `FormatKeyword.For` for the 19 standard formats. The reader matches the UTF-8 value, so it allocates nothing for them.
    - The integer keywords (`minItems`, `maxLength`, ...) for 0 to 16.
    - The boolean keywords (`uniqueItems`, `deprecated`, `readOnly`, `writeOnly`).
    - The subschema keywords that often hold `true` or `false` (`additionalProperties`, `items`, `not`, ...).
    - Call the `For` factory, not the constructor, in the reader and the builder.
  - **Builder calls:** the builder has `params ReadOnlySpan<T>` overloads, so fluent calls do not allocate params arrays. Keep an `IEnumerable<T>` overload next to each one for collections.
  - **Writing:** `ToJsonString` reuses a buffer and a `Utf8JsonWriter` for each thread when the options are default. It handles nested calls, and does not keep buffers over 64 KB.
  - **Walk and rewrite:** a struct visitor or rewriter is a generic argument, so the calls have static dispatch and no delegate. The path is a pooled array, and `SchemaLocation` is a `ref struct` over it, so a walk or a rewrite that changes nothing allocates nothing. `ToPointer` allocates only when called. A rewrite copies a keyword array, a subschema array or a map entry array only when the first entry changes. `AllocationPathTests` checks the zero-allocation case.
  - **Navigating:** `SchemaMap` searches up to 16 entries linearly, and builds an index for larger maps. Both lookups accept a `ReadOnlySpan<char>`, so `FindSubschema` allocates no key strings.
    - `foreach` over a `SchemaMap` uses a struct enumerator.
    - `Keys` and `Values` are `readonly struct` views of the entry array, with struct enumerators. They are boxed only through `IEnumerable<T>`. Do not turn them back into cached classes: that adds a mutable field and an allocation.
- **Escaping:** `ToJsonString` uses `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` when the options have no encoder, so `pattern` values and non-ASCII text stay readable. `WriteTo` and the converter use the encoder of the caller's writer. Do not change that: the caller owns the writer.
- **Equality:** `JsonSchema.Equals` is true if, and only if, the two schemas write equal JSON, as `JsonElement.DeepEquals` defines it.
  - The order of keywords and object properties is not important. Numbers compare by value. Strings compare after unescaping.
  - `true` and `{}` are not equal, and `"string"` and `["string"]` are not equal. The comparison is structural, not about validation.
  - `JsonValueEquality.cs` holds the rules and the hash helpers. Numbers use `JsonNumberParts`, because `JsonElement.DeepEquals` throws for an exponent beyond `int`.
  - Keywords of the same type compare their fields through the internal `IKeywordValueEquality`. Keywords of different types, and custom keywords, compare the JSON that they write.
  - A typed keyword's hash code must be the hash code of the JSON that it writes. Then a typed keyword and an `UnknownKeyword` with the same JSON are equal with the same hash code. Build the hash with the `JsonValueEquality` helpers.
  - `EqualityTests.Typed_and_untyped_parses_of_a_schema_are_equal_with_the_same_hash_code` checks this for every Test Suite schema. If you add a keyword with a new value shape, implement `IKeywordValueEquality` and run that test.
- **Thread safety:** everything is immutable after construction, except these:
  - The lazy element caches on value keywords (`JsonValueKeyword`, `JsonValueArrayKeyword`). They are benign races: two threads can each create an element, and one is kept.
  - The thread-static writer cache in `JsonSchema.ToJsonString`.
  - The hash code cache on `JsonSchema`. It is an `int`, and every thread computes the same value, so the race is benign.
  - Do not add another lazy cache without a reason. If you add one, add it to this list.
- **The public API is pre-1.0.** Breaking changes are acceptable. Record them in `CHANGELOG.md`.

## Conventions

- **C#:** latest version, nullable enabled, `AnalysisLevel` `latest-recommended`, warnings are errors.
- **CA1720** is suppressed in the repo. The spec names types `object`, `string` and `integer`, and defines JSON Pointer.
- **Fields** have no underscore prefix.
- **Extension methods:** write new ones with the C# `extension(...)` block syntax.
- **Comments and XML docs:** write them in ASD-STE100 Simplified Technical English. Use short sentences, active voice, and one idea per sentence. Format lists as markdown `-` bullets, and never use `<list>` in XML docs.
- **Public types:** every public type and member has XML docs, because `GenerateDocumentationFile` is on.
- **Tests:** prefer real data, such as the official Test Suite and meta-schemas, over synthetic data. Prefer coarse tests of real behaviour over tests of mock wiring.
- **Example themes:** examples, tests and benchmarks use a restaurant/menu/order theme. Do not use domains from other projects.
- **Package versions:** they live in `Directory.Packages.props` (Central Package Management). `nuget.config` uses nuget.org only, with package source mapping.

## Release

See `PublishRelease.md`. In short:

1. Bump `<Version>` in `src/JsonSchemaObjectModel/JsonSchemaObjectModel.csproj`.
2. Add a `# Release <version>` section to `CHANGELOG.md`, and merge to `main`.
3. Run `./publish-release.ps1`. It pushes a `v<version>` tag.

The tag triggers `.github/workflows/release.yml`. That workflow packs the library and pushes it to nuget.org with the `NUGET_API_KEY` secret. It then creates a GitHub release from the changelog section.

## Gotchas

- **`dotnet sln add`:** in the .NET 11 RC SDK, it fails with "Solution folder 'src' already contains a project". Edit `JsonSchemaObjectModel.slnx` by hand.
- **SourceLink warnings:** "Repository has no remote/commit" is expected until the repository is pushed.
- **Test projects need three packages:**
  - `Microsoft.Testing.Platform.MSBuild`, or `dotnet test` reports a handshake failure.
  - `Microsoft.Testing.Extensions.TrxReport`, for `--report-trx`.
  - `Microsoft.Testing.Extensions.CodeCoverage`, for `--coverage`.
- **Name resolution in benchmarks:** inside `namespace JsonSchemaObjectModel.Benchmarks`, unqualified `SchemaValueType`, `Formats` and keyword names resolve to this library before any `using Json.Schema`. Qualify the JsonSchema.Net types, for example `JsonEverything.SchemaValueType`.
