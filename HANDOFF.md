# Handoff: design review and cross-language comparison

Date: 2026-11-05.
Scope: a review of `src/JsonSchemaObjectModel`, then a comparison with JSON Schema libraries in Rust, Go and Java.
The maintainer prefers static typing over dynamic typing, boxing and type erasure. The recommendations follow that preference.

No code in `src/` was changed. The probe programs are in the scratchpad of the session, not in the repository.

## Status

| Item | Status |
|---|---|
| Review of the library | Done |
| Defects confirmed with a probe program | 3 (see "Confirmed defects") |
| Cross-language comparison | Done. Library details come from memory, not from a check of current releases. Verify field names before you cite them. |
| Encoder default decision | Recommended, not implemented |

## Part 1: Review

### Strengths

- **Typed leaves, open container.** Each standard keyword is a sealed class with a typed value (`ulong`, `string`, `ImmutableArray<SchemaValueType>`). Custom keywords get the same treatment without reflection.
- **It keeps what other libraries lose.** Keyword order, the string or array form of `type`, numbers beyond `decimal`, and unknown keywords all survive. Most statically typed libraries lose at least one of these.
- **Structural navigation.** `ISingleSubschemaKeyword`, `ISubschemaArrayKeyword` and `ISubschemaMapKeyword` work like Rust traits. Code can walk a schema without knowledge of each concrete keyword.
- **Error paths cost nothing on success.** `SchemaReadException` collects the location while it unwinds.

### Confirmed defects

These were reproduced with a file-based app (`dotnet run probe.cs` with `#:project` to the library).

1. **An empty `type` array is accepted.**
   - `JsonSchema.Parse("""{"type":[]}""")` succeeds and writes `{"type":[]}`.
   - `new TypeKeyword(Array.Empty<SchemaValueType>())` also succeeds.
   - The 2020-12 meta-schema requires `minItems: 1`, so the output is not a valid schema.
   - Locations: `StandardKeywordReaders.cs:277` (`ReadType`) and `Keywords/ValidationKeywords.cs:72` (`TypeKeyword(IEnumerable<SchemaValueType>)`).
   - Fix: throw in both places. Add a test.

2. **Round-trip is semantic, not textual.**

   ```
   in:  {"pattern":"^[a-z]+$","description":"ต้มยำ <b>&</b>","maxLength":2.0,"minimum":1.50}
   out: {"pattern":"^[a-z]\u002B$","description":"\u0E15\u0E49\u0E21\u0E22\u0E33 \u003Cb\u003E\u0026\u003C/b\u003E","maxLength":2,"minimum":1.50}
   ```

   - The escapes come from the default `JavaScriptEncoder`. See "Encoder default".
   - `2.0` becomes `2` because `NonNegativeIntegerKeyword` stores a `ulong`.
   - `minimum` keeps `1.50`, because `NumberValueKeyword` keeps the JSON.
   - `TestSuiteRoundTripTests` compares with `JsonElement.DeepEquals`, so it passes. The goal "parse, then write, gives the same JSON" in `AGENTS.md` overstates the guarantee. Change the wording to "semantically equal JSON", or fix the causes.

3. **A keyword name is not tied to a keyword type.**
   - `builder.Add(new UnknownKeyword("type", <"string">))` writes `{"type":"string"}`, but `schema.Type` returns `null`.
   - After a re-parse, `schema.Type` returns `String`. The same JSON gives two different object models.
   - The same problem occurs if a custom keyword uses a standard name.
   - Fix options:
     - `JsonSchemaBuilder.Add` (`JsonSchemaBuilder.cs:39`) rejects an `UnknownKeyword` with a standard name.
     - Or use recommendation 1 below, which ties the name to the type.

### Other findings

4. **Boxing and interface allocations.**
   - `PrimitiveJsonValue` stores `object? value` (`Keywords/PrimitiveJsonValue.cs:16`). Each `long`, `decimal`, `double` and `Guid` value for `const`/`enum` is boxed. Use separate fields, one for each kind.
   - `ISubschemaMapKeyword.Schemas` and the `schema.Properties` accessor return `IReadOnlyDictionary<string, JsonSchema>`.
     - A `foreach` over the interface boxes the enumerator.
     - Each map keyword allocates a `ReadOnlyDictionary` wrapper during Parse (`Keywords/KeywordBases.cs:96`).
     - The same pattern is in `VocabularyKeyword` (`Keywords/CoreKeywords.cs:116`) and `DependentRequiredKeyword` (`Keywords/ValidationKeywords.cs:457`).
   - Fix: expose a concrete sealed `SchemaMap` type with a struct enumerator and span lookup. It can still implement `IReadOnlyDictionary`. This also removes one Parse allocation for each map keyword.

