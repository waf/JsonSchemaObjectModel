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
/// A keyword whose value is one subschema, for example <c>not</c> or <c>items</c>.
/// </summary>
/// <remarks>
/// <see cref="JsonSchema.FindSubschema(ReadOnlySpan{char})"/> uses this interface to walk into the subschema.
/// </remarks>
public interface ISingleSubschemaKeyword : IJsonSchemaKeyword
{
    /// <summary>
    /// The subschema.
    /// </summary>
    JsonSchema Schema { get; }
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
}

/// <summary>
/// A keyword whose value is an object of named subschemas, for example <c>properties</c> or <c>$defs</c>.
/// </summary>
public interface ISubschemaMapKeyword : IJsonSchemaKeyword
{
    /// <summary>
    /// The subschemas by name, in document order.
    /// </summary>
    IReadOnlyDictionary<string, JsonSchema> Schemas { get; }
}
