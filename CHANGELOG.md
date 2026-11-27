# Release 0.2.0

- Fix: `JsonSchema.Parse` now throws a `JsonException` for a duplicate name in `$vocabulary` or `dependentRequired`. Before, it threw an `ArgumentException` with no location.
  - The `Path` of the exception is the JSON Pointer of the bad entry, for example `/dependentRequired/dessert`.
  - An invalid array in `dependentRequired` also gives the path of its property now.
- Performance: `JsonSchema.Parse` is approximately 15% faster. It allocates the same memory as before.
- Change: text after the schema gives the System.Text.Json error, with the line and the byte position. Before, the message was "The text contains more than one JSON value."

# Release 0.1.0

- Initial release: an AOT-compatible JSON Schema (draft 2020-12) object model, builder and serializer built on System.Text.Json.