5. **`GetKeyword<UnknownKeyword>()` is ambiguous.** It returns the first unknown keyword, whatever its name. Add a by-name accessor, or document the behaviour.

6. **Minor:** `SchemaReadException` boxes `int` segments in a `List<object>`. This occurs only when a parse fails, so it is acceptable. A small `(string? Name, int Index)` struct removes it.

## Part 2: Cross-language comparison

### Four designs

| Design | Examples | Static typing | Lossless and extensible |
|---|---|---|---|
| **A. Wrap untyped JSON** | Rust `schemars` 1.x (`Schema(serde_json::Value)`), Java networknt and victools (Jackson `JsonNode`) | None | Fully lossless. Extension is trivial. |
| **B. One struct with an optional field for each keyword** | Go `google/jsonschema-go`, Go `santhosh-tekuri/jsonschema` (compiled form), Rust `boon`, `schemars` 0.8, Go `swaggest/jsonschema-go` (generated from the meta-schema) | Strong. Field access is O(1). | Loses keyword order. Extensions go into `Extra map[string]any` or a `serde_json::Map`. |
| **C. A sum type for each schema kind** | Rust `openapiv3` (`SchemaKind::Type(Type::String(..))`), swagger-core `StringSchema`, Kotlin sealed classes | Strongest. Matching is exhaustive. | Cannot represent a schema that allows more than one type. Good for code generation, bad as a general model. |
| **D. Ordered list of typed keyword objects** | **This library**, JsonSchema.Net, Java Justify, Kotlin `json-sKema` (each keyword is an AST node), Rust `jsonschema` (compiled `Box<dyn Validate>` list) | Typed leaves, type-erased container | Lossless and extensible |

### Data points

- **schemars moved from B to A in 1.0.** It was the most-used typed schema model in Rust. The typed struct could not keep up with new drafts, unknown keywords and user transforms. This is strong evidence against a change to design B.
- **The Go B-libraries add escape hatches.** `google/jsonschema-go` has:
  - `Type` and `Types` as two fields. This is the same problem that `TypeKeyword.IsArray` solves.
  - `Extra map[string]any`, which is type erasure.
  - A `PropertyOrder` field, added because Go maps do not keep order.
- **santhosh-tekuri is the best hybrid.**
  - Typed fields for standard keywords.
  - Numbers as `*big.Rat`, which is lossless and typed.
  - `Extensions map[string]SchemaExt`, filled by a registered `Vocabulary{URL, Compile}`. Custom vocabularies are typed, but they are in a separate, unordered area.
- **boon uses an arena.** All schemas are in one flat array and refer to each other by index. Allocation is very low, and parent and location data are free. The cost: a schema is a handle into one document, so free composition (as in `JsonSchemaBuilder`) becomes difficult.
- **json-sKema** has a `SchemaVisitor<P>` and a source location on each node.
- **schemars** has `visit::Visitor` and a `Transform` trait. Users rewrite schemas with it, for example for OpenAPI 3.0 or LLM structured output.

## Part 3: Recommendations

The remaining type erasure is in two places:

- The container: `ImmutableArray<IJsonSchemaKeyword>` with `is T` tests.
- Numbers: `JsonElement` as the value of `NumberValueKeyword`.

Do not change to design B. It conflicts with goals 1 and 2 in `AGENTS.md`, and the Go and Rust history shows why. Make design D more static instead.

Recommendations, in order of value against cost:

1. **Use static abstract interface members for keyword definitions.** This is the C# equivalent of a Rust trait with an associated constant.

   ```csharp
   public interface IJsonSchemaKeyword<TSelf> : IJsonSchemaKeyword
       where TSelf : IJsonSchemaKeyword<TSelf>
   {
       static abstract string KeywordName { get; }
       static abstract TSelf Read(JsonElement value, JsonSchemaReadContext context);
   }

   registry.Add<PlaceholderKeyword>();   // no lambda, no name string, AOT-safe
   ```

   - The compiler makes sure that the name and the reader exist. This replaces the run-time check "the reader returned a keyword with a different name".
   - `GetKeyword<T>()` can match on `T.KeywordName`. This fixes defect 3.
   - Keep the delegate overload for dynamic cases.

2. **Add a lossless number type.** Compare `big.Rat` in santhosh-tekuri and `serde_json::Number` with `arbitrary_precision`.
   - A `readonly struct JsonNumber` holds the validated number text. It has `TryGetDecimal`, `TryGetInt64` and `ToBigInteger`.
   - `MinimumKeyword.Value` then has the type "number", not "any JSON, checked at run time".
   - If non-negative integer keywords also keep the text, `2.0` stays `2.0`.

3. **Use concrete collection types in the public API.** See finding 4.

