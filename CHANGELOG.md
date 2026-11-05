# Release 0.1.0

- Initial release: an AOT-compatible JSON Schema (draft 2020-12) object model, builder and serializer built on System.Text.Json.
- `ToJsonString` escapes only what JSON requires (`UnsafeRelaxedJsonEscaping`), so `pattern` values and non-ASCII text stay readable. Pass `JavaScriptEncoder.Default` for HTML-safe output. `WriteTo` and the converter use the caller's encoder.
- Custom keyword types can implement `IJsonSchemaKeyword<TSelf>` and register with `registry.Add<T>()`. The delegate overloads stay.
- `SchemaMap` is the value of `properties`, `$defs` and the other map keywords: immutable, ordered, with a struct enumerator and span lookup.
- The reader and the builder share instances of common keywords (`For(...)` factories).
- A `type` array must not be empty.
