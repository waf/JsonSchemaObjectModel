using System.Collections.Immutable;
using System.Text.Json;

namespace JsonSchemaObjectModel;

/// <summary>
/// A keyword in a schema object.
/// </summary>
/// <remarks>
/// Implement this interface to add a custom keyword.
/// Register a reader for it in a <see cref="JsonSchemaKeywordRegistry"/> so that parsing can create it.
/// </remarks>
public interface IJsonSchemaKeyword
{
    /// <summary>
    /// The name of the keyword, as it appears in JSON.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Writes the keyword value.
    /// The caller has already written the property name.
    /// </summary>
    void WriteValue(Utf8JsonWriter writer);
}

/// <summary>
/// A keyword type that defines its own name and reader. Register it with <see cref="JsonSchemaKeywordRegistry.Add{T}"/>.
/// </summary>
/// <remarks>
/// - The compiler makes sure that the type has a name and a reader. No lambda or name string is needed.
/// - Declare <c>public string Name => KeywordName;</c> as well. The reader checks that the two names are the same.
/// - The registration does not use reflection, so it is safe for Native AOT.
/// - For a reader with run-time configuration, or one type for many names, use
///   <see cref="JsonSchemaKeywordRegistry.Add(string, JsonSchemaKeywordReader)"/> instead.
/// </remarks>
/// <typeparam name="TSelf">The keyword type itself.</typeparam>
public interface IJsonSchemaKeyword<TSelf> : IJsonSchemaKeyword
    where TSelf : class, IJsonSchemaKeyword<TSelf>
{
    /// <summary>
    /// The name of the keyword, as it appears in JSON.
    /// </summary>
    static abstract string KeywordName { get; }

    /// <summary>
    /// Creates the keyword from its JSON value.
    /// </summary>
    /// <param name="value">The keyword value. Clone it if the keyword keeps it.</param>
    /// <param name="context">Reads subschemas and reports errors with their location.</param>
    static abstract TSelf Read(JsonElement value, JsonSchemaReadContext context);
}

/// <summary>
/// A keyword whose value is one subschema, for example <c>not</c> or <c>items</c>.
/// </summary>
/// <remarks>
/// - <see cref="JsonSchema.FindSubschema(ReadOnlySpan{char})"/>, <c>JsonSchema.Walk</c> and
///   <c>JsonSchema.Rewrite</c> use the three subschema interfaces to go into subschemas.
/// - A custom keyword with subschemas must implement one of them. Otherwise those methods do not see its subschemas.
/// </remarks>
public interface ISingleSubschemaKeyword : IJsonSchemaKeyword
{
    /// <summary>
    /// The subschema.
    /// </summary>
    JsonSchema Schema { get; }

    /// <summary>
    /// Returns a keyword of the same type with another subschema. <c>JsonSchema.Rewrite</c> uses it.
    /// </summary>
    /// <remarks>Return this keyword if <paramref name="schema"/> is the current subschema.</remarks>
    ISingleSubschemaKeyword WithSchema(JsonSchema schema);
}

/// <summary>
/// A keyword whose value is an array of subschemas, for example <c>allOf</c> or <c>prefixItems</c>.
/// </summary>
public interface ISubschemaArrayKeyword : IJsonSchemaKeyword
{
    /// <summary>
    /// The subschemas, in document order.
    /// </summary>
    ImmutableArray<JsonSchema> Schemas { get; }

    /// <summary>
    /// Returns a keyword of the same type with other subschemas. <c>JsonSchema.Rewrite</c> uses it.
    /// </summary>
    /// <remarks>Return this keyword if <paramref name="schemas"/> is the current array.</remarks>
    ISubschemaArrayKeyword WithSchemas(ImmutableArray<JsonSchema> schemas);
}

/// <summary>
/// A keyword whose value is an object of named subschemas, for example <c>properties</c> or <c>$defs</c>.
/// </summary>
public interface ISubschemaMapKeyword : IJsonSchemaKeyword
{
    /// <summary>
    /// The subschemas by name, in document order.
    /// </summary>
    SchemaMap Schemas { get; }

    /// <summary>
    /// Returns a keyword of the same type with other subschemas. <c>JsonSchema.Rewrite</c> uses it.
    /// </summary>
    /// <remarks>Return this keyword if <paramref name="schemas"/> is the current map.</remarks>
    ISubschemaMapKeyword WithSchemas(SchemaMap schemas);
}