4. **Add a source generator for keyword classes.** Compare swaggest (generated from the meta-schema) and Rust `macro_rules!`.
   - About 50 similar keyword classes, their readers, builder methods and accessors are kept in agreement by hand. One test finds the gaps.
   - A `[JsonSchemaKeyword("placeholder")] partial class` generator also helps users of custom vocabularies, and it is AOT-safe.
   - Not urgent at the current size.

5. **Add a subschema walk and a rewrite with structural sharing.** Compare json-sKema `SchemaVisitor<P>` and the schemars `Transform` trait.
   - The navigation interfaces support pointer lookup. There is no general "visit each subschema" or "rebuild with changes".
   - A bottom-up `Rewrite(Func<JsonSchema, JsonSchema>)` that reuses unchanged nodes suits an immutable model.
   - Use cases: remove `format` for an LLM structured-output API, or downgrade a schema for OpenAPI 3.0.

6. **Add structural equality.** Rust `#[derive(PartialEq)]` and Kotlin data classes give this for free. `IEquatable<JsonSchema>` on top of `JsonElement.DeepEquals` makes tests, de-duplication and caching easier.

7. **Share common keyword instances.** Rust and Go store these values inline, so they cost nothing there. `TypeKeyword.For` already does this for `type`. Do the same for `additionalProperties: false`, `minItems: 1`, `minLength: 0` and `1`, and `uniqueItems: true`. This reduces allocations on Build and Parse.

### Do not copy

- **Design B or C as the core model.** It loses keyword order, the form of `type`, and first-class custom keywords. It also needs an `Extra` bag, which is type erasure.
- **The boon arena.** It gives the lowest allocation, but it makes `JsonSchema` a handle into one document and stops free composition in the builder. Consider it only if Parse allocation becomes the main concern.
- **Streaming reads from `Utf8JsonReader`, as serde does.** This removes the DOM pass. But `const`, `enum` and unknown keywords share the parsed document, so the cost moves to a different place.
- **Discovery with `ServiceLoader`, as Justify does.** It uses global state and reflection, which goal 2 forbids.

## Encoder default

### Current behaviour (verified)

- `ToJsonString(JsonWriterOptions options = default)` accepts an `Encoder`.
- A non-null `Encoder` fails `PooledJsonWriter.CanReuse`. The call then allocates a new buffer and a new writer each time.
- `WriteTo(Utf8JsonWriter)` and `JsonSchemaConverter` use the caller's encoder. The converter follows `JsonSerializerOptions.Encoder`.
- Output of the probe:

  ```
  default: {"pattern":"^[a-z]\u002B$","description":"\u0E15\u0E49\u0E21\u0E22\u0E33 \uD83C\uDF5C \u003Cb\u003E\u0026\u003C/b\u003E \u0027x\u0027"}
  relaxed: {"pattern":"^[a-z]+$","description":"ต้มยำ \uD83C\uDF5C <b>&</b> 'x'"}
  ```

- `UnsafeRelaxedJsonEscaping` still escapes characters outside the Basic Multilingual Plane (for example emoji) as surrogate pairs. This is a known limit of the System.Text.Json encoders. Only a custom `JavaScriptEncoder` subclass writes them raw.

### What other serializers do by default

| Ecosystem | Default escaping | `+` | Non-ASCII | `< > &` |
|---|---|---|---|---|
| Rust `serde_json` (schemars, jsonschema, boon) | Only what JSON requires: `"`, `\`, control characters | raw | raw | raw |
| Java Jackson (networknt, victools, Justify) | Only what JSON requires | raw | raw | raw |
| Go `encoding/json` (google/jsonschema-go, invopop, santhosh-tekuri) | Escapes HTML characters. `json.Marshal` always does. `Encoder.SetEscapeHTML(false)` stops it. | raw | raw | `<` and others |
| Go `encoding/json/v2` (experimental) | Does not escape HTML by default | raw | raw | raw |
| Java Gson | Escapes HTML characters by default (`disableHtmlEscaping()`) | raw | raw | escaped, also `=` and `'` |
| .NET Newtonsoft (NJsonSchema) | Only what JSON requires | raw | raw | raw |
| .NET System.Text.Json default (this library, JsonSchema.Net) | Escapes HTML characters, non-ASCII and `+` | escaped | escaped | escaped |
| JavaScript `JSON.stringify` | Only what JSON requires | raw | raw | raw |

These defaults come from memory, not from a check of each current release.

Conclusions:

- System.Text.Json is the only ecosystem that escapes non-ASCII and `+` by default.
- Go and Gson escape HTML characters so that output is safe inside a `<script>` block. Go v2 drops this default.
- `+` is escaped as a defence against old UTF-7 attacks.
- JsonSchema.Net inherits the escapes unless the caller supplies an encoder. NJsonSchema writes raw text.
- The `GlobalSetup` check in the benchmarks compares JSON from each library. It passes either because the restaurant schema has no `+`, non-ASCII or HTML characters, or because it compares semantically. Check which, before you change the default.

### Recommendation

Make `UnsafeRelaxedJsonEscaping` the default for `ToJsonString` only. Do not change `WriteTo` or the converter, because the caller owns the writer there.

- **Reason:** schemas usually go into files, OpenAPI documents, LLM tool definitions and diffs. In these places, a readable `pattern` is important and HTML safety is not.
- **Implementation:**
  - In `ToJsonString`, use the relaxed encoder when `options.Encoder` is `null`. The pooled writer uses `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`.
  - Let `CanReuse` also accept that encoder instance, so an explicit relaxed encoder keeps the fast path.
  - A caller that needs HTML-safe output passes `JavaScriptEncoder.Default`.
- **Cost:**
  - It is a change in behaviour. Record it in `CHANGELOG.md`. This is acceptable before 1.0.
  - Add an XML-doc note. "Unsafe" applies only when the output goes into HTML or JavaScript. The JSON stays valid.
- **Emoji:** a custom encoder that writes them raw is possible, but it is more code to maintain for a small gain. Do not do it unless a user asks.

Alternative: keep the System.Text.Json default for consistency with .NET, and document `UnsafeRelaxedJsonEscaping` in the README. This is defensible, but each user then finds the `+` problem alone.

## Suggested next steps

1. Fix defect 1: reject an empty `type` array in the reader and in `TypeKeyword`. Add a test.
2. Decide on the encoder default, then implement it with a `CHANGELOG.md` entry.
3. Remove the boxing in `PrimitiveJsonValue`, and add a concrete `SchemaMap` type (finding 4). Run the benchmarks, because Parse and Build are hot paths.
4. Fix defect 3, with a name check in `JsonSchemaBuilder.Add` or with recommendation 1.
5. Correct the round-trip wording in `AGENTS.md` and `README.md`, or fix the `2.0` and escape cases.
6. Consider recommendations 2, 5 and 6 as API work for a later release.

Before you call any change done, follow `AGENTS.md`: `dotnet build`, `dotnet test`, the AOT smoke test for `src/` changes, and the benchmarks for hot-path changes.

## Follow-up (2026-11-05)

Done:

- Defect 1: the reader and `TypeKeyword` reject an empty `type` array.
- Defect 2: the wording in `AGENTS.md` and `README.md` now says "semantically equal JSON". The library still writes `2.0` as `2`.
- Defect 3: not changed in code. A builder check was tried and removed: a registry without a standard reader produces an `UnknownKeyword` with a standard name, so the check broke copies of parsed schemas. The docs now say that the typed accessors only see typed keywords.
- Encoder default: `ToJsonString` uses `UnsafeRelaxedJsonEscaping` when the options have no encoder. `WriteTo` and the converter are unchanged.
- Finding 4 and recommendation 3:
  - `SchemaMap` replaces `IReadOnlyDictionary` for map keywords. It has a struct enumerator and span lookup.
  - `PrimitiveJsonValue` no longer boxes.
  - A builder's string `enum` is a plain string array.
- Recommendation 1: `IJsonSchemaKeyword<TSelf>` and `registry.Add<T>()` / `Set<T>()`. The delegate overloads stay. The interface has no default `Name`, because a default interface member cannot be called on the concrete type.
- A duplicate name in a map keyword is reported at its own location (`/properties/dish`), as soon as the reader meets it.
- Recommendation 7: `For(...)` factories share common keyword instances.

Deferred: recommendations 2 (`JsonNumber`), 5 (rewrite) and 6 (equality), finding 5 (a doc note only).

## Follow-up 2 (2026-11-05)

Small fixes:

- `multipleOf` read its sign through a `double`, so `1e-400` failed as "not greater than zero". The reader and `MultipleOfKeyword` now read the sign from the JSON text, with the internal `JsonNumberParts`.
- Non-negative integers are read from the JSON text. Any integer form works (`2.0`, `1e2`, `100e-2`). A value above `ulong.MaxValue` fails with a clear message. `AGENTS.md` and `README.md` record this limit as deliberate.
- Finding 6: `SchemaReadException` stores its path segments as a struct, not as boxed objects.

Corrections to this document:

- The sample outputs in "Round-trip is semantic" and "Encoder default" lost their escapes when the file was written. They are now restored. With the default encoder, `+` is written as `\u002B`, and the Thai text and `< > &` are escaped. With the relaxed encoder, only the emoji is escaped.
- Finding 5: `JsonSchema.GetKeyword(string name)` already gives by-name access.
- The benchmark `GlobalSetup` compares JSON with `JsonElement.DeepEquals`, so the encoder does not affect it.
